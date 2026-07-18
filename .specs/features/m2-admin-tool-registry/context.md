# M2 — Admin-Controlled Tool Registry — Context

**Gathered:** 2026-07-16
**Spec:** `.specs/features/m2-admin-tool-registry/spec.md`
**Status:** Ready for design

---

## Feature Boundary

M2 delivers an admin-controlled tool registry. An operator can register downstream MCP
servers via a new Admin API, the Admin API auto-discovers each server's tools via `tools/list`,
the operator can toggle tool visibility/enabled state at runtime, and the gateway reads tool
metadata from a persistent SQLite store on every `tools/list` call. M2 also owns the
downstream-unreachable failure path in `DefaultToolRouter` and closes the open M1 follow-ups.

Auth, tenant scoping, the policy engine, DLP, redaction, secrets, approvals, observability,
and persistent audit are explicitly out of scope (deferred to M3–M6 per the Notion milestone
split).

---

## Implementation Decisions

### M2 milestone split (auth + tenant → M3)

- M2 follows the **Notion** "M2: Admin-Controlled Tool Registry" definition, not the earlier
  local `ROADMAP.md` draft that merged auth into M2.
- OIDC bearer validation, `IAuthenticator`, JWT/OIDC packages, `ITenantContext`, tenant
  scoping, `PolicyStore`, and `TenantSettings` are all deferred to **M3 (Policy + Identity)**.
- The local `ROADMAP.md` will be updated as part of M2 docs to match the Notion split.

### 1. Auth interception point

- **Decision:** out of scope for M2 (deferred to M3). No auth interception is built in M2.
- The Admin API will have **no authentication in M2** — it is an open admin surface for the M2
  demo/iteration loop. A note in the Admin API spec/README will flag that auth lands in M3 and
  the surface must be protected by network/policy in the meantime.

### 2. Store-backed IToolRegistry shape

- **Decision:** introduce a new `IAsyncToolRegistry` interface alongside M1's stable sync
  `IToolRegistry`. The store-backed `StoreToolRegistry` implements `IAsyncToolRegistry`.
  `DefaultToolRouter` switches to depend on `IAsyncToolRegistry`. M1's `ConfigToolRegistry`
  and its tests stay unchanged (additive, not breaking).
- Rationale (per user): keep M1 code untouched; the async surface lets a future DB-backed
  store breathe without a second refactor; the router is already async so the ripple is low
  risk.
- The Design phase will specify the exact `IAsyncToolRegistry` method signatures
  (`GetAllAsync(CancellationToken)` returning `Task<IReadOnlyList<ToolRegistration>>`,
  `GetAsync(string, CancellationToken)` returning `Task<ToolRegistration?>`) and where
  `ListVisibleTools` widens to async.

### 3. Tenant scoping model

- **Decision:** out of scope for M2 (deferred to M3). Without auth there is no identity, so
  tenant scoping is meaningless in M2.
- `TenantSettings` stays an empty stub. No `ITenantContext` is introduced.

### 4. CapabilityCatalog discovery

- **Decision:** auto-discover on register + a `POST /servers/{id}/resync` endpoint for
  re-pulling.
- When the admin registers a server, the Admin API calls `tools/list` on the downstream and
  stores one `CapabilityCatalog` entry per returned tool (server id, tool name, description,
  input schema, synced-at timestamp).
- If discovery fails, the server is still persisted with a `discovery-failed` state; no
  catalog entries are written. Re-sync replaces that server's catalog entries and updates the
  synced-at timestamp.
- Matches M1 `design.md:91-93` ("fetch schemas from the downstream server via the capability
  catalog").

### 5. Allowlist refresh window

- **Decision:** live read, no cache. The gateway reads from the same SQLite store the Admin
  API writes to on every `tools/list` call. Bounded refresh window = 0 seconds.
- Rationale (per user): simplest to reason about; matches the Notion M2 exit criterion
  ("Runtime gateway behavior uses registry data from the control plane or an equivalent
  shared source") literally. M2 assumes Admin API and gateway share one process, so live read
  is cheap.
- A later milestone can add caching/push without changing the store seam.

### 6. Backing store technology

- **Decision:** SQLite via EF Core or Dapper. The **Design phase** picks the ORM.
- Recommendation to be evaluated in Design: **EF Core** — gives a `DbContext` seam, schema
  migrations, and a natural query surface for the Admin API's CRUD. The cost is a couple of
  extra packages and a migration step on startup.
- Dapper is the lighter alternative: raw SQL, fewer packages, but migrations + mapping are
  hand-rolled. Design will present the tradeoff and lock one.
- Integration tests will use a temp-file SQLite DB (no Testcontainers needed for the store;
  Testcontainers stays for the downstream `McpGuard.SampleTools.Server` as in M1).

### 7. Downstream-unreachable path ownership

- **Decision:** M2 owns it.
- `DefaultToolRouter.RouteCallAsync` wraps `IMcpClientFactory.CreateAsync` and
  `IMcpDownstreamClient.CallToolAsync` in try/catch, emits a `tools.call.blocked` audit event
  with a `downstream-unreachable` reason, and returns
  `RouteResult(Allowed=false, Result=null, BlockReason="downstream-unreachable: <server id>")`.
- Closes the M1 eval category-9 `E_recall` miss (`STATE.md:100-104`).

### M1 follow-ups rolled into M2

- T-8 audit assertion: register `ISessionMigrationHandler` in `IntegrationTestFixture.cs`
  and assert `initialize:initialized` with an exact count in
  `Audit_emits_initialized_listed_allowed_and_blocked_events_in_order`.
- `-32602` vs `isError` reconciliation: canonicalize `isError` in the M1 spec text
  (`spec.md:22`, `design.md:232-250`); remove the `-32602` envelope clause for the tool-block
  path. Lower risk, matches the SDK contract.
- `Program.cs:39-44` duplicate `UseHttpsRedirection` + `MapMcp("/mcp")` cleanup.

### Agent's Discretion

- The Design phase chooses between EF Core and Dapper for the SQLite store.
- The Design phase chooses the exact `IAsyncToolRegistry` method shapes and the
  `DefaultToolRouter` async-widening edits (keeping behavior identical to M1).
- The Design phase chooses whether `StoreToolRegistry` and the Admin API share a `DbContext`
  or use a smaller shared store-abstraction interface (likely a single `DbContext` for M2).
- The Design phase chooses the exact `RouteResult`/audit-event `Reason` string format for the
  downstream-unreachable path (must not leak exception internals).

---

## Specific References

- M1 design intent for capability discovery: `.specs/features/m1-mvp-tool-gateway/design.md:91-93`
  — "fetch schemas from the downstream server via the capability catalog".
- M1 eval category-9 miss and the deferred downstream-unreachable path:
  `.specs/project/STATE.md:100-104`.
- M1 follow-ups to close: `.specs/project/STATE.md:89-106` (T-8 audit assertion, `-32602` vs
  `isError` delta, `Program.cs:39-44` cleanup).
- Current `IToolRegistry` contract (sync, to be preserved as-is):
  `src/runtime/McpGuard.ToolRegistry/IToolRegistry.cs:3-7`.
- `ConfigToolRegistry` snapshot-at-construction behavior to leave unchanged:
  `src/runtime/McpGuard.ToolRegistry/ConfigToolRegistry.cs:9-20`.
- Router downstream call sites with no try/catch today:
  `src/runtime/McpGuard.ToolRouter/DefaultToolRouter.cs:54-55`.
- Gateway DI swap point: `src/runtime/McpGuard.Gateway.Api/Program.cs:16`.
- Empty M2 stubs: `src/controlplane/McpGuard.Admin.Api/`,
  `src/controlplane/McpGuard.ServerRegistry/`, `src/controlplane/McpGuard.CapabilityCatalog/`,
  `src/controlplane/McpGuard.HealthChecks/`.
- Notion M2 milestone page:
  `https://app.notion.com/p/37e714784b4781d8b6d3fc09e3bcd377` (parent) — M2 section.

---

## Deferred Ideas

- Caching/push-based refresh of the store-backed registry (M2 does live read; a later milestone
  can add a push or TTL cache if the store grows slow).
- Multi-process pub/sub for store change notification (M2 assumes Admin API + gateway share
  one process; a real pub/sub lands when they split).
- Auth on the Admin API itself (lands in M3 alongside gateway auth).
- Uniqueness constraint on server downstream URL (M2 allows duplicates; a later milestone may
  enforce uniqueness if deduplication is needed).
- Persistent audit sink beyond M1's `LoggerAuditSink` (M6).