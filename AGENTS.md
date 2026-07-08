# Repository Guidelines

## Project structure & module organization

McpGuard is a .NET 10 MCP gateway scaffold. Source lives under `src/`.
`src/runtime/` contains request-time gateway services such as `McpGuard.Gateway.Api`,
`Auth`, `Policy`, `ToolRegistry`, `ToolRouter`, `DlpContext`, `Redaction`,
`Secrets`, `Audit`, and `Observability`. `src/controlplane/` contains admin and
configuration services such as `McpGuard.Admin.Api`, registries, policy stores,
tenant settings, approvals, health checks, and secrets providers. Architecture
Mermaid diagrams live in `docs/*.mmd`. Central NuGet version management belongs
in `Directory.Packages.props`.

## Build, test, and development commands

- `dotnet --version`: confirm the local SDK is 10.x.
- `dotnet build src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj`:
  builds the runtime gateway API.
- `dotnet build src/controlplane/McpGuard.Admin.Api/McpGuard.Admin.Api.csproj`:
  builds the admin API.
- `dotnet run --project src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj`:
  runs the gateway locally.
- `dotnet run --project src/controlplane/McpGuard.Admin.Api/McpGuard.Admin.Api.csproj`:
  runs the admin API locally.

- `dotnet build McpGuard.slnx`: builds the full solution (25 projects).
- `dotnet test McpGuard.slnx`: runs all unit and integration tests (integration
  tests require Docker).

## Coding style & naming conventions

Use C# with nullable reference types and implicit usings enabled. Use four-space
indentation, PascalCase for public types and members, camelCase for locals and
parameters, and `Async` suffixes for asynchronous methods. Name projects and
namespaces with the `McpGuard.<Area>` pattern. Keep package versions centralized
in `Directory.Packages.props`.

## Testing guidelines

Tests live under `tests/` with matching names: `McpGuard.<Area>.Tests`. Framework: xUnit.
No mocking libraries (no Moq, no NSubstitute). Use hand-written **fakes** and **Object Mother**
pattern for test data.

Test naming: simplest descriptive sentence, first letter uppercase, snake*case, no `Should*`or`Returns`verbs. Examples:`Config_tool_registry_returns_only_configured_tools`,
`Route_call_on_disallowed_tool_returns_blocked_and_never_invokes_downstream`,
`Initialize_negotiates_protocol_and_returns_session_id`.

Unit tests are in-process, no network, no Docker. Integration tests use **Testcontainers**
to run the real sample downstream server as a container and a real Kestrel-hosted
gateway (not `WebApplicationFactory`, due to SSE streaming incompatibility with the
in-memory `TestServer` handler). The test fixture uses raw JSON-RPC over `HttpClient`
instead of the MCP SDK client to avoid SSE deadlocks.

Run unit tests:

- `dotnet test tests/McpGuard.ToolRegistry.Tests`
- `dotnet test tests/McpGuard.Audit.Tests`
- `dotnet test tests/McpGuard.ToolRouter.Tests`

Run integration tests (requires Docker):

- `dotnet test tests/McpGuard.Gateway.Api.Tests`

## Commit & pull request guidelines

Git history uses short conventional commits, such as
`build(solution): add .NET solution scaffold` and
`fix (solution): adjust project paths`. Prefer `type(scope): summary`, with
types like `build`, `fix`, `feat`, `docs`, and `test`. PRs should describe the
change, list validation commands, link related issues, and include screenshots
only for visible UI or documentation changes.

## Security & configuration tips

Do not commit secrets; `.env` is ignored. Keep `appsettings*.json` safe for local
defaults, and store real credentials outside the repository.

## Agent-specific instructions

Ignore generated .NET build output. Do not read, edit, search, summarize, or
include files under any `*/bin/*` or `*/obj/*` path unless the user explicitly
requests it.
