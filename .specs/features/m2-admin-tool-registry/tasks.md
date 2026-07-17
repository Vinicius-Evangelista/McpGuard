# M2 — Admin-Controlled Tool Registry — Tasks

Ordered. `[P]` = parallelizable with siblings. Each task lists What, Where, Depends on,
Reuses, Requirement(s), Tools, Done when, Tests, Gate, and Commit. Sub-agents implement one
task each and report back.

Legend: status `[ ]` pending, `[~]` in progress, `[x]` done, `[!]` blocked.

**Design:** `.specs/features/m2-admin-tool-registry/design.md`
**Status:** Complete — T1-T18 done; full gate green (`dotnet build McpGuard.slnx` + `dotnet test McpGuard.slnx` = 63 tests pass).

---

## Execution Plan

### Phase 0: M1 follow-ups (Sequential, fast cleanup)

Closes the open M1 follow-ups so the M2 work starts from a clean, graded-1.00 baseline.

```
T1 → T2 → T3
```

### Phase 1: Store foundation (Sequential)

Builds the EF Core store seam both apps will share.

```
T4 → T5 → T6
```

### Phase 2: Async registry + router widening (Parallel OK)

Adds the async registry interface + store-backed impl + widens the router. Three independent
seams that each compile against T6's `McpDbContext`.

```
T6 complete, then:
    ├→ T7 [P]   (IAsyncToolRegistry interface + ToolRegistration widening)
    ├→ T8 [P]   (IMcpDownstreamClient.ListToolsAsync widening)
    └→ T9 [P]   (StoreToolRegistry impl)
```

T7, T8, T9 are independent at the code level (different files/projects). T10 depends on all three.

### Phase 3: Capability discovery + health checks (Parallel OK)

```
T7 + T8 + T9 complete, then:
    ├→ T10 [P]  (SdkCapabilityDiscoverer)
    └→ T11 [P]  (DownstreamHealthCheck)
```

### Phase 4: Admin API endpoints (Sequential)

All endpoints land in `McpGuard.Admin.Api`. T12 creates the hosting skeleton + server CRUD;
T13 adds discovery/resync wiring; T14 adds capability PATCH + GET; T15 adds `/health`.

```
T10 complete, then:
T12 → T13 → T14 → T15
```

### Phase 5: Gateway rewire + downstream-unreachable (Sequential)

Swaps the gateway to the store-backed async registry, forwards InputSchema, cleans up
`Program.cs`, and adds the downstream-unreachable try/catch.

```
T7 + T9 + T15 complete, then:
T16 → T17 → T18
```

### Phase 6: Integration tests + docs (Sequential)

```
T18 complete, then:
T19 → T20 → T21
```

---

## Task Breakdown

### T1: Tighten M1 audit assertion + register ISessionMigrationHandler in integration fixture

**What:** Register `ISessionMigrationHandler` in `IntegrationTestFixture.cs` (mirror
`Program.cs:20`) and assert the `initialize:initialized` audit event in
`Audit_emits_initialized_listed_allowed_and_blocked_events_in_order` with an exact count (no
`>= 4` tolerance).
**Where:** `tests/McpGuard.Gateway.Api.Tests/IntegrationTestFixture.cs`,
`tests/McpGuard.Gateway.Api.Tests/` (the audit-order test)
**Depends on:** None (Phase 0)
**Reuses:** `AuditSessionHandler` from `src/runtime/McpGuard.Gateway.Api/AuditSessionHandler.cs`
**Requirement:** M2-R30
**Tools:** MCP: none. Skill: none.
**Done when:**
- [ ] `IntegrationTestFixture.cs` registers `ISessionMigrationHandler` via the fixture's service override
- [ ] The audit-order test asserts all four event outcomes (`initialized`, `tools.listed`, `tools.call.allowed`, `tools.call.blocked`) with an exact count
- [ ] Gate passes: `dotnet test tests/McpGuard.Gateway.Api.Tests` (requires Docker)
- [ ] Test count: 7 → 7 tests pass (no silent deletions); the tightened assertion is the change
**Tests:** integration
**Gate:** full
**Commit:** `test(m1): assert initialize:initialized audit event with exact count`

---

### T2: Canonicalize `isError` in M1 spec + design, remove `-32602` envelope clause

**What:** Edit `spec.md:22` and `design.md:232-250` of the M1 spec to canonicalize the
`CallToolResult { IsError = true }` shape for blocked tool calls; remove the `-32602`
JSON-RPC envelope clause for the tool-block path. Matches the implemented behavior.
**Where:** `.specs/features/m1-mvp-tool-gateway/spec.md`,
`.specs/features/m1-mvp-tool-gateway/design.md`
**Depends on:** T1
**Reuses:** The implemented `McpGatewayHandler.CallToolAsync` `isError` shape at
`src/runtime/McpGuard.Gateway.Api/McpGatewayHandler.cs:63-70`
**Requirement:** M2-R31
**Tools:** MCP: none. Skill: none.
**Done when:**
- [ ] `spec.md:22` M1-R6 row describes the `CallToolResult { IsError = true }` shape, not `-32602`
- [ ] `design.md:232-250` "JSON-RPC error shape for blocked calls" section describes `isError`, not the `-32602` envelope
- [ ] No other `-32602` references remain in the M1 spec for the tool-block path (grep-clean)
- [ ] Gate passes: `grep -n "32602" .specs/features/m1-mvp-tool-gateway/` returns no hits in the tool-block context
**Tests:** none (docs only)
**Gate:** build (`dotnet build McpGuard.slnx` — ensures no code drift)
**Commit:** `docs(m1): canonicalize isError shape for blocked tool calls`

---

### T3: Remove duplicate UseHttpsRedirection + MapMcp block in Gateway.Api Program.cs

**What:** Remove the duplicate `app.UseHttpsRedirection()` (lines 41) and duplicate
`app.MapMcp("/mcp")` (line 44) in `Program.cs:39-44`. Keep one of each (lines 35 and 38).
**Where:** `src/runtime/McpGuard.Gateway.Api/Program.cs:39-44`
**Depends on:** T2
**Reuses:** The existing single mapping at lines 35-38
**Requirement:** M2-R32
**Tools:** MCP: none. Skill: none.
**Done when:**
- [ ] `Program.cs` has exactly one `UseHttpsRedirection` call and one `MapMcp("/mcp")` call
- [ ] Gate passes: `dotnet build src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj`
- [ ] Existing M1 integration tests still pass: `dotnet test tests/McpGuard.Gateway.Api.Tests` (7 tests)
**Tests:** integration (existing — must not regress)
**Gate:** full
**Commit:** `refactor(gateway): remove duplicate UseHttpsRedirection and MapMcp block`

---

### T4: Add EF Core SQLite packages to Directory.Packages.props

**What:** Pin `Microsoft.EntityFrameworkCore.Sqlite` 10.0.10,
`Microsoft.EntityFrameworkCore.Design` 10.0.10 (PrivateAssets=all),
`Microsoft.Extensions.Diagnostics.HealthChecks` 10.0.9 in `Directory.Packages.props`.
**Where:** `Directory.Packages.props`
**Depends on:** T3
**Reuses:** The existing central-package-management pattern
**Requirement:** M2-R16 (enables), M2-R17 (enables), M2-R21 (enables)
**Tools:** MCP: none. Skill: none.
**Done when:**
- [ ] Three new `<PackageVersion>` entries added under a new `<!-- EF Core + HealthChecks -->` comment
- [ ] Gate passes: `dotnet restore McpGuard.slnx` exits 0
- [ ] Test count: N/A (no tests yet — packages only)
**Tests:** none
**Gate:** build (`dotnet restore McpGuard.slnx`)
**Commit:** `build(deps): pin EF Core SQLite 10.0.10 and HealthChecks for M2`

---

### T5: Create McpGuard.ServerRegistry EF Core entities + McpDbContext

**What:** Replace the `Class1` placeholder in `McpGuard.ServerRegistry` with `ServerEntity`,
`CapabilityEntity`, and `McpDbContext`. Configure the one-to-many relationship, the
unique index on `(ServerId, ToolName)`, and enable SQLite WAL mode in `OnConfiguring`. Add
project refs: `Microsoft.EntityFrameworkCore.Sqlite`, `McpGuard.ToolRegistry` (for
`IAsyncToolRegistry` + `ToolRegistration` — added in T7; for now, reference the project so
the later `StoreToolRegistry` can live here). Generate the initial EF Core migration
(`Add-Migration InitialCreate` or `dotnet ef migrations add InitialCreate`).
**Where:** `src/controlplane/McpGuard.ServerRegistry/` (replace `Class1.cs`),
`src/controlplane/McpGuard.ServerRegistry/McpGuard.ServerRegistry.csproj`,
`src/controlplane/McpGuard.ServerRegistry/Migrations/`
**Depends on:** T4
**Reuses:** The `ToolRegistration` record from
`src/runtime/McpGuard.ToolRegistry/ToolRegistration.cs` (widened in T7)
**Requirement:** M2-R1 (enables), M2-R7 (enables), M2-R17 (enables)
**Tools:** MCP: none. Skill: none.
**Done when:**
- [ ] `ServerEntity` and `CapabilityEntity` match the design's Data Models section
- [ ] `McpDbContext` has `DbSet<ServerEntity> Servers` + `DbSet<CapabilityEntity> Capabilities`, the FK, the unique index, and WAL pragma in `OnConfiguring`
- [ ] Initial migration files exist under `Migrations/`
- [ ] Gate passes: `dotnet build src/controlplane/McpGuard.ServerRegistry/McpGuard.ServerRegistry.csproj`
- [ ] Test count: N/A (tests come in T9 for `StoreToolRegistry`; entity mapping is covered transitively)
**Tests:** none (entity + DbContext; mapping exercised in T9)
**Gate:** build
**Commit:** `feat(server-registry): add McpDbContext and ServerEntity/CapabilityEntity`

---

### T6: Add IAsyncToolRegistry interface + widen ToolRegistration (non-breaking)

**What:** Add `IAsyncToolRegistry` to `McpGuard.ToolRegistry` with
`GetAllAsync(CancellationToken) → Task<IReadOnlyList<ToolRegistration>>` and
`GetAsync(string, CancellationToken) → Task<ToolRegistration?>`. Widen `ToolRegistration`
with default-valued `ServerId`, `InputSchema`, `CapabilityId`. M1's 5-arg ctor calls stay
valid.
**Where:** `src/runtime/McpGuard.ToolRegistry/IAsyncToolRegistry.cs` (new),
`src/runtime/McpGuard.ToolRegistry/ToolRegistration.cs` (modify),
`src/runtime/McpGuard.ToolRegistry/McpGuard.ToolRegistry.csproj` (no new packages)
**Depends on:** T4
**Reuses:** The existing `ToolRegistration` record
**Requirement:** M2-R16
**Tools:** MCP: none. Skill: none.
**Done when:**
- [ ] `IAsyncToolRegistry` interface file exists with the two async methods
- [ ] `ToolRegistration` has `ServerId = null`, `InputSchema = null`, `CapabilityId = null` default-valued params; M1 `ConfigToolRegistry` compiles unchanged
- [ ] M1 `ConfigToolRegistryTests` still pass: `dotnet test tests/McpGuard.ToolRegistry.Tests` (5 tests)
- [ ] Gate passes: `dotnet build src/runtime/McpGuard.ToolRegistry/McpGuard.ToolRegistry.csproj`
- [ ] Test count: 5 tests pass (no silent deletions)
**Tests:** unit (existing — must not regress)
**Gate:** quick
**Commit:** `feat(tool-registry): add IAsyncToolRegistry and widen ToolRegistration`

---

### T7: Widen IMcpDownstreamClient with ListToolsAsync

**What:** Add `Task<ListToolsResult> ListToolsAsync(CancellationToken ct)` to
`IMcpDownstreamClient`. Implement it in `SdkMcpDownstreamClient` as a pass-through to
`IMcpClient.ListToolsAsync`. Update `FakeMcpDownstreamClient` in the ToolRouter test project
to add a no-op/throw default impl. M1's `DefaultToolRouter` does not call it.
**Where:** `src/runtime/McpGuard.ToolRouter/IMcpDownstreamClient.cs`,
`src/runtime/McpGuard.McpClient.Sdk/SdkMcpDownstreamClient.cs` (the `SdkMcpDownstreamClient`
class — find it), `tests/McpGuard.ToolRouter.Tests/Fakes/FakeMcpDownstreamClient.cs`
**Depends on:** T4
**Reuses:** M1's `IMcpDownstreamClient`, the SDK's `IMcpClient.ListToolsAsync`
**Requirement:** M2-R7 (enables capability discovery)
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `IMcpDownstreamClient` has `ListToolsAsync` with the SDK's `ListToolsResult` return type
- [x] `SdkMcpDownstreamClient` passes through to the underlying `IMcpClient`
- [x] `FakeMcpDownstreamClient` has the new method (throws `NotImplementedException` by default — tests that need it override)
- [x] M1 `DefaultToolRouterTests` still pass: `dotnet test tests/McpGuard.ToolRouter.Tests` (8 tests)
- [x] Gate passes: `dotnet build src/runtime/McpGuard.ToolRouter/McpGuard.ToolRouter.csproj`
- [x] Test count: 8 tests pass (no silent deletions)
**Tests:** unit (existing — must not regress)
**Gate:** quick
**Commit:** `feat(tool-router): widen IMcpDownstreamClient with ListToolsAsync`

---

### T8: Implement StoreToolRegistry (IAsyncToolRegistry, live read)

**What:** Add `StoreToolRegistry : IAsyncToolRegistry` in `McpGuard.ServerRegistry`. Ctor
takes `IDbContextFactory<McpDbContext>`. `GetAllAsync` opens a scope, queries enabled servers
+ their capabilities, maps each capability to a `ToolRegistration` (populating all 8 fields
including `ServerId`, `InputSchema` parsed from `InputSchemaJson`, `CapabilityId`).
`GetAsync` queries by tool name (first match across servers). Live read, no cache.
**Where:** `src/controlplane/McpGuard.ServerRegistry/StoreToolRegistry.cs` (new),
`src/controlplane/McpGuard.ServerRegistry/McpGuard.ServerRegistry.csproj` (add
`ProjectReference` to `McpGuard.ToolRegistry`)
**Depends on:** T5, T6
**Reuses:** `McpDbContext` (T5), `IAsyncToolRegistry` + `ToolRegistration` (T6)
**Requirement:** M2-R17, M2-R20
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `StoreToolRegistry` implements `IAsyncToolRegistry`
- [x] `GetAllAsync` returns capabilities from enabled servers, mapped to `ToolRegistration`
- [x] `GetAsync` returns the first matching capability by tool name, or null
- [x] No in-memory cache — every call opens a DbContext
- [x] New unit test project `tests/McpGuard.ServerRegistry.Tests/` created, references `McpGuard.ServerRegistry` + `Microsoft.EntityFrameworkCore.InMemory` (or SQLite temp file); tests use a hand-seeded `McpDbContext` (Object Mother pattern, no mocks)
- [x] Tests: `Store_tool_registry_returns_capabilities_from_enabled_servers`,
  `Store_tool_registry_returns_null_for_unknown_tool`,
  `Store_tool_registry_excludes_disabled_servers`,
  `Store_tool_registry_reads_live_no_cache_between_calls`,
  `Store_tool_registry_maps_input_schema_from_json`
- [x] Gate passes: `dotnet test tests/McpGuard.ServerRegistry.Tests` (≥5 tests)
- [x] Test count: ≥5 new tests pass (5 passing)
**Tests:** unit
**Gate:** quick
**Commit:** `feat(server-registry): implement StoreToolRegistry with live SQLite read`

---

### T9: Implement SdkCapabilityDiscoverer (ICapabilityDiscoverer)

**What:** Add `ICapabilityDiscoverer` + `DiscoveredTool` record + `SdkCapabilityDiscoverer`
in `McpGuard.CapabilityCatalog`. `DiscoverAsync(Uri)` uses `IMcpClientFactory.CreateAsync`,
calls `ListToolsAsync`, maps each `Tool` to `DiscoveredTool { Name, Description, InputSchema }`.
`DiscoverAsync(string serverId)` overload looks up the server URL from `McpDbContext` then
calls the URI overload. Replace the `Class1` placeholder.
**Where:** `src/controlplane/McpGuard.CapabilityCatalog/ICapabilityDiscoverer.cs` (new),
`src/controlplane/McpGuard.CapabilityCatalog/SdkCapabilityDiscoverer.cs` (new),
`src/controlplane/McpGuard.CapabilityCatalog/DiscoveredTool.cs` (new),
`src/controlplane/McpGuard.CapabilityCatalog/McpGuard.CapabilityCatalog.csproj` (add
`ProjectReference` to `McpGuard.ServerRegistry`, `McpGuard.McpClient.Sdk`)
**Depends on:** T5, T7
**Reuses:** `IMcpClientFactory` + `IMcpDownstreamClient.ListToolsAsync` (T7)
**Requirement:** M2-R7, M2-R9
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `ICapabilityDiscoverer` + `DiscoveredTool` defined
- [x] `SdkCapabilityDiscoverer` implements both overloads; URI overload uses `IMcpClientFactory.CreateAsync` + `ListToolsAsync`
- [x] New unit test project `tests/McpGuard.CapabilityCatalog.Tests/` created; tests use a hand-written `FakeMcpClientFactory` (no mocks) returning canned `ListToolsResult`; `DiscoverAsync(serverId)` overload tested with a seeded `McpDbContext` (SQLite temp file)
- [x] Tests: `Discover_async_via_downstream_returns_tools_with_schemas`,
  `Discover_async_by_server_id_lookups_url_then_discovers`,
  `Discover_async_on_unreachable_downstream_throws`,
  `Discover_async_on_empty_downstream_returns_empty_list`
- [x] Gate passes: `dotnet test tests/McpGuard.CapabilityCatalog.Tests` (≥4 tests)
- [x] Test count: ≥4 new tests pass (4 passing)
**Tests:** unit
**Gate:** quick
**Commit:** `feat(capability-catalog): implement SdkCapabilityDiscoverer via tools/list`

---

### T10: Implement DownstreamHealthCheck

**What:** Add `DownstreamHealthCheck : IHealthCheck` in `McpGuard.HealthChecks`. Ctor takes
`IDbContextFactory<McpDbContext>` + `IMcpClientFactory`. `CheckHealthAsync` enumerates
enabled servers, probes each via `IMcpClientFactory.CreateAsync` + `ListToolsAsync` with a
short timeout (default 5s, configurable via `DownstreamHealthCheckOptions`). Skips
`Enabled=false` servers. Tags each result with the server id. Returns `Healthy` only if all
enabled servers respond within the timeout.
**Where:** `src/controlplane/McpGuard.HealthChecks/DownstreamHealthCheck.cs` (new),
`src/controlplane/McpGuard.HealthChecks/DownstreamHealthCheckOptions.cs` (new),
`src/controlplane/McpGuard.HealthChecks/McpGuard.HealthChecks.csproj` (add
`ProjectReference` to `McpGuard.ServerRegistry`, `McpGuard.McpClient.Sdk`;
`PackageReference` to `Microsoft.Extensions.Diagnostics.HealthChecks`)
**Depends on:** T5, T7
**Reuses:** `IMcpClientFactory` (T7's widening enables `ListToolsAsync`)
**Requirement:** M2-R21, M2-R23
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `DownstreamHealthCheck` implements `IHealthCheck`; probes via `ListToolsAsync`
- [x] `DownstreamHealthCheckOptions` has a `Timeout` property (default 5s)
- [x] Skips disabled servers (verified in a test)
- [x] New unit test project `tests/McpGuard.HealthChecks.Tests/` created; tests use a hand-written `FakeMcpClientFactory` (fast-responding + slow-responding + throwing variants) and a seeded `McpDbContext`
- [x] Tests: `Health_check_reports_healthy_when_all_enabled_servers_reachable`,
  `Health_check_reports_unhealthy_when_any_enabled_server_unreachable`,
  `Health_check_skips_disabled_servers`,
  `Health_check_respects_timeout_for_slow_server`
- [x] Gate passes: `dotnet test tests/McpGuard.HealthChecks.Tests` (≥4 tests)
- [x] Test count: ≥4 new tests pass (4 passing)
**Tests:** unit
**Gate:** quick
**Commit:** `feat(health-checks): implement DownstreamHealthCheck via tools/list probe`

---

### T11: Wire Admin.Api hosting + server CRUD endpoints + capability discover-on-register

**What:** Wire `McpGuard.Admin.Api` `Program.cs`: `AddDbContextFactory<McpDbContext>`,
`AddMcpClientFactory`, `AddCapabilityDiscoverer`, `MapAdminEndpoints()`. Implement
`POST/GET/GET{id}/PUT{id}/DELETE{id}` on `/servers`. `POST /servers` triggers
`ICapabilityDiscoverer.DiscoverAsync(downstreamUrl)`; on success persists capabilities
(`Allowed=true, Visible=true`); on discovery failure persists server with
`DiscoveryState=discovery-failed` and returns 201 + warning. Add a `MapAdminEndpoints()`
extension method grouping the routes.
**Where:** `src/controlplane/McpGuard.Admin.Api/Program.cs` (rewrite),
`src/controlplane/McpGuard.Admin.Api/AdminEndpoints.cs` (new),
`src/controlplane/McpGuard.Admin.Api/Dtos.cs` (new: `RegisterServerRequest`, `ServerDto`,
`ResyncResultDto`), `src/controlplane/McpGuard.Admin.Api/McpGuard.Admin.Api.csproj` (add
`ProjectReference` to `McpGuard.ServerRegistry`, `McpGuard.CapabilityCatalog`,
`McpGuard.HealthChecks`; `PackageReference` to `Microsoft.EntityFrameworkCore.Sqlite`)
**Depends on:** T5, T9
**Reuses:** `McpDbContext` (T5), `SdkCapabilityDiscoverer` (T9)
**Requirement:** M2-R1, M2-R2, M2-R3, M2-R4, M2-R5, M2-R6, M2-R7, M2-R8
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `Program.cs` configures `McpDbContext` (SQLite, shared file from `McpGuard:Store:SqlitePath`), applies migrations on startup
- [x] `POST /servers` validates payload (400 on missing/malformed URL), persists server, calls discoverer, branches on success/failure
- [x] `GET /servers`, `GET /servers/{id}`, `PUT /servers/{id}`, `DELETE /servers/{id}` all work with 404 on missing
- [x] New integration test project `tests/McpGuard.Admin.Api.Tests/` created (xUnit, `Sdk.Web`, real Kestrel on a random port, SQLite temp file); tests use a hand-seeded `McpDbContext` + a fake `ICapabilityDiscoverer` (no mocks — hand-written fake returning canned `DiscoveredTool` lists)
- [x] Tests: `Register_server_persists_and_triggers_discovery`,
  `Register_server_with_invalid_url_returns_400`,
  `Register_server_when_discovery_fails_persists_with_discovery_failed_state`,
  `Get_servers_returns_all`,
  `Get_server_by_id_returns_404_for_unknown`,
  `Put_server_updates_mutable_fields`,
  `Delete_server_returns_204_or_404`
- [x] Gate passes: `dotnet test tests/McpGuard.Admin.Api.Tests` (≥7 tests)
- [x] Test count: ≥7 new tests pass (7 passing)
**Tests:** integration (real Kestrel + SQLite temp file; no Docker needed for Admin API tests)
**Gate:** full
**Commit:** `feat(admin-api): wire hosting and server CRUD with on-register discovery`

---

### T12: Add resync + capability PATCH + capability GET endpoints to Admin.Api [x]

**What:** Add `POST /servers/{id}/resync`, `PATCH /capabilities/{id}`, `GET /capabilities`,
`GET /capabilities/{id}`, `GET /servers/{id}/capabilities` to `AdminEndpoints.cs`. Resync
calls `ICapabilityDiscoverer.DiscoverAsync(serverId)`, replaces that server's capabilities,
updates `SyncedAt`. PATCH updates `Allowed`/`Visible`. 404 on unknown ids.
**Where:** `src/controlplane/McpGuard.Admin.Api/AdminEndpoints.cs` (modify),
`src/controlplane/McpGuard.Admin.Api/Dtos.cs` (add `PatchCapabilityRequest`, `CapabilityDto`)
**Depends on:** T11
**Reuses:** `SdkCapabilityDiscoverer` (T9)
**Requirement:** M2-R9, M2-R10, M2-R11, M2-R12, M2-R15, M2-R27, M2-R28, M2-R29
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `POST /servers/{id}/resync` re-discovers, replaces capabilities, updates synced-at; 404 on unknown server
- [x] `PATCH /capabilities/{id}` updates `Allowed`/`Visible`; 404 on unknown capability
- [x] `GET /capabilities`, `GET /capabilities/{id}`, `GET /servers/{id}/capabilities` all work
- [x] Tests added to `tests/McpGuard.Admin.Api.Tests/`:
  `Resync_server_replaces_capabilities_and_updates_synced_at`,
  `Resync_unknown_server_returns_404`,
  `Patch_capability_visibility_reflects_in_next_query`,
  `Patch_capability_allowed_reflects_in_next_query`,
  `Patch_unknown_capability_returns_404`,
  `Get_capabilities_returns_all`,
  `Get_capability_by_id_returns_404_for_unknown`,
  `Get_server_capabilities_returns_only_that_server`
- [x] Gate passes: `dotnet test tests/McpGuard.Admin.Api.Tests` (18 tests pass, ≥15 expected)
- [x] Test count: 18 tests pass (no silent deletions)
**Tests:** integration
**Gate:** full
**Commit:** `feat(admin-api): add resync and capability allowlist/visibility endpoints`

---

### T13: Add /health endpoint to Admin.Api

**What:** Wire `AddHealthChecks().AddCheck<DownstreamHealthCheck>()` in `Program.cs` and
`MapHealthChecks("/health")` returning 200 when all healthy, 503 when any unreachable, with
per-server status JSON. Use a custom `HealthResponseWriter` that formats
`HealthReport.Entries` into a per-server JSON.
**Where:** `src/controlplane/McpGuard.Admin.Api/Program.cs` (modify),
`src/controlplane/McpGuard.Admin.Api/HealthEndpoints.cs` (new),
`src/controlplane/McpGuard.Admin.Api/HealthResponseWriter.cs` (new)
**Depends on:** T10, T11
**Reuses:** `DownstreamHealthCheck` (T10)
**Requirement:** M2-R22
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `GET /health` returns 200 when all enabled servers reachable, 503 otherwise
- [x] Response body is JSON with per-server status keyed by server id
- [x] Tests added to `tests/McpGuard.Admin.Api.Tests/`:
  `Health_endpoint_returns_200_when_all_servers_reachable`,
  `Health_endpoint_returns_503_when_any_server_unreachable`,
  `Health_endpoint_response_includes_per_server_status`
- [x] Gate passes: `dotnet test tests/McpGuard.Admin.Api.Tests` (≥3 new + ≥15 existing = ≥18 tests)
- [x] Test count: ≥18 tests pass (18 passing)
**Tests:** integration
**Gate:** full
**Commit:** `feat(admin-api): add /health endpoint with per-server downstream status`

---

### T14: Widen DefaultToolRouter to IAsyncToolRegistry + ListVisibleToolsAsync

**What:** Change `DefaultToolRouter` ctor from `IToolRegistry` to `IAsyncToolRegistry`.
`ListVisibleTools` → `Task<IReadOnlyList<ToolRegistration>> ListVisibleToolsAsync(ct)` —
await `GetAllAsync`, filter `Allowed && Visible`. `RouteCallAsync` awaits `GetAsync`. Update
`IToolRouter.ListVisibleTools` → `ListVisibleToolsAsync`. Update `McpGatewayHandler` to await
it. Update M1 fakes in `tests/McpGuard.ToolRouter.Tests/Fakes/` (`FakeToolRegistry` →
`FakeAsyncToolRegistry` implementing `IAsyncToolRegistry`; the existing M1 `FakeToolRegistry`
stays if still referenced — otherwise replace). Keep M1 unit tests green by updating the
router tests to use the async interface.
**Where:** `src/runtime/McpGuard.ToolRouter/IToolRouter.cs`,
`src/runtime/McpGuard.ToolRouter/DefaultToolRouter.cs`,
`src/runtime/McpGuard.Gateway.Api/McpGatewayHandler.cs`,
`tests/McpGuard.ToolRouter.Tests/DefaultToolRouterTests.cs`,
`tests/McpGuard.ToolRouter.Tests/Fakes/`
**Depends on:** T6, T8
**Reuses:** `IAsyncToolRegistry` (T6), `StoreToolRegistry` (T8 — not the impl here, but the
interface it implements)
**Requirement:** M2-R13, M2-R14, M2-R18
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `IToolRouter.ListVisibleToolsAsync` returns `Task<IReadOnlyList<ToolRegistration>>`
- [x] `DefaultToolRouter` ctor takes `IAsyncToolRegistry`; `ListVisibleToolsAsync` + `RouteCallAsync` await the async registry
- [x] `McpGatewayHandler.ListToolsAsync` awaits `ListVisibleToolsAsync`
- [x] Router unit tests updated to use `IAsyncToolRegistry` fakes; existing M1 behavior tests
  pass unchanged in intent (visible filter, allowed routing, blocked routing, invisible
  blocking): `dotnet test tests/McpGuard.ToolRouter.Tests` (8 tests, possibly renamed to match
  async behavior — names stay snake_case descriptive)
- [x] Gate passes: `dotnet build src/runtime/McpGuard.ToolRouter/McpGuard.ToolRouter.csproj`
  + `dotnet test tests/McpGuard.ToolRouter.Tests`
- [x] Test count: 8 tests pass (no silent deletions; renames OK if behavior is preserved)
**Tests:** unit
**Gate:** quick
**Commit:** `refactor(tool-router): switch DefaultToolRouter to IAsyncToolRegistry`

---

### T15: Add downstream-unreachable try/catch to DefaultToolRouter.RouteCallAsync

**What:** Wrap the `IMcpClientFactory.CreateAsync` + `IMcpDownstreamClient.CallToolAsync`
calls in `DefaultToolRouter.RouteCallAsync` in try/catch. On any exception, emit a
`tools.call.blocked` `AuditEvent` with `Reason = "downstream-unreachable: <serverId>"` (no
exception internals), return `RouteResult(false, null, reason)`. Do not re-throw.
**Where:** `src/runtime/McpGuard.ToolRouter/DefaultToolRouter.cs:54-55` (after T14's widening)
**Depends on:** T14
**Reuses:** `IAuditSink` + `AuditEvent` (M1), `RouteResult` (M1)
**Requirement:** M2-R24, M2-R25, M2-R26
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `RouteCallAsync` try/catch wraps the downstream `CreateAsync` + `CallToolAsync`
- [x] Catch emits `AuditEvent` with `Outcome="tools.call.blocked"`, `Reason="downstream-unreachable: <serverId>"`
- [x] Catch returns `RouteResult(false, null, reason)`; no re-throw
- [x] Reason string does not include exception messages or stack traces
- [x] New router unit tests using a `FakeMcpClientFactory` that throws on `CreateAsync` and a
  second that returns a `FakeMcpDownstreamClient` whose `CallToolAsync` throws:
  `Route_call_on_unreachable_downstream_emits_blocked_audit_with_downstream_unreachable_reason`,
  `Route_call_on_unreachable_downstream_returns_route_result_blocked`,
  `Route_call_on_unreachable_downstream_does_not_leak_exception_in_reason`
- [x] Gate passes: `dotnet test tests/McpGuard.ToolRouter.Tests` (8 existing + 3 new = 11 tests)
- [x] Test count: 11 tests pass
**Tests:** unit
**Gate:** quick
**Commit:** `feat(tool-router): handle downstream-unreachable with blocked audit and clear reason`

---

### T16: Rewire Gateway.Api Program.cs to StoreToolRegistry + forward InputSchema

**What:** In `Gateway.Api` `Program.cs`: add `AddDbContextFactory<McpDbContext>(UseSqlite)`
reading `McpGuard:Store:SqlitePath`; add `ProjectReference` to `McpGuard.ServerRegistry`;
register `IAsyncToolRegistry → StoreToolRegistry`; change `DefaultToolRouter` registration to
resolve `IAsyncToolRegistry`; in `McpGatewayHandler.ListToolsAsync`, forward
`ToolRegistration.InputSchema` to the SDK `Tool` descriptor. Add `McpGuard:Store:SqlitePath`
to `appsettings.json` (default `./mcpguard.db`).
**Where:** `src/runtime/McpGuard.Gateway.Api/Program.cs`,
`src/runtime/McpGuard.Gateway.Api/McpGatewayHandler.cs`,
`src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj` (add `ProjectReference` to
`McpGuard.ServerRegistry`), `src/runtime/McpGuard.Gateway.Api/appsettings.json`
**Depends on:** T8, T14
**Reuses:** `StoreToolRegistry` (T8), `IAsyncToolRegistry` (T6)
**Requirement:** M2-R19, M2-R20 (gateway half)
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `Program.cs` configures `McpDbContext` + registers `IAsyncToolRegistry → StoreToolRegistry`
- [x] `DefaultToolRouter` resolves `IAsyncToolRegistry` (no longer `IToolRegistry`)
- [x] `McpGatewayHandler.ListToolsAsync` populates `Tool.InputSchema` from `ToolRegistration.InputSchema`
- [x] `appsettings.json` has `McpGuard:Store:SqlitePath` (default `./mcpguard.db`)
- [x] M1 `ConfigToolRegistry` registration kept (compat); M1 `IToolRegistry` tests still pass
- [x] Gate passes: `dotnet build src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj`
- [x] Test count: N/A at this task (integration tests in T19); unit tests for ToolRouter + ToolRegistry still pass
**Tests:** integration (covered in T19)
**Gate:** build
**Commit:** `refactor(gateway): swap to StoreToolRegistry and forward InputSchema in tools/list`

---

### T17: End-to-end integration tests — Admin API + Gateway live-read + downstream-unreachable [x]

**What:** Add M2 integration tests to `tests/McpGuard.Gateway.Api.Tests/` (and possibly a new
`tests/McpGuard.M2.Integration.Tests/` if cleaner). The tests start both apps (Admin API on a
random port, Gateway on another) sharing a temp-file SQLite; start the
`McpGuard.SampleTools.Server` Testcontainer; then drive the M2 flow: register server via
Admin API → assert `tools/list` through the gateway reflects discovered tools → PATCH a
capability to `Visible=false` → assert `tools/list` drops it (no restart) → PATCH back →
assert returns → point a tool at a dead URL → call it → assert `isError` result + audit
`tools.call.blocked` with `downstream-unreachable: <serverId>` reason.
**Where:** `tests/McpGuard.Gateway.Api.Tests/M2IntegrationTests.cs` (new) or
`tests/McpGuard.M2.Integration.Tests/` (new project — preferred, to keep M1 + M2 suites
isolated)
**Depends on:** T16, T13 (health), T15 (downstream-unreachable)
**Reuses:** The M1 integration fixture pattern (Kestrel + Testcontainers + raw JSON-RPC);
extend with an Admin API `HttpClient` + a temp-file SQLite
**Requirement:** M2-R1, M2-R7, M2-R11, M2-R12, M2-R13, M2-R14, M2-R20, M2-R24, M2-R25, M2-R26
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] New integration test project `tests/McpGuard.M2.Integration.Tests/` added to `McpGuard.slnx`
- [x] Tests use Testcontainers for the sample downstream, real Kestrel for both Admin API + Gateway, temp-file SQLite cleaned in `IAsyncLifetime.DisposeAsync`
- [x] Fixture exposes `ResetStateAsync()` to clear servers/capabilities + session + audit between tests so each test is isolated despite the shared SQLite file
- [x] Tests:
  `Register_server_via_admin_api_then_gateway_tools_list_reflects_discovered_tools`,
  `Patch_capability_visible_false_drops_tool_from_gateway_tools_list_without_restart`,
  `Patch_capability_allowed_false_blocks_gateway_tools_call`,
  `Tools_call_on_unreachable_downstream_returns_isError_and_emits_downstream_unreachable_audit`,
  `Health_endpoint_reports_unreachable_server_as_unhealthy`
- [x] Gate passes: `dotnet test tests/McpGuard.M2.Integration.Tests` (requires Docker)
- [x] Test count: 5 new tests pass (deterministic across repeated runs)
**Tests:** integration
**Gate:** full
**Commit:** `test(m2): add end-to-end integration for admin registry, live read, and unreachable path`

---

### T18: Full-solution build + test + refresh stale STRUCTURE.md [x]

**What:** Run the full gate: `dotnet build McpGuard.slnx` (all projects) and
`dotnet test McpGuard.slnx` (unit + integration). Refresh the stale
`.specs/codebase/STRUCTURE.md` (still describes the pre-M1 "20 projects, no refs" snapshot)
to reflect the M1 + M2 wiring so future milestones plan against reality.
**Where:** `.specs/codebase/STRUCTURE.md`
**Depends on:** T17
**Reuses:** The M1 `STRUCTURE.md` as a starting point
**Requirement:** (cross-cutting hygiene)
**Tools:** MCP: none. Skill: none.
**Done when:**
- [x] `dotnet build McpGuard.slnx` exits 0 (all 26 projects)
- [x] `dotnet test McpGuard.slnx` exits 0 (unit tests always; integration tests with Docker) — 63 tests pass (5+4+11+5+4+4+7+18+5)
- [x] `.specs/codebase/STRUCTURE.md` reflects the M1 + M2 project reference graph (runtime + control-plane wiring)
- [x] Test count: full solution test run passes (sum of all unit + integration tests; no silent deletions)
- [x] Gate passes: `dotnet build McpGuard.slnx` + `dotnet test McpGuard.slnx`
**Tests:** all
**Gate:** full
**Commit:** `docs(codebase): refresh STRUCTURE.md to M1+M2 wiring`

---

## Parallel Execution Map

```
Phase 0 (Sequential):
  T1 ──→ T2 ──→ T3

Phase 1 (Sequential):
  T3 ──→ T4 ──→ T5

Phase 2 (Parallel):
  T5 complete, then:
    ├── T6 [P]   (IAsyncToolRegistry + ToolRegistration widen)
    ├── T7 [P]   (IMcpDownstreamClient.ListToolsAsync)
    └── T8 [P]   (StoreToolRegistry — wait, T8 depends on T5+T6, so T8 is NOT parallel with T6)

  Correction: T8 depends on T6. Real Phase 2:
    T5 complete, then:
      ├── T6 [P]   (IAsyncToolRegistry)
      └── T7 [P]   (IMcpDownstreamClient.ListToolsAsync)
    T6 complete, then:
      └── T8       (StoreToolRegistry — depends on T5+T6)

Phase 3 (Parallel):
  T8 + T7 complete, then:
    ├── T9 [P]    (SdkCapabilityDiscoverer — depends on T5+T7)
    └── T10 [P]   (DownstreamHealthCheck — depends on T5+T7)

Phase 4 (Sequential):
  T9 complete, then:
    T11 ──→ T12 ──→ T13   (Admin.Api endpoints; T13 also needs T10)

Phase 5 (Sequential):
  T8 + T13 complete, then:
    T14 ──→ T15 ──→ T16 ──→ T17

Phase 6 (Sequential):
  T17 complete, then:
    T18
```

**Parallelism constraint:** A task marked `[P]` must have ALL of these:
- No unfinished dependencies
- Required test type is parallel-safe (per TESTING.md — unit tests are parallel-safe; integration tests that share a SQLite file or Docker are NOT parallel-safe across each other, but M2's only parallel `[P]` tasks are unit-test-based: T6, T7, T9, T10. None share mutable state.)
- No shared mutable state with other `[P]` tasks in the same phase

T6 and T7 (Phase 2) touch different projects (`ToolRegistry` vs `ToolRouter` + `McpClient.Sdk`) — no shared mutable state. T9 and T10 (Phase 3) touch different projects (`CapabilityCatalog` vs `HealthChecks`) and each owns its own test DB — no shared mutable state.

---

## Task Granularity Check

| Task | Scope | Status |
| ---- | ----- | ------ |
| T1: Tighten M1 audit assertion | 2 test files, 1 assertion change | ✅ Granular |
| T2: Canonicalize `isError` in M1 spec | 2 doc files, text edit | ✅ Granular |
| T3: Remove duplicate Program.cs block | 1 file, 6 lines | ✅ Granular |
| T4: Pin EF Core + HealthChecks packages | 1 file (`Directory.Packages.props`) | ✅ Granular |
| T5: ServerRegistry entities + McpDbContext | 1 project, entities + DbContext + migration | ⚠️ Cohesive (3 related types in one project + one migration) — OK |
| T6: IAsyncToolRegistry + ToolRegistration widen | 2 files in one project | ✅ Granular |
| T7: IMcpDownstreamClient.ListToolsAsync | 3 files (interface, impl, fake) | ⚠️ Cohesive (additive method + impl + fake) — OK |
| T8: StoreToolRegistry | 1 class + 1 csproj ref + tests | ✅ Granular |
| T9: SdkCapabilityDiscoverer | 3 files + tests in one project | ⚠️ Cohesive (interface + impl + record) — OK |
| T10: DownstreamHealthCheck | 2 files + tests | ✅ Granular |
| T11: Admin.Api hosting + server CRUD | Program.cs + endpoints + dtos + tests | ⚠️ Cohesive (hosting + CRUD endpoints + their tests) — OK per "merge backward" rule (hosting is needed to test the endpoints) |
| T12: Resync + capability PATCH/GET | 1 file modify + dtos + tests | ⚠️ Cohesive (5 endpoints + tests) — could split per endpoint, but they share DTOs and test setup; keeping atomic reduces overhead |
| T13: /health endpoint | 3 files + tests | ✅ Granular |
| T14: DefaultToolRouter async widening | 3 source files + fake + tests | ⚠️ Cohesive (interface + impl + handler + fakes + tests for one widening) — OK |
| T15: Downstream-unreachable try/catch | 1 method + tests | ✅ Granular |
| T16: Gateway Program.cs rewire + InputSchema | 3 files + appsettings | ⚠️ Cohesive (DI + handler + config for one rewire) — OK |
| T17: E2E integration tests | 1 new test project + 5 tests | ✅ Granular |
| T18: Full-solution gate + STRUCTURE.md refresh | 1 doc + 2 commands | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| ---- | ---------------------- | ------------- | ------ |
| T1 | None (Phase 0) | T1 (start) | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T2 | T2 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T4 | T4 → T5 | ✅ Match |
| T6 | T4 (parallel with T7) | T5 complete → T6 [P], T7 [P] | ✅ Match (both depend on T5's package availability via T4; T6 doesn't depend on T5's entities) |
| T7 | T4 (parallel with T6) | T5 complete → T6 [P], T7 [P] | ✅ Match |
| T8 | T5, T6 | T6 complete → T8 | ✅ Match |
| T9 | T5, T7 | T8 + T7 complete → T9 [P], T10 [P] | ✅ Match (T9 needs T5+T7; T8 done by then) |
| T10 | T5, T7 | T8 + T7 complete → T9 [P], T10 [P] | ✅ Match |
| T11 | T5, T9 | T9 complete → T11 | ✅ Match |
| T12 | T11 | T11 → T12 | ✅ Match |
| T13 | T10, T11 | T12 → T13 | ✅ Match (T13 also needs T10's health check; diagram shows T12 → T13, and T10 is complete in Phase 3) |
| T14 | T6, T8 | T8 + T13 complete → T14 | ✅ Match (T14 needs T6+T8; T13 done by then; diagram shows the start of Phase 5) |
| T15 | T14 | T14 → T15 | ✅ Match |
| T16 | T8, T14 | T15 → T16 | ✅ Match (T16 needs T8's StoreToolRegistry + T14's async router; T15 done by then) |
| T17 | T16, T13, T15 | T16 → T17 | ✅ Match (T17 needs T16 + T13 + T15; all complete by end of Phase 5) |
| T18 | T17 | T17 → T18 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires (TESTING.md) | Task Says | Status |
| ---- | --------------------------- | ---------------------------- | --------- | ------ |
| T1 | Integration test assertion (Gateway.Api.Tests) | integration | integration | ✅ OK |
| T2 | Docs only (M1 spec/design) | none | none | ✅ OK |
| T3 | Gateway.Api Program.cs (build) | integration (regression) | integration (existing) | ✅ OK |
| T4 | `Directory.Packages.props` (deps) | none | none | ✅ OK |
| T5 | ServerRegistry entities + DbContext (class library) | unit (StoreToolRegistry tests cover mapping in T8) | none (mapping covered transitively in T8) | ✅ OK (T8 co-locates the entity-mapping tests) |
| T6 | ToolRegistry interfaces + record (class library) | unit (existing M1 tests must not regress) | unit (existing) | ✅ OK |
| T7 | ToolRouter interface + impl + fake (class library) | unit (existing M1 tests must not regress) | unit (existing) | ✅ OK |
| T8 | ServerRegistry StoreToolRegistry (class library) | unit | unit | ✅ OK |
| T9 | CapabilityCatalog discoverer (class library) | unit | unit | ✅ OK |
| T10 | HealthChecks (class library) | unit | unit | ✅ OK |
| T11 | Admin.Api endpoints (web app) | integration | integration | ✅ OK |
| T12 | Admin.Api endpoints (web app) | integration | integration | ✅ OK |
| T13 | Admin.Api /health (web app) | integration | integration | ✅ OK |
| T14 | ToolRouter + Gateway.Api handler (class library + web app) | unit (router) + integration (handler covered in T17) | unit | ✅ OK (router unit tests co-located; handler integration covered in T17) |
| T15 | ToolRouter RouteCallAsync (class library) | unit | unit | ✅ OK |
| T16 | Gateway.Api Program.cs + handler (web app) | integration (covered in T17) | integration (T17) | ✅ OK (merge forward: T16's integration tests land in T17) |
| T17 | New integration test project | integration | integration | ✅ OK |
| T18 | STRUCTURE.md doc + full-solution gate | all (gate run) | all | ✅ OK |

---

## Notes for the orchestrator

- **Test counts:** T1 (7→7, tightened), T6 (5 pass), T7 (8 pass), T8 (≥5 new), T9 (≥4 new),
  T10 (≥4 new), T11 (≥7 new), T12 (≥8 new), T13 (≥3 new), T14 (8 pass, possibly renamed),
  T15 (8+3=11), T17 (≥5 new). Final solution test count should grow by ≥32 tests across M2.
- **No silent deletions:** every task's Done-when includes a test count check. If a sub-agent
  reports a lower count than expected, the orchestrator halts and inspects before proceeding.
- **Commit per task:** each task ships one commit using the `Commit` field's message. Conventional
  commit format per AGENTS.md: `type(scope): summary`.
- **EF Core migrations:** T5 creates the initial migration; T11 + T16 both call
  `db.Database.MigrateAsync()` on startup so the store is ready in either app. The Admin API owns
  writes; the gateway only reads (it still calls MigrateAsync defensively so it can boot alone in
  a dev setup, but in M2 it never writes).
- **Shared SQLite file:** both apps read `McpGuard:Store:SqlitePath`. Integration tests use a
  per-test temp file (unique name) cleaned in `DisposeAsync` to avoid cross-test contention.