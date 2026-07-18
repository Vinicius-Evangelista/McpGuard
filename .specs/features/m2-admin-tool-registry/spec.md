# M2 — Admin-Controlled Tool Registry — Specification

Status: **draft** (2026-07-16). Specify phase output. Design + Tasks phases follow this file.
Source PRD: M2 milestone in `.specs/project/ROADMAP.md` (updated to the Notion split) and the
user's Notion "McpGuard Roadmap Backlog" → "M2: Admin-Controlled Tool Registry".
Predecessor: `.specs/features/m1-mvp-tool-gateway/` (complete, graded Spec-complete 2026-07-09).

## Problem Statement

M1 ships an MVP gateway, but the tool allowlist lives in a static `McpGuard:Tools` section of
`appsettings.json`, bound once at construction into `ConfigToolRegistry`. There is no way to
register downstream MCP servers, discover their capabilities, or toggle tool visibility
without rebuilding and restarting the gateway. Operators need explicit control-plane
capabilities to manage downstream servers and tool metadata, and the runtime needs to read
that data from a real store instead of static config.

## Goals

- [ ] Move downstream server registration and tool metadata out of static config into an
      Admin API backed by a persistent store.
- [ ] Discover tool capabilities from downstream servers automatically at registration time,
      with on-demand re-sync.
- [ ] Allow an operator to toggle tool visibility/enabled state at runtime without editing
      gateway code or restarting the gateway.
- [ ] Swap the runtime's tool-registry source from `ConfigToolRegistry` to a store-backed
      async registry without changing M1's stable `IToolRegistry` contract or M1 tests.
- [ ] Add downstream health checks so operators can see which registered servers are
      reachable.
- [ ] Own the downstream-unreachable failure path in `DefaultToolRouter` (M1 eval category-9
      miss) and close the open M1 follow-ups.

## Out of Scope

Explicitly excluded from M2. Documented to prevent scope creep.

| ID    | Non-goal                                                                                       | Deferred to |
| ----- | ---------------------------------------------------------------------------------------------- | ------------ |
| M2-N1 | No bearer/OIDC auth on `initialize`. No `IAuthenticator`, no JWT/OIDC packages.                | M3           |
| M2-N2 | No tenant, user, role, or scope context for gateway decisions. No `ITenantContext`.           | M3           |
| M2-N3 | No policy engine, no allow/deny/approval-required decision model. `PolicyStore` stays a stub.  | M3           |
| M2-N4 | No DLP context, no redaction before/after downstream call. `DlpContext`/`Redaction` stay empty. | M4           |
| M2-N5 | No secrets brokering. `SecretsProvider` stays a stub.                                          | M5           |
| M2-N6 | No OpenTelemetry traces, no persistent audit sink beyond M1's `LoggerAuditSink`. `Observability` stays a stub. | M6 |
| M2-N7 | No approval workflow for tools. `Approvals` stays a stub.                                      | M5           |
| M2-N8 | No multi-process pub/sub for store change notification. M2 assumes Admin API and gateway share one process. | Future |

---

## User Stories

### P1: Admin registers a downstream MCP server ⭐ MVP

**User Story**: As an operator, I want to register a downstream MCP server via the Admin API
so that the gateway knows it exists and can later serve its tools.

**Why P1**: Without a registered server there is nothing to discover, allow, or route to. This
is the root record every other M2 capability hangs off.

**Acceptance Criteria**:

1. WHEN an admin POSTs a valid server payload to the Admin API THEN the system SHALL persist a
   `ServerRegistry` record with an id, name, downstream URL, created timestamp, and enabled
   state.
2. WHEN an admin POSTs a server payload with a missing or malformed downstream URL THEN the
   Admin API SHALL return a 400 with a validation error naming the invalid field.
3. WHEN an admin GETs the servers endpoint THEN the Admin API SHALL return all registered
   servers with their current enabled state.
4. WHEN an admin GETs a single server by id THEN the Admin API SHALL return that server or 404
   if the id does not exist.
5. WHEN an admin PUTs an existing server by id THEN the Admin API SHALL update the mutable
   fields (name, downstream URL, enabled) and persist the change.
6. WHEN an admin DELETEs a server by id THEN the Admin API SHALL remove the server record and
   return 204, or 404 if the id does not exist.

**Independent Test**: Register a server via the Admin API, then GET it and observe the stored
record. Repeat with an invalid payload and observe a 400.

---

### P1: Capability auto-discovery on register ⭐ MVP

**User Story**: As an operator, I want the Admin API to call `tools/list` on a downstream
server when I register it, so that tool metadata is captured without manual entry.

**Why P1**: Manual schema entry drifts from the real downstream; auto-discovery keeps the
catalog accurate and matches the M1 design intent (`design.md:91-93`).

**Acceptance Criteria**:

7. WHEN a server is successfully registered THEN the Admin API SHALL call `tools/list` on the
   downstream URL and persist one `CapabilityCatalog` entry per returned tool, each carrying
   server id, tool name, description, input schema, and a synced-at timestamp.
8. WHEN `tools/list` on a newly registered server fails (unreachable, non-2xx, or invalid
   JSON-RPC) THEN the Admin API SHALL still persist the `ServerRegistry` record, SHALL mark
   the server with a `discovery-failed` state, SHALL return a 201 with a warning, and SHALL NOT
   persist any `CapabilityCatalog` entries for that server.
9. WHEN the admin later triggers re-sync for a server (POST `/servers/{id}/resync`) THEN the
   Admin API SHALL re-call `tools/list`, replace that server's `CapabilityCatalog` entries
   with the latest results, and update the synced-at timestamp.
10. WHEN re-sync is triggered for a server id that does not exist THEN the Admin API SHALL
    return 404.

**Independent Test**: Register a server running the `McpGuard.SampleTools.Server` (echo, add),
GET the capability catalog for that server, and observe two tool entries with schemas.

---

### P1: Allowlist and denylist management ⭐ MVP

**User Story**: As an operator, I want to toggle a tool's `Allowed` and `Visible` state via
the Admin API so that I can control which tools clients see and call without editing runtime
code.

**Why P1**: This is the core governance loop M1 deferred — visibility changes must take effect
without a gateway restart.

**Acceptance Criteria**:

11. WHEN an admin PATCHes a capability catalog entry's `Allowed` flag THEN the Admin API SHALL
    persist the change and the gateway's next `tools/list` SHALL reflect it without a restart.
12. WHEN an admin PATCHes a capability catalog entry's `Visible` flag THEN the Admin API SHALL
    persist the change and the gateway's next `tools/list` SHALL reflect it without a restart.
13. WHEN a tool is marked `Allowed=false` THEN a subsequent `tools/call` for that tool through
    the gateway SHALL be blocked with the same JSON-RPC error shape as M1-R6.
14. WHEN a tool is marked `Visible=false` THEN it SHALL NOT appear in `tools/list` AND a
    `tools/call` for it SHALL be blocked.
15. WHEN an admin PATCHes a capability catalog entry that does not exist THEN the Admin API
    SHALL return 404.

**Independent Test**: Register + discover a server, PATCH one tool to `Visible=false`, call
`tools/list` through the gateway, observe the tool is gone; PATCH it back to `Visible=true`,
observe it returns.

---

### P1: Store-backed async registry feeds the gateway ⭐ MVP

**User Story**: As a developer, I want the gateway to read tool metadata from the control-
plane store on every request, so that allowlist changes are visible immediately.

**Why P1**: This is the runtime half of the control-plane work. Without it, the Admin API
mutates a store the gateway never reads.

**Acceptance Criteria**:

16. The system SHALL introduce a new `IAsyncToolRegistry` interface (async `GetAllAsync` +
    `GetAsync`) alongside M1's stable sync `IToolRegistry`. M1's `ConfigToolRegistry` and
    M1's `IToolRegistry` tests SHALL remain unchanged.
17. A store-backed `StoreToolRegistry` SHALL implement `IAsyncToolRegistry` and read from the
    same SQLite store the Admin API writes to, on every call (no in-memory cache).
18. `DefaultToolRouter` SHALL depend on `IAsyncToolRegistry` instead of `IToolRegistry`. Its
    visible-tools filter and call-routing logic SHALL be behavior-preserving relative to M1.
19. The gateway's `Program.cs` SHALL register `StoreToolRegistry` as the `IAsyncToolRegistry`
    implementation. `ConfigToolRegistry` SHALL remain registered as the M1 `IToolRegistry`
    (kept for compatibility, unused by the router in M2).
20. WHEN the Admin API mutates the store THEN the gateway's next `tools/list` SHALL reflect the
    change with no restart and no explicit refresh signal (live read, 0-second window).

**Independent Test**: Register a server + discover its tools via the Admin API, then call
`tools/list` through the gateway and observe the discovered tools; PATCH one to `Visible=false`
via the Admin API, call `tools/list` again, observe it gone — no restart in between.

---

### P1: Downstream health checks ⭐ MVP

**User Story**: As an operator, I want to see which registered downstream servers are
reachable, so that I can troubleshoot routing failures.

**Why P1**: The Notion M2 exit criteria include health checks for registered downstream MCP
servers, and the downstream-unreachable path (next story) needs a reachability signal to emit
clear audit reasons.

**Acceptance Criteria**:

21. The `McpGuard.HealthChecks` project SHALL expose a health check that probes each enabled
    registered server's downstream URL and reports reachability.
22. The Admin API SHALL expose `GET /health` returning an aggregate health status plus
    per-server reachability, with a 200 when all enabled servers are reachable and a 503 when
    any enabled server is unreachable.
23. WHEN a server is disabled (`enabled=false`) THEN the health check SHALL skip it and SHALL
    NOT report it as unhealthy.

**Independent Test**: Register two servers (one reachable, one at a dead URL), GET `/health`,
observe a 503 with the dead server marked unreachable and the reachable one marked healthy.

---

### P1: Downstream-unreachable failure handling ⭐ MVP

**User Story**: As a client, when I call an approved tool whose downstream server is
unreachable, I want a clear error rather than an opaque crash, and I want the failure audited.

**Why P1**: Closes the M1 eval category-9 `E_recall` miss (`STATE.md:100-104`); M2 already
reworks the registry feeding the router, so the try/catch belongs here.

**Acceptance Criteria**:

24. WHEN `DefaultToolRouter.RouteCallAsync` catches an exception from
    `IMcpClientFactory.CreateAsync` or `IMcpDownstreamClient.CallToolAsync` (connection
    refused, timeout, DNS failure, non-2xx) THEN it SHALL emit an `AuditEvent` with
    `Outcome = "tools.call.blocked"` and a `Reason` identifying the failure as
    downstream-unreachable (without leaking exception internals).
25. WHEN the router catches a downstream-unreachable exception THEN it SHALL return
    `RouteResult(Allowed=false, Result=null, BlockReason="downstream-unreachable: <server id>")`
    and SHALL NOT re-throw.
26. The gateway SHALL surface the downstream-unreachable failure to the client in the same
    `CallToolResult { IsError = true }` shape used by M1-R6 (the canonicalized `isError` form,
    see M1 follow-up below), not as an unhandled exception.

**Independent Test**: Point a tool at a dead downstream URL via the Admin API, call the tool
through the gateway, observe an `isError` result and a `tools.call.blocked` audit line with a
`downstream-unreachable` reason.

---

### P2: Admin API lists and inspects the capability catalog

**User Story**: As an operator, I want to list all discovered tools and filter by server, so
that I can review the catalog before toggling visibility.

**Why P2**: Useful for operations but not on the critical path of the P1 governance loop.

**Acceptance Criteria**:

27. WHEN an admin GETs `/servers/{id}/capabilities` THEN the Admin API SHALL return all
    `CapabilityCatalog` entries for that server.
28. WHEN an admin GETs `/capabilities` THEN the Admin API SHALL return all catalog entries
    across all servers.
29. WHEN an admin GETs `/capabilities/{id}` THEN the Admin API SHALL return that catalog entry
    or 404.

**Independent Test**: Register + discover a server, GET `/capabilities`, observe the two
tools; GET `/capabilities/{id}` for one of them, observe its schema.

---

### P2: M1 follow-ups closed

**User Story**: As a developer, I want the open M1 follow-ups closed so that M1's grade lifts
to 1.00 and the spec matches the implementation.

**Why P2**: Not M2-functional, but rolled into M2 per the kickoff decision; cheap to do while
the gateway is open for the registry swap.

**Acceptance Criteria**:

30. The integration test `IntegrationTestFixture.cs` SHALL register
    `ISessionMigrationHandler` (mirroring `Program.cs:20`), and the
    `Audit_emits_initialized_listed_allowed_and_blocked_events_in_order` test SHALL assert the
    `initialize:initialized` audit event with an exact count (no `>= 4` tolerance). Lifts M1-R7
    `T` to 1.00.
31. `spec.md:22` and `design.md:232-250` of the M1 spec SHALL be reconciled with the
    implemented `CallToolResult { IsError = true }` shape by canonicalizing `isError` in the
    spec text (lower risk, matches the SDK contract). The `-32602` envelope clause is removed.
32. The duplicate `UseHttpsRedirection` + `MapMcp("/mcp")` block in `Program.cs:39-44` SHALL
    be removed.

**Independent Test**: `dotnet test tests/McpGuard.Gateway.Api.Tests` passes with the tightened
audit assertion; M1 spec text no longer mentions `-32602` for the tool-block path.

---

## Edge Cases

- WHEN two servers are registered with the same downstream URL THEN the Admin API SHALL
  accept both (no uniqueness constraint on URL) and the gateway SHALL route to the server id
  recorded on the capability entry.
- WHEN a server is deleted while a `tools/call` is in flight to one of its tools THEN the
  router SHALL still complete the in-flight call (best-effort) and subsequent calls SHALL be
  blocked as unknown-tool.
- WHEN `tools/list` on a downstream returns zero tools THEN the Admin API SHALL persist the
  server with an empty capability set and a `discovery-ok` state.
- WHEN the SQLite store file does not exist on startup THEN the system SHALL create it and
  apply migrations.
- WHEN the store is locked or unreadable at gateway startup THEN the gateway SHALL fail fast
  with a clear error.

---

## Requirement Traceability

| Requirement ID | Story                                         | Phase  | Status  |
| -------------- | --------------------------------------------- | ------ | ------- |
| M2-R1          | P1: Admin registers a downstream MCP server   | Design | Pending |
| M2-R2          | P1: Admin registers a downstream MCP server   | Design | Pending |
| M2-R3          | P1: Admin registers a downstream MCP server    | Design | Pending |
| M2-R4          | P1: Admin registers a downstream MCP server    | Design | Pending |
| M2-R5          | P1: Admin registers a downstream MCP server    | Design | Pending |
| M2-R6          | P1: Admin registers a downstream MCP server    | Design | Pending |
| M2-R7          | P1: Capability auto-discovery on register      | Design | Pending |
| M2-R8          | P1: Capability auto-discovery on register      | Design | Pending |
| M2-R9          | P1: Capability auto-discovery on register      | Design | Pending |
| M2-R10         | P1: Capability auto-discovery on register      | Design | Pending |
| M2-R11         | P1: Allowlist and denylist management          | Design | Pending |
| M2-R12         | P1: Allowlist and denylist management          | Design | Pending |
| M2-R13         | P1: Allowlist and denylist management          | Design | Pending |
| M2-R14         | P1: Allowlist and denylist management          | Design | Pending |
| M2-R15         | P1: Allowlist and denylist management          | Design | Pending |
| M2-R16         | P1: Store-backed async registry feeds gateway  | Design | Pending |
| M2-R17         | P1: Store-backed async registry feeds gateway  | Design | Pending |
| M2-R18         | P1: Store-backed async registry feeds gateway  | Design | Pending |
| M2-R19         | P1: Store-backed async registry feeds gateway  | Design | Pending |
| M2-R20         | P1: Store-backed async registry feeds gateway  | Design | Pending |
| M2-R21         | P1: Downstream health checks                   | Design | Pending |
| M2-R22         | P1: Downstream health checks                   | Design | Pending |
| M2-R23         | P1: Downstream health checks                   | Design | Pending |
| M2-R24         | P1: Downstream-unreachable failure handling    | Design | Pending |
| M2-R25         | P1: Downstream-unreachable failure handling    | Design | Pending |
| M2-R26         | P1: Downstream-unreachable failure handling    | Design | Pending |
| M2-R27         | P2: Admin API lists and inspects the catalog   | -      | Pending |
| M2-R28         | P2: Admin API lists and inspects the catalog   | -      | Pending |
| M2-R29         | P2: Admin API lists and inspects the catalog   | -      | Pending |
| M2-R30         | P2: M1 follow-ups closed                       | -      | Pending |
| M2-R31         | P2: M1 follow-ups closed                       | -      | Pending |
| M2-R32         | P2: M1 follow-ups closed                       | -      | Pending |

**ID format:** `M2-Rn` (mirrors M1's `M1-Rn`).

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 32 total requirements; 26 mapped to P1 stories (Design phase), 6 mapped to P2
stories. All 32 will map to atomic tasks in `tasks.md`.

---

## Acceptance

M2 is done when:
- All exit criteria from the Notion M2 milestone are demonstrated green:
  - An admin can register and inspect MCP servers.
  - Tool metadata can be discovered or maintained centrally.
  - Tool visibility can be changed without editing runtime code.
  - Runtime gateway behavior uses registry data from the control-plane store.
- All M2-R1…M2-R32 acceptance criteria pass via the M2 test suite (unit + integration).
- `dotnet build McpGuard.slnx` succeeds (all projects).
- `dotnet test McpGuard.slnx` passes (unit tests always; integration tests with Docker).
- A `spec-driven-eval` run against this spec grades every M2-R requirement as fulfilled.

## Success Criteria

- [ ] An operator can register a downstream server, discover its tools, and toggle tool
      visibility via the Admin API, all without touching `appsettings.json` or restarting the
      gateway.
- [ ] The gateway's `tools/list` reflects Admin API allowlist changes on the next call (live
      read, 0-second window).
- [ ] A `tools/call` to a tool on an unreachable downstream server returns a clear `isError`
      result and emits a `tools.call.blocked` audit event with a `downstream-unreachable`
      reason — no unhandled exceptions.
- [ ] M1's open follow-ups (T-8 audit assertion, `-32602` vs `isError` spec reconciliation,
      `Program.cs` duplicate cleanup) are closed; M1 grade lifts to 1.00.
- [ ] M1's `IToolRegistry`/`ConfigToolRegistry` and their tests remain unchanged — the new
      `IAsyncToolRegistry` is additive.