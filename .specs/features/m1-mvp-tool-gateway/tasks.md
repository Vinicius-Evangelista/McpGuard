# M1 — MVP Tool Gateway — Tasks

Ordered. `[P]` = parallelizable with siblings. Each task lists What, Where, Depends on,
Reuses, Done when, Tests, and Gate. Sub-agents implement one task each and report back.

Legend: status `[ ]` pending, `[~]` in progress, `[x]` done, `[!]` blocked.

---

## Task 1 — Wire packages + project references + sample server scaffold

**Status:** `[x]`

**What:**
- Add package versions to `Directory.Packages.props`: `ModelContextProtocol`,
  `ModelContextProtocol.AspNetCore`, `xunit`, `xunit.runner.visualstudio`,
  `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers`.
- Add `<ProjectReference>` wiring:
  - `McpGuard.Gateway.Api` → `McpGuard.ToolRegistry`, `McpGuard.ToolRouter`, `McpGuard.Audit`
  - `McpGuard.ToolRouter` → `McpGuard.ToolRegistry`, `McpGuard.Audit`
- Create the `McpGuard.SampleTools.Server` console project under
  `src/runtime/McpGuard.SampleTools.Server/` (`Microsoft.NET.Sdk.Web`, `net10.0`), add it to
  `McpGuard.slnx` under `/runtime/`, add a multi-stage `Dockerfile` at the project root
  (build → runtime on `8080/mcp`). Give it a minimal `Program.cs` that starts Kestrel on
  `8080` with no endpoints yet (the SDK handler wiring is task 5).
- Create the four test projects under `tests/` (`McpGuard.ToolRegistry.Tests`,
  `McpGuard.ToolRouter.Tests`, `McpGuard.Audit.Tests`, `McpGuard.Gateway.Api.Tests`), each
  `net10.0` with `xunit` + `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`, and the
  right `<ProjectReference>` to the runtime project under test. Add all four to
  `McpGuard.slnx` under a new `/tests/` folder. The integration test project also references
  `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers`.
- Delete `Class1.cs` from `McpGuard.ToolRegistry`, `McpGuard.ToolRouter`, `McpGuard.Audit`.
  Leave `Class1.cs` in the other stub projects untouched.

**Where:** repo root (`Directory.Packages.props`, `McpGuard.slnx`),
`src/runtime/McpGuard.SampleTools.Server/`, `src/runtime/McpGuard.Gateway.Api/`,
`src/runtime/McpGuard.ToolRouter/`, `src/runtime/McpGuard.ToolRegistry/`,
`src/runtime/McpGuard.Audit/`, `tests/`.

**Depends on:** nothing (first task).

**Reuses:** `.specs/codebase/CONVENTIONS.md` (package + reference rules),
`.specs/codebase/TESTING.md` (test project layout).

**Done when:**
- `dotnet build src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj` exits 0.
- `dotnet build src/runtime/McpGuard.SampleTools.Server/McpGuard.SampleTools.Server.csproj`
  exits 0.
- `dotnet build` on each of the four test csproj exits 0 (no tests yet, just builds).
- `docker build -t mcpguard-sample-tools -f
  src/runtime/McpGuard.SampleTools.Server/Dockerfile
  src/runtime/McpGuard.SampleTools.Server` exits 0 (image builds).

**Tests:** none yet (scaffolding).

**Gate:** all four `dotnet build` commands + the `docker build` exit 0. Sub-agent reports
each command + exit code.

---

## Task 2 — `[P]` ToolRegistry: model + ConfigToolRegistry + options

**Status:** `[x]`

**What:** Implement the `McpGuard.ToolRegistry` project per `design.md`:
- `ToolRegistration` record.
- `IToolRegistry` interface.
- `ToolRegistryOptions` + `ToolEntry` (binds `McpGuard:Tools`).
- `ConfigToolRegistry : IToolRegistry` (ctor takes `IOptions<ToolRegistryOptions>`; maps
  `ToolEntry` → `ToolRegistration`; `GetAll` returns the list; `Get` returns the match or
  null).
- Unit tests in `tests/McpGuard.ToolRegistry.Tests/` using `ObjectMother` for fixtures and a
  real `IOptions<ToolRegistryOptions>` built from `Options.Create(ObjectMother.ToolRegistryOptions())`.

**Tests (snake_case names, no mocks):**
- `Config_tool_registry_returns_only_configured_tools`
- `Config_tool_registry_returns_null_for_unknown_tool`
- `Config_tool_registry_preserves_allowed_and_visible_flags`
- `Config_tool_registry_maps_downstream_url`

**Where:** `src/runtime/McpGuard.ToolRegistry/`, `tests/McpGuard.ToolRegistry.Tests/`.

**Depends on:** Task 1 (projects + package refs exist).

**Reuses:** `design.md` type signatures, `TESTING.md` naming + Object Mother rules.

**Done when:** `dotnet test tests/McpGuard.ToolRegistry.Tests/McpGuard.ToolRegistry.Tests.csproj`
exits 0 with all four tests passing.

**Gate:** `dotnet test` exit 0; sub-agent reports test count + result.

---

## Task 3 — `[P]` Audit: IAuditSink + LoggerAuditSink + AuditEvent

**Status:** `[x]`

**What:** Implement the `McpGuard.Audit` project per `design.md`:
- `AuditEvent` record (Timestamp, SessionId, Method, ToolName, Outcome, Reason).
- `IAuditSink` interface.
- `LoggerAuditSink : IAuditSink` (ctor takes `ILogger<LoggerAuditSink>`; serializes the event
  to a single JSON line via `System.Text.Json` with `JsonSerializerOptions { PropertyNamingPolicy
  = JsonNamingPolicy.CamelCase }` and writes at `LogLevel.Information`).
- Unit tests in `tests/McpGuard.Audit.Tests/` capturing logger output. Use a hand-written
  `CapturingLoggerProvider` fake (no Moq) that records formatted log messages, plus
  `ObjectMother.AuditEvent(...)` factories for each outcome.

**Tests:**
- `Logger_audit_sink_writes_one_json_line_per_event`
- `Logger_audit_sink_serializes_all_fields_in_camel_case`
- `Logger_audit_sink_includes_reason_when_blocked`
- `Logger_audit_sink_omits_reason_when_not_blocked` (verify the field is null in JSON)

**Where:** `src/runtime/McpGuard.Audit/`, `tests/McpGuard.Audit.Tests/`.

**Depends on:** Task 1.

**Reuses:** `design.md` type signatures, `TESTING.md` naming + fakes rules.

**Done when:** `dotnet test tests/McpGuard.Audit.Tests/McpGuard.Audit.Tests.csproj` exits 0
with all four tests passing.

**Gate:** `dotnet test` exit 0; sub-agent reports test count + result.

---

## Task 4 — ToolRouter: IToolRouter + DefaultToolRouter + IMcpClientFactory

**Status:** `[x]`

**What:** Implement the `McpGuard.ToolRouter` project per `design.md`:
- `RouteResult` record.
- `IToolRouter` interface.
- `IMcpClientFactory` + `IMcpDownstreamClient` interfaces.
- `DefaultToolRouter : IToolRouter` (ctor takes `IToolRegistry`, `IAuditSink`,
  `IMcpClientFactory`; `ListVisibleTools` filters `Allowed && Visible`; `RouteCallAsync`
  guards → audits → routes → audits → returns).
- Unit tests in `tests/McpGuard.ToolRouter.Tests/` using `FakeToolRegistry`,
  `FakeAuditSink`, `FakeMcpClientFactory`, and `ObjectMother` for tool registrations +
  call arguments.

**Tests:**
- `List_visible_tools_hides_disallowed_and_invisible`
- `Route_call_on_allowed_tool_invokes_downstream_and_returns_result`
- `Route_call_on_allowed_tool_emits_allowed_audit_event`
- `Route_call_on_disallowed_tool_returns_blocked_and_never_invokes_downstream`
- `Route_call_on_disallowed_tool_emits_blocked_audit_event`
- `Route_call_on_invisible_tool_returns_blocked_and_never_invokes_downstream`
- `Route_call_on_unknown_tool_returns_blocked_and_never_invokes_downstream`
- `Block_reason_names_the_tool`

**Where:** `src/runtime/McpGuard.ToolRouter/`, `tests/McpGuard.ToolRouter.Tests/`.

**Depends on:** Task 2 AND Task 3 (uses `ToolRegistration` + `IAuditSink`).

**Reuses:** `design.md` type signatures + `RouteCallAsync` algorithm, `TESTING.md` fakes
rules, the `ObjectMother` from tasks 2/3 (extend it).

**Done when:** `dotnet test tests/McpGuard.ToolRouter.Tests/McpGuard.ToolRouter.Tests.csproj`
exits 0 with all eight tests passing.

**Gate:** `dotnet test` exit 0; sub-agent reports test count + result.

---

## Task 5 — Gateway.Api: Program.cs wiring + MCP handlers + sample server tool handlers

**Status:** `[x]`

**What:**
- `src/runtime/McpGuard.Gateway.Api/Program.cs` — full DI wiring per `design.md`:
  configure `ToolRegistryOptions`, register `IToolRegistry`, `IAuditSink`, `IMcpClientFactory`
  (SDK-backed `SdkMcpClientFactory`), `IToolRouter`. `AddMcpServer()` + map HTTP at `/mcp`.
  Register handlers for `initialize`, `tools/list`, `tools/call` that delegate to `IToolRouter`
  + `IAuditSink`. HTTPS redirect only when not in Development.
- `src/runtime/McpGuard.Gateway.Api/appsettings.json` — add the three-tool `McpGuard:Tools`
  example from `design.md` (echo + add approved/visible, dangerous not).
- Implement `SdkMcpClientFactory` + `SdkMcpDownstreamClient` wrapping the SDK's
  `McpClientFactory.CreateAsync` and `IMcpClient.CallToolAsync`. Live in `McpGuard.ToolRouter`
  or a small adapter class in `Gateway.Api` — pick whichever keeps `ToolRouter` testable
  without an SDK reference in the unit tests (prefer: adapter in `Gateway.Api` so
  `McpGuard.ToolRouter` stays SDK-free and unit-testable with pure fakes).
- `src/runtime/McpGuard.SampleTools.Server/Program.cs` — wire `AddMcpServer()` + register
  `echo(message)` and `add(a, b)` tool handlers via the SDK; listen on `http://0.0.0.0:8080/mcp`.

**Where:** `src/runtime/McpGuard.Gateway.Api/`, `src/runtime/McpGuard.SampleTools.Server/`,
`src/runtime/McpGuard.ToolRouter/` (only if the SDK adapter ends up there — see above).

**Depends on:** Task 1, Task 2, Task 3, Task 4 (all runtime pieces exist).

**Reuses:** `design.md` Program.cs outline + SDK notes, `CONVENTIONS.md` config rules.

**Done when:**
- `dotnet build src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj` exits 0.
- `dotnet build src/runtime/McpGuard.SampleTools.Server/McpGuard.SampleTools.Server.csproj`
  exits 0.
- `dotnet run --project src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj` starts
  without throwing and logs that it is listening (sub-agent verifies via a short timeout
  run, then kills the process).
- `dotnet run --project src/runtime/McpGuard.SampleTools.Server/McpGuard.SampleTools.Server.csproj`
  starts and listens on `8080/mcp` (same short-run verification).
- `docker build` of the sample server Dockerfile still exits 0.

**Tests:** none new in this task (integration tests are task 6).

**Gate:** the four commands above exit 0 / start cleanly. Sub-agent reports each.

---

## Task 6 — Integration tests: Testcontainers + WebApplicationFactory end-to-end

**Status:** `[x]`

**What:** Implement `tests/McpGuard.Gateway.Api.Tests/`:
- `WebApplicationFactory<Program>` to host the gateway in-process on plain HTTP.
- Override `IToolRegistry` (or `ToolRegistryOptions`) so `echo`/`add` `DownstreamUrl` = the
  Testcontainer's mapped URL, and add a `dangerous` entry with `Allowed=false, Visible=false`
  pointing at the same URL.
- Override `IAuditSink` with a `CapturingAuditSink` fake so tests can assert events.
- Use Testcontainers' `ImageFromDockerfile` to build & start the `McpGuard.SampleTools.Server`
  image; get the mapped host port; tear down in `IAsyncLifetime.DisposeAsync`.
- Drive the gateway with a real SDK MCP client (in the test) over HTTP.

**Tests:**
- `Initialize_negotiates_protocol_and_returns_session_id`
- `Tools_list_returns_only_approved_tools` (echo + add present; dangerous absent)
- `Tools_call_on_approved_echo_returns_downstream_result`
- `Tools_call_on_approved_add_returns_downstream_result`
- `Tools_call_on_disallowed_tool_is_blocked_with_jsonrpc_error` (assert `isError` result
  and that the message names `dangerous`; reconciled 2026-07-16 by M2 T2 — the earlier
  `-32602` envelope assertion was replaced by the SDK's `isError` shape)
- `Tools_call_on_invisible_tool_is_blocked_with_jsonrpc_error`
- `Audit_emits_initialized_listed_allowed_and_blocked_events_in_order` (via
  `CapturingAuditSink`)

**Where:** `tests/McpGuard.Gateway.Api.Tests/`.

**Depends on:** Task 5 (gateway + sample server run).

**Reuses:** `TESTING.md` integration flow + naming, `ObjectMother` extended for integration
fixtures, the `CapturingAuditSink` fake pattern.

**Done when:** `dotnet test tests/McpGuard.Gateway.Api.Tests/McpGuard.Gateway.Api.Tests.csproj`
exits 0 with all seven tests passing (requires Docker daemon). This task demonstrates all
five M1 exit criteria green.

**Gate:** `dotnet test` exit 0; sub-agent reports test count + result + which exit criteria
were demonstrated. If Docker is unavailable in the sub-agent's environment, the sub-agent
reports `Blocked` with the Docker error and the orchestrator arranges a re-run.

---

## Task 7 — Docs + STATE update

**Status:** `[x]`

**What:**
- Update `README.md` with: project blurb, run commands (`dotnet run` for gateway + sample
  server), test commands (unit + integration, noting the Docker prerequisite), and a pointer
  to `.specs/` for the spec-driven workflow.
- Update `AGENTS.md` testing section to match `.specs/codebase/TESTING.md` (snake_case names,
  no mocks, fakes + Object Mother, Testcontainers for integration). Note the change as a
  deliberate supersession.
- Update `.specs/project/STATE.md`: mark M1 complete in the session log, record any new
  lessons/blockers discovered during implementation, refresh the Todos (e.g. remove the
  "update AGENTS.md" todo if done in this task).
- Optional: fix the `dotnet build McpGuard.slnx` failure if it was resolved by the new
  project wiring; otherwise leave the AGENTS.md caveat in place.

**Where:** `README.md`, `AGENTS.md`, `.specs/project/STATE.md`.

**Depends on:** Task 6 (implementation is proven before docs claim it works).

**Reuses:** `STRUCTURE.md` for the run/test commands, `TESTING.md` for the AGENTS.md update.

**Done when:** the three files are updated and `README.md` lists working run + test commands.

**Gate:** sub-agent reports the diffs; orchestrator reviews.

---

## Parallelism & sequencing

```
Task 1
  |
  +-- Task 2 [P] --+
  |                |
  +-- Task 3 [P] --+
                   |
                   v
                 Task 4
                   |
                   v
                 Task 5
                   |
                   v
                 Task 6
                   |
                   v
                 Task 7
```

Tasks 2 and 3 run concurrently (one sub-agent each). Task 4 waits for both. Task 5 waits for
1 + 4. Task 6 waits for 5. Task 7 waits for 6.

## Post-implementation

Run the **spec-driven-eval** skill against `spec.md` (M1-R1..M1-R10 + M1-N1..M1-N5) and the
implemented code + tests. Every requirement should grade as fulfilled. Record the eval
result in `STATE.md`. If any requirement is partial, open a follow-up todo before starting
M2.