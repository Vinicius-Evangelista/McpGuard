# McpGuard — Persistent State

Memory across sessions: decisions made, blockers hit, lessons learned, deferred ideas, and
preferences. Append-only per section; date each entry.

## Decisions

### 2026-07-16 — M2 kickoff

- **Milestone split realigned to Notion.** M2 is "Admin-Controlled Tool Registry" only, not
  "Auth & Admin-managed registry" as the earlier local `ROADMAP.md` draft had it. OIDC bearer
  auth, `IAuthenticator`, `ITenantContext`, tenant scoping, `PolicyStore`, and `TenantSettings`
  are deferred to **M3 (Policy + Identity)**. The local `ROADMAP.md` was rewritten to match
  the Notion M0–M4 split (M2 admin-registry; M3 policy+identity; M4 DLP+redaction+audit+
  observability; M5 secrets+approvals; M6 observability+health).
- **No auth on the Admin API in M2.** The Admin API is an open admin surface for the M2
  iteration loop; network/policy protection is the operator's responsibility until M3 adds auth
  to both the gateway and the Admin API.
- **Registry interface:** introduce a new `IAsyncToolRegistry` (async `GetAllAsync`/`GetAsync`)
  alongside M1's stable sync `IToolRegistry`. M1's `ConfigToolRegistry` and tests stay
  unchanged (additive). The store-backed `StoreToolRegistry` implements `IAsyncToolRegistry`;
  `DefaultToolRouter` switches to depend on the async interface.
- **CapabilityCatalog discovery:** auto-discover via `tools/list` on server register, plus a
  `POST /servers/{id}/resync` endpoint for re-pulling. Store one `CapabilityCatalog` entry per
  returned tool (server id, name, description, input schema, synced-at). On discovery failure,
  persist the server with a `discovery-failed` state and write no catalog entries. Matches
  M1 `design.md:91-93` intent.
- **Allowlist refresh:** live read, no cache. The gateway reads from the same SQLite store the
  Admin API writes to on every `tools/list` call. Bounded refresh window = 0 seconds. M2
  assumes Admin API + gateway share one process.
- **Backing store:** SQLite via EF Core or Dapper — **Design phase picks the ORM**
  (recommendation will be evaluated in Design; EF Core favored for migrations + `DbContext`
  seam). Integration tests use a temp-file SQLite DB (no Testcontainers for the store; the M1
  Testcontainers setup stays for the downstream `McpGuard.SampleTools.Server`).
- **Downstream-unreachable path owned by M2.** `DefaultToolRouter.RouteCallAsync` wraps
  `IMcpClientFactory.CreateAsync` + `IMcpDownstreamClient.CallToolAsync` in try/catch, emits
  a `tools.call.blocked` audit event with a `downstream-unreachable` reason, returns
  `RouteResult(false, null, reason)`. Closes the M1 eval category-9 `E_recall` miss
  (`STATE.md:100-104`).
- **M1 follow-ups rolled into M2:** (a) T-8 audit assertion — register
  `ISessionMigrationHandler` in `IntegrationTestFixture.cs` and assert
  `initialize:initialized` with an exact count in
  `Audit_emits_initialized_listed_allowed_and_blocked_events_in_order`; (b) `-32602` vs
  `isError` reconciliation — canonicalize `isError` in the M1 spec text
  (`spec.md:22`, `design.md:232-250`), remove the `-32602` envelope clause for the tool-block
  path (lower risk, matches the SDK contract); (c) `Program.cs:39-44` duplicate
  `UseHttpsRedirection` + `MapMcp("/mcp")` cleanup.

### 2026-07-03 — M1 kickoff

- **MCP SDK:** Use the official `ModelContextProtocol` C# SDK
  (`ModelContextProtocol.AspNetCore` for the client-facing server, `ModelContextProtocol`
  client for downstream calls). Maintained in collaboration with Microsoft; current latest
  is 1.4.x. Do not hand-roll JSON-RPC framing.
- **Allowlist source for M1:** Static `McpGuard:Tools` section in `appsettings.json`, bound
  via `IOptions<ToolRegistryOptions>` and surfaced through `IToolRegistry`. M2 swaps the
  implementation for a control-plane store; the interface is stable from day one.
- **Auth scope for M1:** Deferred. M1 accepts `initialize` without token validation. An
  `IAuthenticator` no-op stub may be inserted only if it keeps the pipeline shape intact
  without adding behavior. Real OIDC lands in M2.
- **DLP / Redaction / Secrets:** Stay empty stubs for M1. Their projects are not referenced
  by the gateway in M1. No `IDlpContextProvider`, `IRedactionService`, or `ISecretsBroker`
  interfaces are introduced in M1 — adding no-op stubs would add interfaces with no behavior.
- **Audit output for M1:** Structured JSON lines to `ILogger` via `LoggerAuditSink`. One JSON
  object per event. Easy to grep, easy to swap for a persistent sink in M5.
- **Downstream provenance:** A new `McpGuard.SampleTools.Server` console project exposes
  `echo` and `add` over `/mcp` and is containerized via a `Dockerfile`. Integration tests
  spin it up via Testcontainers and point the gateway at the container's mapped port.
- **Testing conventions (binding for all milestones):**
  - No mocking libraries (no Moq, no NSubstitute). Use hand-written **fakes** + **Object
    Mother** pattern.
  - Test names: simplest descriptive sentence, first letter uppercase, snake*case, no
    `Should*`/`Returns`verbs. Examples:`Initialize_negotiates_protocol_and_returns_session_id`,
`Tools_list_returns_only_approved_tools`,
`Route_call_on_invisible_tool_never_invokes_downstream`.
  - Unit tests are in-process, no network, no Docker.
  - Integration tests use **Testcontainers** to run the real sample downstream server as a
    container, plus `WebApplicationFactory<Program>` to host the gateway in-process.
  - AGENTS.md's prior `Method_State_ExpectedOutcome` PascalCase convention is superseded for
    this project by the snake_case rule above. Update AGENTS.md when code lands.

## Blockers

_(none yet)_

## Lessons

_(none yet)_

## Deferred ideas

- **In-memory audit ring buffer + HTTP read endpoint.** Considered for M1, rejected in favor
  of the logger sink. Reconsider for M5 alongside persistent audit.
- **Pass-through `IDlpContextProvider` / `IRedactionService` / `ISecretsBroker` no-op stubs
  in the M1 pipeline.** Rejected: adds interfaces with no behavior and widens M1 scope
  without serving the exit criteria. They appear in M3/M4.
- **Minimal bearer shared-secret auth for M1.** Rejected: M1 themes are tool-governance only.
  Real OIDC in M2 is a better use of effort than a throwaway static-secret check.

## Preferences

- Heavier reasoning tasks (brownfield mapping, design, spec writing) suit the lead model;
  lightweight tasks (state updates, validation reports) work fine on faster/cheaper models.
  Track here to avoid repeating the tip. _(recorded 2026-07-03)_

## Todos

- [x] When M1 code lands, update `AGENTS.md` testing section to match the snake_case +
      no-mocks + Testcontainers conventions recorded under Decisions. — **Done 2026-07-03.**
- [x] Fix the solution-level `dotnet build McpGuard.slnx` failure (currently documented
      in AGENTS.md as failing during restore with no diagnostics) or update AGENTS.md to
      reflect the resolved state. — **Done 2026-07-03.** `dotnet build McpGuard.slnx` now
      succeeds (25 projects, 0 errors).
- [x] Run integration tests (Task 6) when Docker is available — tests build but require
      Docker to execute. — **Done 2026-07-03.** All 7 integration tests pass with Docker
      (Docker Engine installed in WSL2 Ubuntu). Required Testcontainers 4.x API fix
      (`WithDockerfileDirectory` + `WithContextDirectory`), MCP SDK stateless mode for
      test compatibility, and real Kestrel server (not `WebApplicationFactory`) for SSE
      response handling.
- [x] Run spec-driven-eval against M1 spec to grade completion. — **Done 2026-07-09.**
      Final **0.9952 (Spec-complete, band ≥ 0.90)**. Report:
      `.specs/features/m1-mvp-tool-gateway/evaluations/p0-m1-mvp-tool-gateway-20260709T004017Z.md`.
      Frozen AC baseline:
      `.specs/features/m1-mvp-tool-gateway/evaluations/_ac-baseline.md`. Single UNMET
      check: M1-R7 T-8 (e2e does not assert the `initialize:initialized` audit event, and
      the `IntegrationTestFixture` does not register `ISessionMigrationHandler` that emits
      it). One design-vs-impl delta: M1-R6 specifies a raw `-32602` JSON-RPC envelope, but
      the implementation uses `CallToolResult { IsError = true }` (the SDK's tool-call
      error contract). 60/60 I-checks MET; 31/32 T-checks MET.
- [ ] **M1 follow-up (closes the only UNMET test check):** register
      `ISessionMigrationHandler` in `IntegrationTestFixture.cs` (mirror `Program.cs:20`)
      and assert `initialize:initialized` in
      `Audit_emits_initialized_listed_allowed_and_blocked_events_in_order`; tighten the
      `>= 4` tolerance to an exact count or assert all 4 outcomes. Lifts M1-R7 `T` to 1.00
      and `Final` to 1.00. _(recorded 2026-07-09 from the eval; rolled into M2 — see M2-R30)_
- [ ] **M1 follow-up (design-vs-impl delta):** reconcile the `-32602` envelope clause in
      `spec.md:22` + `design.md:232-250` with the implemented `isError` shape — either
      canonicalize `isError` in the spec (lower risk, matches the SDK contract) or throw
      a `-32602` JSON-RPC error from `McpGatewayHandler.CallToolAsync`. _(recorded
      2026-07-09 from the eval; rolled into M2 — canonicalize `isError`, see M2-R31)_
- [x] **Deferred (not M1, follow-up for M2/M3):** address the downstream-unreachable
      failure path in `DefaultToolRouter.RouteCallAsync` — catch SDK exceptions, emit a
      `tools.call.blocked` audit event with a downstream-unreachable reason, return a
      clear error. `E_recall` category-9 miss surfaced by the eval. _(recorded
      2026-07-09; owned by M2 — see M2-R24..R26)_
- [ ] Cosmetic: remove the duplicate `UseHttpsRedirection` + `MapMcp("/mcp")` block in
      `Program.cs:39-44`. _(recorded 2026-07-09 from the eval; rolled into M2 — see M2-R32)_
- [x] Start M2 spec (Specify phase) under `.specs/features/m2-.../` — OIDC bearer auth
      on `initialize`, Admin API CRUD for ServerRegistry/CapabilityCatalog/PolicyStore,
      store-backed `IToolRegistry`, per-tenant allowlist scoping. M1 is graded
      Spec-complete; M2 can begin once the M1-R7 T-8 follow-up is closed. _(recorded
      2026-07-09) — **Done 2026-07-16.** Scope realigned to Notion: M2 = Admin-Controlled
      Tool Registry only (auth/tenant deferred to M3). Spec + context written at
      `.specs/features/m2-admin-tool-registry/`. `_`

## Lessons

- **2026-07-03** — The official MCP C# SDK (v1.4.0) uses `MapMcp("/mcp")` (not
  `MapMcpHttpTransport`) for the Streamable HTTP endpoint, and `AddMcpServer()` +
  `WithHttpTransport()` for server registration. Handler registration is via
  `WithListToolsHandler()` and `WithCallToolHandler()`. The `initialize` flow is handled
  entirely by the SDK — no custom handler needed.
- **2026-07-03** — `ILogger<T>` vs `ILogger`: class libraries that use `ILogger<T>` need
  `Microsoft.Extensions.Logging.Abstractions`. Test projects that create `ILoggerFactory`
  need `Microsoft.Extensions.Logging`. Central package management means adding the version
  once in `Directory.Packages.props` and referencing it (versionless) from csproj files.
- **2026-07-03** — The MCP SDK's `McpClient.CreateAsync` uses `HttpClientTransport` (not
  `SseClientTransport` or `StreamableHttpClientTransport` — those names don't exist in v1.4.0).
  The constructor takes `HttpClientTransportOptions` with an `Endpoint` property of type `Uri`.
- **2026-07-03** — Top-level `Program.cs` generates an internal `Program` class. Tests using
  `WebApplicationFactory<Program>` need a `public partial class Program {}` in a separate file.
- **2026-07-03** — `WebApplicationFactory.CreateClient()` uses an in-memory `TestServer` handler
  that cannot handle MCP's SSE streaming (the SDK keeps the stream open for notifications,
  causing `ReadAsStringAsync()` to hang). Solution: use a real `WebApplication` with Kestrel on
  a real port (`http://127.0.0.1:5099`) and raw `HttpClient` JSON-RPC calls instead.
- **2026-07-03** — MCP SDK `McpClient.CreateAsync` has the same SSE deadlock issue in tests.
  Use raw JSON-RPC over `HttpClient` for test assertions. Parse SSE format:
  `event: message\ndata: {...json...}\n\n`.
- **2026-07-03** — Testcontainers 4.x API changes: `IContainer` → `IContainer`, but
  `FutureDockerImage` now requires `CreateAsync()` before `StartAsync()`. Dockerfile path
  resolution uses `WithContextDirectory(repoRoot)` + `WithDockerfileDirectory(".")`.
- **2026-07-03** — MCP SDK `CallToolResult.IsError` is optional — when a tool succeeds, the
  `isError` property may be absent from the JSON. Test assertions must use
  `TryGetProperty("isError", ...)` instead of `GetProperty("isError")`.
- **2026-07-03** — `ToolRegistryOptions.Tools` is `init`-only. Integration test fixture uses
  `TestToolRegistry` directly instead of `Configure<ToolRegistryOptions>`.

## Session log

- **2026-07-16** — M2 Specify phase. Realigned milestone split to Notion (M2 admin-registry
  only; auth/tenant to M3). Ran the tlc-spec-driven Specify → discuss-gray-areas flow with 7
  locked decisions. Wrote `.specs/features/m2-admin-tool-registry/spec.md` (32 requirements
  M2-R1…M2-R32) + `context.md`. Rewrote `.specs/project/ROADMAP.md` to the Notion M0–M6 split.
  Next: Design phase (component map, DI wiring deltas, EF Core vs Dapper decision, sequence
  diagrams for register+discover and live-read tools/list), then Tasks.
- **2026-07-03** — M1 kickoff. Explored scaffold (20 empty projects, 4 architecture
  diagrams, no code, no refs, no packages). Ran the tlc-spec-driven Specify → Discuss →
  Design → Tasks flow. Captured 6 gray-area decisions via user discussion. Wrote
  `.specs/project/`, `.specs/codebase/`, `.specs/features/m1-mvp-tool-gateway/`.
- **2026-07-03** — M1 implementation. Executed tasks 1–7. All builds pass. Unit tests: 5/5
  ToolRegistry, 4/4 Audit, 8/8 ToolRouter = 17 total. Integration tests build but require
  Docker to execute. Updated README, STATE.
