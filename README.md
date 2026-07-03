# McpGuard

An open-source .NET 10 MCP (Model Context Protocol) gateway that enforces tool-level governance between MCP clients and downstream MCP servers.

## What it does

McpGuard sits between MCP clients (AI agents, IDEs, chat clients) and downstream MCP servers. It ensures:

- **Clients only see approved tools** — `tools/list` returns only tools in the configured allowlist.
- **Disallowed tool calls are blocked** — `tools/call` to a non-approved tool returns a clear error; no downstream call is made.
- **Every decision is audited** — initialize, discovery, allowed calls, and blocked calls are logged as structured JSON events.

## Current milestone (M1 — MVP Tool Gateway)

- Single MCP Streamable HTTP endpoint at `POST /mcp`
- `initialize`, `tools/list`, `tools/call` handlers
- Config-driven tool allowlist (`appsettings.json`)
- Downstream routing via the official MCP C# SDK
- Structured JSON audit events to `ILogger`
- Integration tests with Testcontainers

## Run the gateway

```bash
dotnet run --project src/runtime/McpGuard.Gateway.Api
```

The gateway starts on `http://localhost:5000` (development) and serves MCP at `/mcp`.

## Run the sample downstream server

```bash
dotnet run --project src/runtime/McpGuard.SampleTools.Server
```

The sample server starts on `http://0.0.0.0:8080` and serves `echo` and `add` tools at `/mcp`.

## Run tests

Unit tests (no Docker required):

```bash
dotnet test tests/McpGuard.ToolRegistry.Tests
dotnet test tests/McpGuard.Audit.Tests
dotnet test tests/McpGuard.ToolRouter.Tests
```

Integration tests (require Docker):

```bash
dotnet test tests/McpGuard.Gateway.Api.Tests
```

## Build the sample server Docker image

```bash
docker build -t mcpguard-sample-tools \
  -f src/runtime/McpGuard.SampleTools.Server/Dockerfile \
  src/runtime/McpGuard.SampleTools.Server
```

## Architecture

See `docs/` for Mermaid diagrams and `.specs/` for the spec-driven development workflow (project vision, roadmap, feature specs, design docs, task breakdowns).

## License

TBD