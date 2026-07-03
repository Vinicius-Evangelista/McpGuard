# McpGuard — Codebase Structure

Snapshot taken at M1 kickoff (2026-07-03). The repo is a pure scaffold: every project is an
empty shell. This doc records the starting point so later milestones can see what changed.

## Top-level layout

```
McpGuard/
├── .agents/                 # (empty)
├── .opencode/               # opencode skill harness (not part of the .NET build)
├── .specs/                  # this spec-driven workflow output
├── docs/                    # 4 architecture Mermaid diagrams
├── src/
│   ├── runtime/             # 10 projects (request-time gateway services)
│   └── controlplane/        # 10 projects (admin + configuration services)
├── AGENTS.md
├── Directory.Packages.props # central NuGet version management (empty <ItemGroup>)
├── McpGuard.slnx            # solution file listing all 20 projects
└── README.md                # 2-line placeholder
```

## Project inventory (all target `net10.0`, NRTs + implicit usings enabled)

### `src/runtime/` — request-time gateway services

| Project | SDK | Source files | State at M1 kickoff |
|---|---|---|---|
| `McpGuard.Gateway.Api` | `Microsoft.NET.Sdk.Web` | `Program.cs` (9 lines, bare Kestrel + HTTPS redirect), `appsettings*.json`, `launchSettings.json`, `.http` file (references nonexistent `/weatherforecast/`) | **Stub host.** Runs but maps no endpoints, registers no services, references no other project. |
| `McpGuard.Audit` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.Auth` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.DlpContext` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.Observability` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.Policy` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.Redaction` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.Secrets` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.ToolRegistry` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.ToolRouter` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |

### `src/controlplane/` — admin + configuration services

| Project | SDK | Source files | State at M1 kickoff |
|---|---|---|---|
| `McpGuard.Admin.Api` | `Microsoft.NET.Sdk.Web` | `Program.cs` (7 lines, bare Kestrel + HTTPS redirect), `appsettings*.json`, `launchSettings.json`, `.http` file | **Stub host.** No endpoints, no services. |
| `McpGuard.Approvals` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.CapabilityCatalog` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.DlpPolicyStore` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.HealthChecks` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.PolicyStore` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.RedactionRules` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.SecretsProvider` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.ServerRegistry` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |
| `McpGuard.TenantSettings` | `Microsoft.NET.Sdk` | `Class1.cs` | **Empty shell.** |

## Dependency graph at M1 kickoff

- **Project references:** none. No `.csproj` contains any `<ProjectReference>`. The 18 class
  libraries and 2 web projects are fully disconnected.
- **Package references:** none. `Directory.Packages.props` has central management enabled but
  an empty `<ItemGroup>`. No project declares any `<PackageReference>`.
- **Solution:** `McpGuard.slnx` groups the 20 projects under `/runtime/` and `/controlplane/`
  virtual folders only. Per `AGENTS.md`, solution-level `dotnet build McpGuard.slnx`
  currently fails during restore with no diagnostics — build projects individually until
  that is fixed.

## Architecture diagrams (`docs/*.mmd`)

Authored 2026-06-12, dark theme. These are the only substantive artifacts and serve as the
architectural spec for the MVP.

| File | Content |
|---|---|
| `01-system-context.mmd` | System context. Actors: Platform Admins (via Admin API), MCP Hosts/Clients. Center: McpGuard. External: Downstream MCP Servers, IdP (OIDC/OAuth), DLP/Redaction, Audit/Observability. Edges: MCP JSON-RPC over Streamable HTTP, token validation, DLP classification, OTel emission. |
| `02-runtime-components.mmd` | Runtime decomposition. Client Layer → Gateway Core (MCP Gateway API, AuthN/AuthZ, Policy Engine, DLP Context, Redaction, Tool Registry, Tool Router, Secrets Broker, Audit Log, Observability) → External Integrations (GitHub/Jira/Database MCP servers, Internal APIs). |
| `03-control-plane-components.mmd` | Control-plane map. Admin API → Server Registry, Capability Catalog, Policy Store, DLP Policy Store, Redaction Rules, Tenant Settings, Approval Workflow, Secrets Provider, Health Checks. |
| `04-request-flow.mmd` | Primary request-flow sequence. `initialize`: validate token → negotiate → create `Mcp-Session-Id` → return `InitializeResult`. `tools/list` + `tools/call`: build DLP context → evaluate discovery/execution policy → route → redact result → audit. Blocked/approval-required paths return JSON-RPC errors. |

## What M1 will add (preview — full detail in the feature spec)

- `src/runtime/McpGuard.SampleTools.Server/` — new console project, containerized sample
  downstream MCP server exposing `echo` + `add`.
- `tests/` — new directory: `McpGuard.ToolRegistry.Tests`, `McpGuard.ToolRouter.Tests`,
  `McpGuard.Audit.Tests` (unit, in-process) and `McpGuard.Gateway.Api.Tests` (integration,
  Testcontainers-driven).
- Real implementations in `ToolRegistry`, `ToolRouter`, `Audit`, and `Gateway.Api`.
- Project references: `Gateway.Api → ToolRegistry, ToolRouter, Audit`.
- Package versions pinned in `Directory.Packages.props`.
- `DlpContext`, `Redaction`, `Secrets`, `Auth`, `Policy`, `Observability` (runtime) and all
  control-plane projects remain empty shells for M1.

## Build / run commands (from AGENTS.md, verified)

- `dotnet build src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj`
- `dotnet build src/controlplane/McpGuard.Admin.Api/McpGuard.Admin.Api.csproj`
- `dotnet run --project src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj`
- `dotnet run --project src/controlplane/McpGuard.Admin.Api/McpGuard.Admin.Api.csproj`
- `dotnet build McpGuard.slnx` — **currently broken**; do not rely on it.