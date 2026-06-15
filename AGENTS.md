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

Solution-level `dotnet build McpGuard.slnx` currently fails during restore with
no diagnostics; verify it before relying on it.

## Coding style & naming conventions

Use C# with nullable reference types and implicit usings enabled. Use four-space
indentation, PascalCase for public types and members, camelCase for locals and
parameters, and `Async` suffixes for asynchronous methods. Name projects and
namespaces with the `McpGuard.<Area>` pattern. Keep package versions centralized
in `Directory.Packages.props`.

## Testing guidelines

No test projects are present yet. Add tests under `tests/` with matching names,
such as `McpGuard.Policy.Tests` or `McpGuard.Gateway.Api.Tests`. Prefer xUnit or
the first test framework introduced. Name tests `Method_State_ExpectedOutcome`.
Run tests with `dotnet test <test-project>.csproj`, and cover policy, routing,
redaction, and API behavior.

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
