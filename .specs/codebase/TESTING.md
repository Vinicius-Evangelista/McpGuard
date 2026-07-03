# McpGuard — Testing

Binding for all milestones. Recorded as a decision on 2026-07-03 and supersedes the
`Method_State_ExpectedOutcome` PascalCase convention in `AGENTS.md` for this project.

## Frameworks & tools

- **xUnit** as the test framework.
- `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` to run tests via `dotnet test`.
- `Microsoft.AspNetCore.Mvc.Testing` for `WebApplicationFactory<Program>` in-process gateway
  hosting in integration tests.
- **Testcontainers** to run the real sample downstream MCP server as a container in
  integration tests. Requires a Docker daemon available on the machine running the tests.
- **No mocking libraries.** No Moq, no NSubstitute, no `Mock<T>`. All test doubles are
  hand-written.

## Test doubles: fakes + Object Mother

- **Fakes** — hand-written implementations of the interfaces under test, living in the test
  project. They hold state the test asserts against directly.
  - Examples: `FakeToolRegistry` (constructor takes a list of `ToolRegistration` and returns
    them), `FakeAuditSink` (captures events into a `List<AuditEvent>` the test reads),
    `FakeMcpClientFactory` (returns a recorded result for a given tool name, and records
    whether it was invoked).
- **Object Mother** — a static class (or one per area) that hands back ready-made domain
  objects for common scenarios. No inline `new ToolRegistration(...)` with magic strings
  inside test bodies; go through the mother.
  - Examples: `ObjectMother.ApprovedEchoTool()`, `ObjectMother.DisallowedDangerousTool()`,
    `ObjectMother.InvisibleTool()`, `ObjectMother.EchoCallArguments("hello")`,
    `ObjectMother.AllowedRouteResult("hello")`,
    `ObjectMother.BlockedRouteResult("tool 'x' is not approved for execution")`.

## Test naming

Simplest descriptive sentence, **first letter uppercase, snake_case**, no `Should_`/`Returns`
verbs.

Examples (real M1 tests will follow this form):

- `Initialize_negotiates_protocol_and_returns_session_id`
- `Tools_list_returns_only_approved_tools`
- `Tools_call_on_approved_echo_returns_downstream_result`
- `Tools_call_on_disallowed_tool_is_blocked_with_jsonrpc_error`
- `Tools_call_on_invisible_tool_is_blocked_with_jsonrpc_error`
- `Route_call_on_invisible_tool_never_invokes_downstream`
- `Route_call_on_allowed_tool_invokes_downstream_and_returns_result`
- `Audit_sink_writes_one_json_line_per_event`
- `Config_tool_registry_returns_only_configured_tools`
- `Config_tool_registry_returns_null_for_unknown_tool`

## Test project layout

```
tests/
├── McpGuard.ToolRegistry.Tests/   # unit — FakeToolRegistry not needed here (ConfigToolRegistry
│                                  #          bound from IOptions); Object Mother for fixtures
├── McpGuard.ToolRouter.Tests/     # unit — FakeToolRegistry, FakeAuditSink, FakeMcpClientFactory
├── McpGuard.Audit.Tests/          # unit — capture ILogger output via ITestOutputHelper
└── McpGuard.Gateway.Api.Tests/    # integration — WebApplicationFactory<Program> + Testcontainers
```

- Unit test projects target `net10.0` and reference the single runtime project they test.
- Integration test project references `McpGuard.Gateway.Api` and (transitively) the runtime
  projects.

## Unit vs integration split

- **Unit** = no network, no Docker, no real MCP client. All downstream/registry/audit
  collaborators are fakes. Fast; runs anywhere.
- **Integration** = real gateway in-process via `WebApplicationFactory<Program>` + real
  sample downstream server in a Testcontainer. Requires Docker. Exercises the full
  initialize → tools/list → tools/call → audit path end to end.

## Integration test flow (M1)

1. Build the `McpGuard.SampleTools.Server` Docker image (once per test run; cache locally).
2. Testcontainers starts the image; gets a random host port mapped to container `8080`.
3. `WebApplicationFactory<Program>` hosts the gateway in-process with
   `McpGuard:Tools` overridden so `echo`/`add` `DownstreamUrl` = the container URL, plus a
   `dangerous` tool entry pointing at the same URL with `Allowed=false, Visible=false`.
4. A real SDK MCP client (in the test) drives the gateway:
   `initialize` → `tools/list` → `tools/call echo` → `tools/call dangerous` →
   `tools/call <invisible>`.
5. Audit events are captured by injecting a test `IAuditSink` (or by reading the
   `ILogger` output via `ITestOutputHelper`) and asserting the four event types are emitted
   with the expected fields.
6. Testcontainers tears the container down (xUnit `IAsyncLifetime`).

## Commands

- `dotnet test tests/McpGuard.ToolRegistry.Tests/McpGuard.ToolRegistry.Tests.csproj`
- `dotnet test tests/McpGuard.ToolRouter.Tests/McpGuard.ToolRouter.Tests.csproj`
- `dotnet test tests/McpGuard.Audit.Tests/McpGuard.Audit.Tests.csproj`
- `dotnet test tests/McpGuard.Gateway.Api.Tests/McpGuard.Gateway.Api.Tests.csproj`
  *(requires Docker daemon running)*
- `dotnet test` (from repo root, once solution build is fixed) runs all four.

## Build the sample server image (one-time / before integration tests)

```
docker build -t mcpguard-sample-tools \
  -f src/runtime/McpGuard.SampleTools.Server/Dockerfile \
  src/runtime/McpGuard.SampleTools.Server
```

The integration test project may also build the image programmatically via Testcontainers'
`ImageFromDockerfile` builder, avoiding the manual `docker build` step. Preferred: use
`ImageFromDockerfile` so `dotnet test` is self-contained.

## Coverage expectations for M1

| Area | Test type | Must cover |
|---|---|---|
| `ConfigToolRegistry` | unit | returns only configured tools; returns null for unknown tool |
| `DefaultToolRouter` | unit | visible-filter hides disallowed + invisible; allowed call routes + returns; disallowed call blocks + never invokes downstream; invisible call blocks + never invokes downstream |
| `LoggerAuditSink` | unit | writes exactly one JSON line per event with expected fields (timestamp, session id, method, tool name, outcome, reason) |
| `Gateway.Api` end-to-end | integration | initialize; tools/list hides dangerous; tools/call echo + add return downstream result; tools/call dangerous + invisible return JSON-RPC error; audit emits allowed + blocked events |

## Gate check (every task)

A task is not done until its gate check passes: the relevant `dotnet test` (or `dotnet build`
for task 1) command exits 0. Sub-agents implementing a task report the gate command and its
exit code back to the orchestrator.