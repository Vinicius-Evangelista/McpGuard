# McpGuard — Conventions

Source: `AGENTS.md` (verified) + M1 kickoff decisions. Applies to all code written for
McpGuard.

## Language & framework

- C# with **nullable reference types** enabled (`<Nullable>enable</Nullable>`).
- **Implicit usings** enabled (`<ImplicitUsings>enable</ImplicitUsings>`).
- Target framework: **`net10.0`** on every project.
- ASP.NET Core for the two API hosts (`Gateway.Api`, `Admin.Api`) and the sample server.
- Official `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` SDK for MCP protocol
  concerns. Do not hand-roll JSON-RPC framing.

## Formatting

- **Four-space indentation.**
- PascalCase for public types and members.
- camelCase for locals and parameters.
- `Async` suffix on asynchronous methods (`RouteCallAsync`, `LogAsync`, `GetAllAsync`).
- Braces on new lines (Allman style) — match the default `dotnet new` output already in the
  repo.

## Naming

- Projects and namespaces follow the `McpGuard.<Area>` pattern:
  `McpGuard.ToolRegistry`, `McpGuard.ToolRouter`, `McpGuard.Audit`,
  `McpGuard.Gateway.Api`, `McpGuard.SampleTools.Server`, etc.
- Interfaces prefixed with `I`: `IToolRegistry`, `IToolRouter`, `IAuditSink`,
  `IMcpClientFactory`.
- Options classes suffixed with `Options`: `ToolRegistryOptions`.
- Records for value-like data: `ToolRegistration`, `RouteResult`, `AuditEvent`.

## Package management

- Central package management is **on** (`ManagePackageVersionsCentrally=true`).
- All package versions are pinned in `Directory.Packages.props` at the repo root. Do not add
  `<PackageReference Include="..." Version="...">` with a version in individual `.csproj`
  files — only `<PackageReference Include="..." />` (version resolved centrally).
- Add new packages by appending to the `<ItemGroup>` in `Directory.Packages.props`.

## Project references

- Wire the runtime dependency graph explicitly: `Gateway.Api → ToolRegistry, ToolRouter,
  Audit`; `ToolRouter → ToolRegistry, Audit`. Add the `McpGuard.SampleTools.Server` project.
- Do **not** reference `Auth`, `DlpContext`, `Redaction`, `Secrets`, `Policy`, or
  `Observability` from `Gateway.Api` in M1 — they are empty stubs and referencing them would
  pull empty dependencies into the build for no behavior.
- Control-plane projects are not referenced by anything in M1.

## Configuration

- `appsettings.json` holds local defaults. Real credentials live outside the repo (`.env` is
  gitignored; do not commit secrets).
- Custom config sections live under a top-level `McpGuard` object, e.g. `McpGuard:Tools`.
- Bind config to typed options classes via `IOptions<T>` / `services.Configure<T>(section)`.

## Git & commits

- Short **conventional commits**: `type(scope): summary`.
- Types in use: `build`, `fix`, `feat`, `docs`, `test`.
- Examples from history: `build(solution): add .NET solution scaffold`,
  `fix (solution): adjust project paths`, `feat: add agents.md to increase my AI harness`.
- Do not commit `bin/` or `obj/` output (gitignored).

## Agent-specific

- Ignore generated .NET build output. Do not read, edit, search, summarize, or include files
  under any `*/bin/*` or `*/obj/*` path unless the user explicitly requests it.