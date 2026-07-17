# M1 — MVP Tool Gateway — Design

Companion to `spec.md`. Architecture for the M1 runtime slice only.

## Component slice (only the runtime projects M1 touches)

```
Client  --POST /mcp (Streamable HTTP)-->  McpGuard.Gateway.Api
                                            |
                                            v
                       SDK MCP server handlers (in Gateway.Api)
                         initialize / tools/list / tools/call
                            |   delegate to:
                            v
                          McpGuard.ToolRegistry  (IToolRegistry)
                            |   ToolRegistration {Name, Description,
                            |   InputSchema, DownstreamUrl, Allowed, Visible}
                            v
                          McpGuard.ToolRouter    (IToolRouter)
                            |   ListVisibleToolsAsync  -> approved + visible only
                            |   RouteCallAsync         -> guard -> route -> audit
                            v
                          McpGuard.Audit         (IAuditSink -> ILogger)
```

`McpGuard.SampleTools.Server` is a separate console project downstream of the gateway at
runtime; it is not referenced by `Gateway.Api` at compile time.

## Project reference graph (M1, compile-time)

```
McpGuard.Gateway.Api
├── McpGuard.ToolRegistry
├── McpGuard.ToolRouter
│   ├── McpGuard.ToolRegistry
│   └── McpGuard.Audit
└── McpGuard.Audit

McpGuard.SampleTools.Server   (standalone — referenced by nothing; containerized)

tests/McpGuard.ToolRegistry.Tests   -> McpGuard.ToolRegistry
tests/McpGuard.Audit.Tests          -> McpGuard.Audit
tests/McpGuard.ToolRouter.Tests     -> McpGuard.ToolRouter
tests/McpGuard.Gateway.Api.Tests    -> McpGuard.Gateway.Api
```

Empty-stub projects are NOT referenced. `Gateway.Api` does not reference `Auth`, `Policy`,
`DlpContext`, `Redaction`, `Secrets`, or `Observability` in M1.

## Key types

### `McpGuard.ToolRegistry`

```csharp
public sealed record ToolRegistration(
    string Name,
    string Description,
    JsonElement? InputSchema,
    Uri DownstreamUrl,
    bool Allowed,
    bool Visible);

public interface IToolRegistry
{
    IReadOnlyList<ToolRegistration> GetAll(CancellationToken ct);
    ToolRegistration? Get(string name, CancellationToken ct);
}

public sealed class ToolRegistryOptions
{
    public List<ToolEntry> Tools { get; init; } = new();
}

public sealed class ToolEntry
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public Uri DownstreamUrl { get; init; } = new("http://localhost");
    public bool Allowed { get; init; } = true;
    public bool Visible { get; init; } = true;
}

public sealed class ConfigToolRegistry : IToolRegistry
{
    // ctor(IOptions<ToolRegistryOptions>) — maps ToolEntry -> ToolRegistration
}
```

`InputSchema` for M1 is not fetched from the downstream server; it is omitted (null) in the
config-driven registry. The SDK fills tool descriptors for `tools/list` from the registry's
`Name`/`Description`; the gateway returns the schema as null/empty if not known. (M2 will
fetch schemas from the downstream server via the capability catalog.) This keeps M1 honest:
the gateway returns what it is configured to return, not what it discovers at runtime.

### `McpGuard.ToolRouter`

```csharp
public interface IToolRouter
{
    IReadOnlyList<ToolRegistration> ListVisibleTools(CancellationToken ct);
    Task<RouteResult> RouteCallAsync(
        string toolName, JsonElement arguments, string sessionId,
        CancellationToken ct);
}

public sealed record RouteResult(bool Allowed, object? Result, string? BlockReason);

public interface IMcpClientFactory
{
    Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct);
}

public interface IMcpDownstreamClient
{
    Task<object> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct);
    ValueTask DisposeAsync();
}

public sealed class DefaultToolRouter : IToolRouter
{
    // ctor(IToolRegistry, IAuditSink, IMcpClientFactory)
    // ListVisibleTools: registry.GetAll where Allowed && Visible
    // RouteCallAsync:
    //   1. reg = registry.Get(toolName)
    //   2. if reg is null || !reg.Allowed || !reg.Visible:
    //        audit.LogAsync(blocked, reason="tool '<name>' is not approved for execution")
    //        return RouteResult(false, null, reason)
    //   3. audit.LogAsync(allowed)
    //   4. client = factory.CreateAsync(reg.DownstreamUrl)
    //   5. result = client.CallToolAsync(toolName, arguments)
    //   6. return RouteResult(true, result, null)
}
```

`IMcpClientFactory` + `IMcpDownstreamClient` are thin wrappers around the SDK's
`McpClientFactory.CreateAsync` and the resulting `IMcpClient.CallToolAsync`. We own the
wrappers so unit tests inject fakes and never touch the network. The SDK is the only
implementation of these interfaces (in `McpGuard.ToolRouter` or a small adapter in
`Gateway.Api`).

### `McpGuard.Audit`

```csharp
public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string SessionId,
    string Method,
    string? ToolName,
    string Outcome,
    string? Reason);

public interface IAuditSink
{
    Task LogAsync(AuditEvent evt, CancellationToken ct);
}

public sealed class LoggerAuditSink : IAuditSink
{
    // ctor(ILogger<LoggerAuditSink>)
    // LogAsync: serialize evt to a single JSON line (System.Text.Json,
    //           camelCase, no indentation) and write via ILogger at Information level.
}
```

`Outcome` values: `"initialized"`, `"tools.listed"`, `"tools.call.allowed"`,
`"tools.call.blocked"`. `Method` is the JSON-RPC method (`"initialize"`, `"tools/list"`,
`"tools/call"`).

### `McpGuard.Gateway.Api`

`Program.cs`:
- `WebApplication.CreateBuilder(args)`.
- `services.Configure<ToolRegistryOptions>(builder.Configuration.GetSection("McpGuard:Tools"))`.
- Register `IToolRegistry → ConfigToolRegistry`, `IAuditSink → LoggerAuditSink`,
  `IMcpClientFactory → SdkMcpClientFactory`, `IToolRouter → DefaultToolRouter`.
- `services.AddMcpServer()` (SDK) configured for HTTP transport.
- `builder.Services.AddLogging()` with console sink (default).
- `var app = builder.Build();`
- HTTPS redirect only when not in Development (so integration tests hit plain HTTP).
- `app.MapMcpHttpTransport("/mcp")` (or the SDK's equivalent HTTP mapping) and register
  handlers for `initialize`, `tools/list`, `tools/call` that delegate to `IToolRouter` +
  `IAuditSink`. Session id comes from the SDK's session manager and is read in the handler to
  pass into `AuditEvent.SessionId` and `RouteCallAsync`.
- `app.Run();`

`appsettings.json` `McpGuard:Tools` example (three tools: two approved + visible, one not):

```json
{
  "McpGuard": {
    "Tools": [
      { "Name": "echo", "Description": "Echoes the input message",
        "DownstreamUrl": "http://127.0.0.1:5010/mcp", "Allowed": true, "Visible": true },
      { "Name": "add",  "Description": "Adds two integers",
        "DownstreamUrl": "http://127.0.0.1:5010/mcp", "Allowed": true, "Visible": true },
      { "Name": "dangerous", "Description": "A hidden, disallowed tool",
        "DownstreamUrl": "http://127.0.0.1:5010/mcp", "Allowed": false, "Visible": false }
    ]
  }
}
```

### `McpGuard.SampleTools.Server`

Console app (`Microsoft.NET.Sdk.Web` + `ModelContextProtocol.AspNetCore`):
- `Program.cs` — `WebApplication.CreateBuilder` → `services.AddMcpServer()` → register two
  tool handlers: `echo(message)` returns `message` unchanged; `add(a, b)` returns `a + b`.
- Listens on `http://0.0.0.0:8080/mcp` (container-friendly).
- `Dockerfile` — multi-stage `dotnet` build → runtime image exposing `8080`. Added at the
  project root: `src/runtime/McpGuard.SampleTools.Server/Dockerfile`.
- Added to `McpGuard.slnx` under `/runtime/`.

## MCP SDK usage notes

- **Client-facing server** (`Gateway.Api`): `ModelContextProtocol.AspNetCore` —
  `AddMcpServer()` + `WithHttpTransport()` / `MapMcpHttpTransport("/mcp")`. The SDK manages
  the `Mcp-Session-Id` header and the Streamable HTTP framing. The gateway registers
  handlers for the three methods; the SDK handles the JSON-RPC envelope and returns
  JSON-RPC errors for us when we throw or return an error result.
- **Downstream client** (`ToolRouter` via `IMcpClientFactory`): `ModelContextProtocol` —
  `McpClientFactory.CreateAsync` pointed at `DownstreamUrl`; call `CallToolAsync(name, args)`
  on the resulting `IMcpClient`. Wrap in our own `IMcpDownstreamClient` so unit tests inject
  fakes.
- **Packages pinned in `Directory.Packages.props`:**
  - `ModelContextProtocol` (latest 1.4.x)
  - `ModelContextProtocol.AspNetCore` (latest 1.4.x)
  - `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`
  - `Microsoft.AspNetCore.Mvc.Testing`
  - `Testcontainers` (core, with `ImageFromDockerfile` to build the sample server image
    programmatically)

## Blocked-call result shape (M1-R6)

When `RouteCallAsync` returns `Allowed=false`, the gateway handler returns a
`CallToolResult` with `IsError = true` and a `TextContentBlock` carrying the block reason:

```json
{
  "jsonrpc": "2.0",
  "id": "<request id>",
  "result": {
    "content": [
      { "type": "text", "text": "tool 'dangerous' is not approved for execution" }
    ],
    "isError": true
  }
}
```

This is the MCP SDK's tool-call error contract: the handler returns a `CallToolResult`, and
the SDK serializes it (including `isError: true`) rather than emitting a raw JSON-RPC error
envelope. The message always names the tool and the reason. No downstream call is made for a
blocked tool (asserted in unit tests via `FakeMcpClientFactory`).

> **Reconciled 2026-07-16 (M2 T2).** An earlier draft of this section specified a raw
> `-32602` (invalid params) JSON-RPC error envelope. The implementation uses the SDK's
> `isError` result path instead, which is how the MCP SDK surfaces tool-call errors when a
> handler returns a `CallToolResult`. The `isError` shape is canonical for M1; the
> `-32602` envelope clause has been removed from the spec and this design.

## Integration test architecture (M1-R9)

```
[McpGuard.Gateway.Api.Tests]
   |
   +-- WebApplicationFactory<Program>  (gateway in-process, plain HTTP)
   |      overrides McpGuard:Tools:*DownstreamUrl -> container URL
   |
   +-- Testcontainers  (ImageFromDockerfile builds & starts
                        McpGuard.SampleTools.Server image)
   |      container port 8080 -> random host port
   |
   +-- SDK MCP client (in test) -> gateway /mcp
          initialize
          tools/list
          tools/call echo
          tools/call add
           tools/call dangerous   (expect isError result)
           tools/call <invisible> (expect isError result)
          assert audit events captured
```

Audit capture: the integration test replaces `IAuditSink` with a `CapturingAuditSink` fake
via `WebApplicationFactory`'s service override, then asserts the four event types appear in
order with the expected fields.

## Traceability

| Requirement | Implemented in |
|---|---|
| M1-R1 | `Gateway.Api` `Program.cs` `/mcp` mapping |
| M1-R2 | SDK `initialize` handler in `Gateway.Api` + audit `initialized` |
| M1-R3 | `DefaultToolRouter.ListVisibleTools` + `tools/list` handler |
| M1-R4 | `ToolRegistry` project (`ToolRegistration`, `IToolRegistry`, `ConfigToolRegistry`, `ToolRegistryOptions`, `ToolEntry`) |
| M1-R5 | `DefaultToolRouter.RouteCallAsync` allowed branch + `IMcpClientFactory`/`IMcpDownstreamClient` SDK impl |
| M1-R6 | `DefaultToolRouter.RouteCallAsync` blocked branch + gateway handler returning `CallToolResult { IsError = true }` |
| M1-R7 | `Audit` project (`AuditEvent`, `IAuditSink`, `LoggerAuditSink`) + handler call sites |
| M1-R8 | `.csproj` `<ProjectReference>` wiring |
| M1-R9 | `tests/` four projects |
| M1-R10 | `McpGuard.SampleTools.Server` project + `Dockerfile` + slnx entry |