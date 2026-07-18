# McpGuard — Codebase Structure

Snapshot originally taken at M1 kickoff (2026-07-03). This revision captures the state after
M1 + M2 landed: the runtime/control-plane wiring is in place, EF Core + SQLite back the registry,
and the Admin API exposes server + capability + health endpoints. Future milestones plan from
this baseline.

## Top-level layout

```
McpGuard/
├── .agents/                 # (empty)
├── .opencode/               # opencode skill harness (not part of the .NET build)
├── .specs/                  # spec-driven workflow output (specs, design, tasks, codebase docs)
├── docs/                    # 4 architecture Mermaid diagrams
├── src/
│   ├── runtime/             # 11 projects (request-time gateway services — +SampleTools.Server)
│   └── controlplane/        # 10 projects (admin + configuration services)
├── tests/                   # 9 test projects (7 unit + 2 integration)
├── AGENTS.md
├── Directory.Packages.props # central NuGet version management (EF Core, HealthChecks, xUnit, MCP SDK, Testcontainers)
├── McpGuard.slnx            # solution file listing all 26 projects (25 source + test)
└── README.md
```

## Project inventory (all target `net10.0`, NRTs + implicit usings enabled)

### `src/runtime/` — request-time gateway services

| Project | SDK | State after M2 | Notable types |
|---|---|---|---|
| `McpGuard.Gateway.Api` | `Microsoft.NET.Sdk.Web` | **Live host.** Wires `McpDbContext` + `StoreToolRegistry` + `DefaultToolRouter` + `McpGatewayHandler`; maps `/mcp` via `MapMcp`. `appsettings.json` carries `McpGuard:Store:SqlitePath`. | `Program.cs`, `McpGatewayHandler`, `AuditSessionHandler` |
| `McpGuard.Audit` | `Microsoft.NET.Sdk` | **Implemented M1.** Logger-based audit sink + `AuditEvent` record. | `AuditEvent`, `LoggerAuditSink`, `IAuditSink` |
| `McpGuard.ToolRegistry` | `Microsoft.NET.Sdk` | **Implemented M1+M2.** M1 sync `IToolRegistry` + `ConfigToolRegistry`; M2 adds `IAsyncToolRegistry` + widens `ToolRegistration` with `ServerId`/`InputSchema`/`CapabilityId`. | `IToolRegistry`, `IAsyncToolRegistry`, `ToolRegistration`, `ConfigToolRegistry` |
| `McpGuard.ToolRouter` | `Microsoft.NET.Sdk` | **Implemented M1+M2.** `IToolRouter` async widened (`ListVisibleToolsAsync`, `RouteCallAsync`); `DefaultToolRouter` ctor now takes `IAsyncToolRegistry`; `RouteCallAsync` try/catches downstream-unreachable and emits `tools.call.blocked` audit. | `IToolRouter`, `DefaultToolRouter`, `IMcpDownstreamClient`, `RouteResult` |
| `McpGuard.McpClient.Sdk` | `Microsoft.NET.Sdk` | **Implemented M1+M2.** Adapter over the MCP SDK; `SdkMcpClientFactory` + `SdkMcpDownstreamClient` (M2 added `ListToolsAsync`). | `IMcpClientFactory`, `SdkMcpClientFactory`, `IMcpDownstreamClient`, `SdkMcpDownstreamClient` |
| `McpGuard.SampleTools.Server` | `Microsoft.NET.Sdk.Web` (console) | **Implemented M1.** Containerized sample downstream MCP server exposing `echo` + `add`. Used by integration tests via Testcontainers. | `Program.cs` |
| `McpGuard.Auth` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M3. | `Class1.cs` |
| `McpGuard.DlpContext` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M4. | `Class1.cs` |
| `McpGuard.Observability` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M6. | `Class1.cs` |
| `McpGuard.Policy` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M3. | `Class1.cs` |
| `McpGuard.Redaction` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M4. | `Class1.cs` |
| `McpGuard.Secrets` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M5. | `Class1.cs` |

### `src/controlplane/` — admin + configuration services

| Project | SDK | State after M2 | Notable types |
|---|---|---|---|
| `McpGuard.Admin.Api` | `Microsoft.NET.Sdk.Web` | **Live host.** Wires `McpDbContext` + `SdkCapabilityDiscoverer` + `DownstreamHealthCheck`; applies migrations on startup. Endpoints: `POST/GET/GET{id}/PUT{id}/DELETE{id}` on `/servers`, `POST /servers/{id}/resync`, `GET /servers/{id}/capabilities`, `GET/PATCH /capabilities`, `GET /capabilities/{id}`, `GET /health`. | `Program.cs`, `AdminEndpoints`, `HealthEndpoints`, `HealthResponseWriter`, `Dtos.cs` |
| `McpGuard.ServerRegistry` | `Microsoft.NET.Sdk` | **Implemented M2.** EF Core store seam: `McpDbContext` + `ServerEntity` + `CapabilityEntity` (FK, unique `(ServerId, ToolName)` index, SQLite WAL); `StoreToolRegistry : IAsyncToolRegistry` (live read, no cache). Initial migration under `Migrations/`. | `McpDbContext`, `ServerEntity`, `CapabilityEntity`, `StoreToolRegistry` |
| `McpGuard.CapabilityCatalog` | `Microsoft.NET.Sdk` | **Implemented M2.** `ICapabilityDiscoverer` + `DiscoveredTool` + `SdkCapabilityDiscoverer` (calls `ListToolsAsync`, maps to `DiscoveredTool`; both `DiscoverAsync(Uri)` and `DiscoverAsync(serverId)` overloads). | `ICapabilityDiscoverer`, `DiscoveredTool`, `SdkCapabilityDiscoverer` |
| `McpGuard.HealthChecks` | `Microsoft.NET.Sdk` | **Implemented M2.** `DownstreamHealthCheck : IHealthCheck` probes each enabled server via `IMcpClientFactory` + `ListToolsAsync` with a configurable timeout (default 5s); skips disabled servers. | `DownstreamHealthCheck`, `DownstreamHealthCheckOptions` |
| `McpGuard.Approvals` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M5. | `Class1.cs` |
| `McpGuard.DlpPolicyStore` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M4. | `Class1.cs` |
| `McpGuard.PolicyStore` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M3. | `Class1.cs` |
| `McpGuard.RedactionRules` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M4. | `Class1.cs` |
| `McpGuard.SecretsProvider` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M5. | `Class1.cs` |
| `McpGuard.TenantSettings` | `Microsoft.NET.Sdk` | **Empty shell.** Deferred to M3. | `Class1.cs` |

## Test projects (`tests/`, all xUnit, no mocking libs — hand-written fakes + Object Mother)

| Project | Type | Tests | What it covers |
|---|---|---|---|
| `McpGuard.ToolRegistry.Tests` | unit | 5 | `ConfigToolRegistry` (M1) |
| `McpGuard.Audit.Tests` | unit | 4 | `LoggerAuditSink` ordering + outcomes |
| `McpGuard.ToolRouter.Tests` | unit | 11 | visible filter, allowed routing, blocked routing, invisible blocking, downstream-unreachable path |
| `McpGuard.ServerRegistry.Tests` | unit | 5 | `StoreToolRegistry` live read, mapping, disabled-server exclusion, no-cache, input-schema JSON |
| `McpGuard.CapabilityCatalog.Tests` | unit | 4 | `SdkCapabilityDiscoverer` via fake `IMcpClientFactory`, by-server-id lookup, unreachable throws, empty downstream |
| `McpGuard.HealthChecks.Tests` | unit | 4 | healthy/unreachable/skip-disabled/timeout paths |
| `McpGuard.Gateway.Api.Tests` | integration (Testcontainers) | 12 | M1 end-to-end (initialize, tools/list, tools/call allowed + blocked, audit order) + admin-gateway end-to-end (register → tools/list reflects, visible PATCH, allowed PATCH blocks, downstream-unreachable, /health unhealthy) |
| `McpGuard.Admin.Api.Tests` | integration (Kestrel + SQLite temp file) | 18 | server CRUD, on-register discovery, resync, capability PATCH/GET, /health |

Total after M2: **63 tests** (5+4+11+5+4+4+12+18). Unit tests run in-process, no network/Docker.
Integration tests need Docker for Testcontainers (sample downstream + admin-gateway fixture).

## Dependency graph after M2

Project references now wire the runtime and control-plane together. Central package versions
live in `Directory.Packages.props` (MCP SDK, EF Core Sqlite/Design, HealthChecks, xUnit,
Testcontainers, Microsoft.Extensions.*).

### Runtime

```
Gateway.Api → Audit, ToolRegistry, ToolRouter, McpClient.Sdk, ServerRegistry (M2), SampleTools.Server (self-contained)
ToolRouter → Audit, ToolRegistry, McpClient.Sdk
McpClient.Sdk → (MCP SDK + Microsoft.Extensions.Http)
SampleTools.Server → (MCP SDK server)
```

### Control plane

```
Admin.Api → ServerRegistry, CapabilityCatalog, HealthChecks, McpClient.Sdk
ServerRegistry → ToolRegistry (for IAsyncToolRegistry + ToolRegistration)
CapabilityCatalog → ServerRegistry, McpClient.Sdk
HealthChecks → ServerRegistry, McpClient.Sdk
```

### Tests

Each test project references the projects it exercises. Integration test projects also reference
`Testcontainers` + a SQLite temp file (Admin/Gateway) or a real Testcontainer (sample downstream)
for the M1 and M2 end-to-end fixtures.

## Shared SQLite store

Both `McpGuard.Gateway.Api` and `McpGuard.Admin.Api` read `McpGuard:Store:SqlitePath`
(default `./mcpguard.db`) and call `db.Database.MigrateAsync()` on startup. The Admin API owns
writes (servers + capabilities); the gateway only reads (it still migrates defensively so it can
boot alone in a dev setup). `StoreToolRegistry` opens a fresh `McpDbContext` per call — live read,
0-second visibility window for Admin API mutations. Integration tests use a per-fixture temp file
cleaned in `DisposeAsync` to avoid cross-test contention.

## Architecture diagrams (`docs/*.mmd`)

Authored 2026-06-12, dark theme. These remain the architectural spec for the MVP and M2.

| File | Content |
|---|---|
| `01-system-context.mmd` | System context. Actors: Platform Admins (via Admin API), MCP Hosts/Clients. Center: McpGuard. External: Downstream MCP Servers, IdP (OIDC/OAuth), DLP/Redaction, Audit/Observability. Edges: MCP JSON-RPC over Streamable HTTP, token validation, DLP classification, OTel emission. |
| `02-runtime-components.mmd` | Runtime decomposition. Client Layer → Gateway Core (MCP Gateway API, AuthN/AuthZ, Policy Engine, DLP Context, Redaction, Tool Registry, Tool Router, Secrets Broker, Audit Log, Observability) → External Integrations (GitHub/Jira/Database MCP servers, Internal APIs). |
| `03-control-plane-components.mmd` | Control-plane map. Admin API → Server Registry, Capability Catalog, Policy Store, DLP Policy Store, Redaction Rules, Tenant Settings, Approval Workflow, Secrets Provider, Health Checks. |
| `04-request-flow.mmd` | Primary request-flow sequence. `initialize`: validate token → negotiate → create `Mcp-Session-Id` → return `InitializeResult`. `tools/list` + `tools/call`: build DLP context → evaluate discovery/execution policy → route → redact result → audit. Blocked/approval-required paths return JSON-RPC errors. |

## Build / run / test commands (verified)

- `dotnet build McpGuard.slnx` — full solution build (all 26 projects). ✅ exits 0 after M2.
- `dotnet test McpGuard.slnx` — full test gate (unit always; integration with Docker). ✅ exits 0 after M2.
- `dotnet build src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj` — runtime gateway only.
- `dotnet build src/controlplane/McpGuard.Admin.Api/McpGuard.Admin.Api.csproj` — admin API only.
- `dotnet run --project src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj` — runs the gateway locally.
- `dotnet run --project src/controlplane/McpGuard.Admin.Api/McpGuard.Admin.Api.csproj` — runs the admin API locally.
- Unit-only quick gate: `dotnet test tests/McpGuard.ToolRegistry.Tests tests/McpGuard.Audit.Tests tests/McpGuard.ToolRouter.Tests tests/McpGuard.ServerRegistry.Tests tests/McpGuard.CapabilityCatalog.Tests tests/McpGuard.HealthChecks.Tests` (no Docker needed).
- Integration: `dotnet test tests/McpGuard.Gateway.Api.Tests tests/McpGuard.Admin.Api.Tests` (Docker required).

## What M3+ will add (preview — full detail in the feature specs)

- **M3 (Auth + Policy + Tenancy):** real implementations in `McpGuard.Auth`, `McpGuard.Policy`,
  `McpGuard.PolicyStore`, `McpGuard.TenantSettings`. `IAuthenticator` (OIDC/JWT),
  `ITenantContext`, policy engine with allow/deny/approval-required decision model. Gateway
  `Program.cs` will gain auth middleware.
- **M4 (DLP + Redaction):** `McpGuard.DlpContext`, `McpGuard.Redaction`,
  `McpGuard.DlpPolicyStore`, `McpGuard.RedactionRules`. Pre/post-call redaction against the
  DLP context.
- **M5 (Secrets + Approvals):** `McpGuard.Secrets`, `McpGuard.SecretsProvider`,
  `McpGuard.Approvals`. Approval workflow for tools; secret brokering.
- **M6 (Observability):** `McpGuard.Observability` — OpenTelemetry traces, persistent audit
  sink beyond M1's `LoggerAuditSink`.