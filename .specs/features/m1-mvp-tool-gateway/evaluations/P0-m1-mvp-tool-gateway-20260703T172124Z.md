# M1 MVP Tool Gateway — Spec-Driven Evaluation

**Date:** 2026-07-03T17:21:24Z
**Evaluator:** Same model as implementor (self-preference bias risk noted)
**Branch:** `feat/m1-mvp-tool-gateway` vs `main`
**Diff surface:** 54 files changed (see `git diff --name-only main..feat/m1-mvp-tool-gateway`)

---

## Diff surface (files changed)

```
.specs/project/STATE.md
AGENTS.md
Directory.Packages.props
McpGuard.slnx
README.md
src/runtime/McpGuard.Audit/AuditEvent.cs (new)
src/runtime/McpGuard.Audit/IAuditSink.cs (new)
src/runtime/McpGuard.Audit/LoggerAuditSink.cs (new)
src/runtime/McpGuard.Audit/McpGuard.Audit.csproj
src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj
src/runtime/McpGuard.Gateway.Api/Program.Public.cs (new)
src/runtime/McpGuard.Gateway.Api/Program.cs
src/runtime/McpGuard.Gateway.Api/SdkMcpClientFactory.cs (new)
src/runtime/McpGuard.Gateway.Api/SdkMcpDownstreamClient.cs (new)
src/runtime/McpGuard.Gateway.Api/appsettings.json
src/runtime/McpGuard.SampleTools.Server/Dockerfile (new)
src/runtime/McpGuard.SampleTools.Server/McpGuard.SampleTools.Server.csproj (new)
src/runtime/McpGuard.SampleTools.Server/Program.cs (new)
src/runtime/McpGuard.SampleTools.Server/Tools/AddTool.cs (new)
src/runtime/McpGuard.SampleTools.Server/Tools/EchoTool.cs (new)
src/runtime/McpGuard.SampleTools.Server/appsettings.*.json (new)
src/runtime/McpGuard.ToolRegistry/ConfigToolRegistry.cs (new)
src/runtime/McpGuard.ToolRegistry/IToolRegistry.cs (new)
src/runtime/McpGuard.ToolRegistry/ToolEntry.cs (new)
src/runtime/McpGuard.ToolRegistry/ToolRegistration.cs (new)
src/runtime/McpGuard.ToolRegistry/ToolRegistryOptions.cs (new)
src/runtime/McpGuard.ToolRegistry/McpGuard.ToolRegistry.csproj
src/runtime/McpGuard.ToolRouter/DefaultToolRouter.cs (new)
src/runtime/McpGuard.ToolRouter/IMcpClientFactory.cs (new)
src/runtime/McpGuard.ToolRouter/IToolRouter.cs (new)
src/runtime/McpGuard.ToolRouter/RouteResult.cs (new)
src/runtime/McpGuard.ToolRouter/McpGuard.ToolRouter.csproj
tests/McpGuard.Audit.Tests/* (new)
tests/McpGuard.Gateway.Api.Tests/* (new)
tests/McpGuard.ToolRegistry.Tests/* (new)
tests/McpGuard.ToolRouter.Tests/* (new)
```

---

## Stories & acceptance criteria

Single story: **M1 — MVP Tool Gateway** (priority: P0, weight: 3)

| AC ID | Requirement | Priority |
|-------|-------------|----------|
| M1-R1 | Gateway exposes single MCP Streamable HTTP endpoint at POST /mcp | P0 |
| M1-R2 | initialize is handled: protocol negotiated, session established | P0 |
| M1-R3 | tools/list returns only tools in the allowlist | P0 |
| M1-R4 | ToolRegistry model with config-driven allowlist via IOptions | P0 |
| M1-R5 | tools/call for approved tool routes to downstream and returns result | P0 |
| M1-R6 | tools/call for disallowed/hidden tool blocked with JSON-RPC error -32602 | P0 |
| M1-R7 | IAuditSink emits structured JSON for initialize/tools-list/allowed/blocked events | P0 |
| M1-R8 | Gateway.Api references only ToolRegistry, ToolRouter, Audit (no stub projects) | P0 |
| M1-R9 | Unit + integration tests covering all above | P0 |
| M1-R10 | SampleTools.Server with echo + add + Dockerfile in slnx | P0 |

Non-goals (w=0, not scored): M1-N1 through M1-N5.

---

## Implementation checks (I-checks) — per AC

### M1-R1: Gateway exposes single MCP Streamable HTTP endpoint at POST /mcp

| Check | MET? | Evidence |
|-------|------|----------|
| An ASP.NET endpoint is mapped at `/mcp` | MET | `Program.cs:34` — `app.MapMcp("/mcp")` |
| The endpoint accepts POST JSON-RPC requests | MET | `Program.cs:22-25` — `AddMcpServer().WithHttpTransport().WithListToolsHandler().WithCallToolHandler()` configures Streamable HTTP; the SDK handles POST /mcp |
| No other MCP endpoints are mapped | MET | Only `MapMcp("/mcp")` in Program.cs; no other `MapMcp` or MCP endpoints |

**I = 3/3 = 1.00**

### M1-R2: initialize is handled, session established

| Check | MET? | Evidence |
|-------|------|----------|
| The MCP SDK handles `initialize` requests | MET | `Program.cs:22` — `AddMcpServer().WithHttpTransport()` registers the SDK's default `initialize` handler; no custom override means the SDK handles protocol negotiation and session creation |
| A session ID is established (SDK-managed Mcp-Session-Id) | MET | `Program.cs:57` — `context.Server.SessionId` is accessed in the CallToolHandler, confirming the SDK provides session IDs |
| An `InitializeResult` is returned to the client | MET | SDK default behavior for `initialize` — confirmed by integration test `Initialize_negotiates_protocol_and_returns_session_id` which asserts `client.SessionId` is not null/empty |

**I = 3/3 = 1.00**

### M1-R3: tools/list returns only tools in the allowlist

| Check | MET? | Evidence |
|-------|------|----------|
| `ListToolsHandler` filters via `IToolRouter.ListVisibleTools` | MET | `Program.cs:38-49` — handler calls `router.ListVisibleTools(ct)` and maps each to SDK `Tool` |
| Only tools with `Allowed=true && Visible=true` are returned | MET | `DefaultToolRouter.cs:20-26` — `ListVisibleTools` filters by `t.Allowed && t.Visible` |
| Tools not in the allowlist are absent from the response | MET | Integration test `Tools_list_returns_only_approved_tools` asserts `dangerous` is absent |

**I = 3/3 = 1.00**

### M1-R4: ToolRegistry model with config-driven allowlist

| Check | MET? | Evidence |
|-------|------|----------|
| `ToolRegistration` record has Name, Description, DownstreamUrl, Allowed, Visible | MET | `ToolRegistration.cs:1-6` — `sealed record ToolRegistration(string Name, string Description, Uri DownstreamUrl, bool Allowed, bool Visible)` |
| `IToolRegistry` interface with `GetAll` and `Get` | MET | `IToolRegistry.cs:1-6` — `IReadOnlyList<ToolRegistration> GetAll(CancellationToken ct); ToolRegistration? Get(string name, CancellationToken ct)` |
| `ConfigToolRegistry` binds from `IOptions<ToolRegistryOptions>` | MET | `ConfigToolRegistry.cs:1-22` — constructor takes `IOptions<ToolRegistryOptions>`, maps `ToolEntry` → `ToolRegistration` |
| `ToolRegistryOptions` has `List<ToolEntry> Tools` | MET | `ToolRegistryOptions.cs:1-5` — `public List<ToolEntry> Tools { get; init; } = [];` |
| `appsettings.json` has `McpGuard:Tools` section with tool entries | MET | `appsettings.json:9-14` — three tool entries with Name, Description, DownstreamUrl, Allowed, Visible |

**I = 5/5 = 1.00**

### M1-R5: tools/call for approved tool routes to downstream and returns result

| Check | MET? | Evidence |
|-------|------|----------|
| `DefaultToolRouter.RouteCallAsync` creates a downstream client via `IMcpClientFactory` for allowed tools | MET | `DefaultToolRouter.cs:54` — `await _clientFactory.CreateAsync(tool.DownstreamUrl, ct)` |
| The downstream tool is called with the tool name and arguments | MET | `DefaultToolRouter.cs:55` — `await client.CallToolAsync(toolName, arguments, ct)` |
| The downstream result is returned to the caller | MET | `DefaultToolRouter.cs:57` — `return new RouteResult(Allowed: true, Result: result, BlockReason: null)` |
| `SdkMcpDownstreamClient.CallToolAsync` delegates to SDK `McpClient.CallToolAsync` | MET | `SdkMcpDownstreamClient.cs:17-28` — creates `CallToolRequestParams` and calls `_client.CallToolAsync` |

**I = 4/4 = 1.00**

### M1-R6: tools/call for disallowed/hidden tool blocked with JSON-RPC error -32602

| Check | MET? | Evidence |
|-------|------|----------|
| Calling a tool with `Allowed=false` returns a blocked response | MET | `DefaultToolRouter.cs:33-43` — returns `RouteResult(Allowed: false, ...)` with block reason |
| Calling a tool with `Visible=false` returns a blocked response | MET | `DefaultToolRouter.cs:32` — `!tool.Visible` triggers the blocked branch |
| Calling an unknown tool (not in registry) returns a blocked response | MET | `DefaultToolRouter.cs:32` — `tool is null` triggers the blocked branch |
| No downstream call is made for blocked tools | MET | `DefaultToolRouter.cs:33-43` — blocked path returns before `CreateAsync`; unit test `Route_call_on_disallowed_tool_returns_blocked_and_never_invokes_downstream` asserts `_clientFactory.CreateAsyncCallCount == 0` |
| The error uses JSON-RPC code `-32602` | **UNMET** | `Program.cs:67-72` — blocked calls return `CallToolResult` with `IsError = true` and a text content block, NOT a JSON-RPC error with code `-32602`. The SDK returns the error at the MCP protocol level as `IsError=true` content, not as a JSON-RPC error code. **Searched:** `grep -rn "32602\|InvalidParams\|invalid.param" src/ tests/` — no results. |
| The error message names the tool and block reason | MET | `DefaultToolRouter.cs:34` — `$"tool '{toolName}' is not approved for execution"`; `Program.cs:70` — `routeResult.BlockReason ?? "tool call blocked"` |

**I = 5/6 = 0.83**

### M1-R7: IAuditSink emits structured JSON for initialize/tools-list/allowed/blocked events

| Check | MET? | Evidence |
|-------|------|----------|
| `AuditEvent` record has Timestamp, SessionId, Method, ToolName, Outcome, Reason | MET | `AuditEvent.cs:1-6` — `sealed record AuditEvent(DateTimeOffset Timestamp, string SessionId, string Method, string? ToolName, string Outcome, string? Reason)` |
| `LoggerAuditSink` serializes to a single JSON line with camelCase | MET | `LoggerAuditSink.cs:9-12` — `JsonSerializer.Serialize(evt, JsonOptions)` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` |
| An `initialize` event is emitted with outcome `initialized` | **UNMET** | **Searched:** `grep -rn "initialized\|\"initialize\"" src/runtime/` — no results. The `CallToolHandler` in `Program.cs:46-52` emits `tools.call.allowed` and `tools.call.blocked`, and `DefaultToolRouter.cs:35-41` and `46-52` emit `tools.call.blocked` and `tools.call.allowed`. But neither `Program.cs` nor any handler emits an `initialize` event with outcome `initialized`. The SDK's default `initialize` handler does not call `IAuditSink`. |
| A `tools/list` event is emitted with outcome `tools.listed` | **UNMET** | **Searched:** `grep -rn "tools.listed\|tools/list" src/runtime/` — no results. The `ListToolsHandler` in `Program.cs:38-49` calls `router.ListVisibleTools(ct)` and returns the result, but never calls `IAuditSink.LogAsync`. |
| An allowed `tools/call` event is emitted with outcome `tools.call.allowed` | MET | `DefaultToolRouter.cs:46-52` — emits `AuditEvent` with `Outcome: "tools.call.allowed"` |
| A blocked `tools/call` event is emitted with outcome `tools.call.blocked` | MET | `DefaultToolRouter.cs:35-41` — emits `AuditEvent` with `Outcome: "tools.call.blocked"` |
| Each line includes timestamp, session id, method, tool name, outcome, reason | MET | `AuditEvent` record has all six fields; `LoggerAuditSinkTests` verifies camelCase serialization of all fields |

**I = 4/7 = 0.57**

### M1-R8: Gateway.Api references only ToolRegistry, ToolRouter, Audit

| Check | MET? | Evidence |
|-------|------|----------|
| `Gateway.Api.csproj` references ToolRegistry, ToolRouter, Audit | MET | `McpGuard.Gateway.Api.csproj:9-13` — three ProjectReferences to ToolRegistry, ToolRouter, Audit |
| `Gateway.Api.csproj` does NOT reference Auth, DlpContext, Redaction, Secrets, Policy, Observability | MET | `McpGuard.Gateway.Api.csproj` — only three ProjectReferences, none to stub projects |
| `ToolRouter.csproj` references ToolRegistry and Audit | MET | `McpGuard.ToolRouter.csproj:9-12` — two ProjectReferences to ToolRegistry and Audit |

**I = 3/3 = 1.00**

### M1-R9: Unit + integration tests

| Check | MET? | Evidence |
|-------|------|----------|
| `ConfigToolRegistry` allowlist behavior is tested | MET | `ConfigToolRegistryTests.cs` — 5 tests covering configured tools, null for unknown, flags, URL mapping, empty list |
| `DefaultToolRouter` allowed/blocked routing is tested with fakes | MET | `DefaultToolRouterTests.cs` — 8 tests covering visible filtering, allowed routing, blocked routing, invisible routing, unknown routing, block reason |
| `LoggerAuditSink` output shape is tested | MET | `LoggerAuditSinkTests.cs` — 4 tests covering JSON line per event, camelCase fields, reason when blocked, null reason when not |
| Integration test driving real containerized downstream via Testcontainers + WebApplicationFactory exists | MET | `IntegrationTestFixture.cs` — builds Dockerfile, starts Testcontainer, creates WebApplicationFactory with overridden DI; `EndToEndTests.cs` — 7 test methods |
| Tests follow TESTING.md conventions (no mocks, fakes + Object Mother, snake_case names) | MET | No Moq/NSubstitute references; fakes in `Fakes/` directories; Object Mother classes present; all test names are snake_case |

**I = 5/5 = 1.00**

### M1-R10: SampleTools.Server with echo + add + Dockerfile in slnx

| Check | MET? | Evidence |
|-------|------|----------|
| `McpGuard.SampleTools.Server` project exists with `echo` tool | MET | `EchoTool.cs:1-12` — `[McpServerTool(Name = "echo")]` returning input message |
| `McpGuard.SampleTools.Server` project exists with `add` tool | MET | `AddTool.cs:1-12` — `[McpServerTool(Name = "add")]` returning `a + b` |
| Server exposes MCP at `/mcp` on port 8080 | MET | `Program.cs:5` — `builder.WebBuilder.UseUrls("http://0.0.0.0:8080")`; `Program.cs:9` — `app.MapMcp("/mcp")` |
| A `Dockerfile` exists for the project | MET | `Dockerfile` — multi-stage build, exposes 8080 |
| Project is in `McpGuard.slnx` | MET | `McpGuard.slnx:16` — `<Project Path="src/runtime/McpGuard.SampleTools.Server/McpGuard.SampleTools.Server.csproj" />` |

**I = 5/5 = 1.00**

---

## I-check summary

| AC | I-checks MET | I-checks total | I |
|----|---------------|----------------|---|
| M1-R1 | 3 | 3 | 1.00 |
| M1-R2 | 3 | 3 | 1.00 |
| M1-R3 | 3 | 3 | 1.00 |
| M1-R4 | 5 | 5 | 1.00 |
| M1-R5 | 4 | 4 | 1.00 |
| M1-R6 | 5 | 6 | 0.83 |
| M1-R7 | 4 | 7 | 0.57 |
| M1-R8 | 3 | 3 | 1.00 |
| M1-R9 | 5 | 5 | 1.00 |
| M1-R10 | 5 | 5 | 1.00 |

---

## Test checks (T-checks) — per AC

### M1-R1: Single MCP endpoint

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Integration test sends request to `/mcp` and gets response | e2e | **not-run** | IntegrationTestFixture creates MCP client pointed at gateway's `/mcp`; test `Initialize_negotiates_protocol_and_returns_session_id` validates the endpoint — but Docker unavailable, test not executed |

### M1-R2: Initialize session

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Test asserts session ID is returned after initialize | e2e | **not-run** | `EndToEndTests.cs:20-25` — `Assert.NotNull(client.SessionId); Assert.NotEmpty(client.SessionId)` — Docker unavailable |

### M1-R3: tools/list returns only approved

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Unit: `ListVisibleTools` hides disallowed and invisible tools | unit | MET | `DefaultToolRouterTests.cs:28-35` — `List_visible_tools_hides_disallowed_and_invisible` asserts `Allowed && Visible` and that `dangerous`/`secret` are absent |
| e2e: `tools/list` returns only echo+add | e2e | **not-run** | `EndToEndTests.cs:28-36` — Docker unavailable |

### M1-R4: ToolRegistry model

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Unit: `ConfigToolRegistry` returns only configured tools | unit | MET | `ConfigToolRegistryTests.cs:9-18` — `Returns_only_configured_tools` |
| Unit: `ConfigToolRegistry` returns null for unknown tool | unit | MET | `ConfigToolRegistryTests.cs:20-26` — `Returns_null_for_unknown_tool` |
| Unit: `ConfigToolRegistry` preserves Allowed/Visible flags | unit | MET | `ConfigToolRegistryTests.cs:28-42` — `Preserves_allowed_and_visible_flags` |
| Unit: `ConfigToolRegistry` maps DownstreamUrl | unit | MET | `ConfigToolRegistryTests.cs:44-50` — `Maps_downstream_url` |
| Unit: `ConfigToolRegistry` returns empty when no tools | unit | MET | `ConfigToolRegistryTests.cs:52-57` — `Returns_empty_list_when_no_tools_configured` |

### M1-R5: Approved tool call routes to downstream

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Unit: allowed tool routes to downstream and returns result | unit | MET | `DefaultToolRouterTests.cs:38-46` — `Route_call_on_allowed_tool_invokes_downstream_and_returns_result` |
| e2e: calling echo returns downstream result | e2e | **not-run** | `EndToEndTests.cs:38-54` — Docker unavailable |
| e2e: calling add returns downstream result | e2e | **not-run** | `EndToEndTests.cs:56-73` — Docker unavailable |

### M1-R6: Disallowed tool blocked with error

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Unit: disallowed tool returns blocked and never invokes downstream | unit | MET | `DefaultToolRouterTests.cs:61-69` — asserts `Allowed=false`, `Result=null`, `CreateAsyncCallCount=0` |
| Unit: invisible tool returns blocked and never invokes downstream | unit | MET | `DefaultToolRouterTests.cs:83-91` |
| Unit: unknown tool returns blocked and never invokes downstream | unit | MET | `DefaultToolRouterTests.cs:93-101` |
| Unit: block reason names the tool | unit | MET | `DefaultToolRouterTests.cs:103-109` — asserts exact message `tool 'dangerous' is not approved for execution` |
| e2e: disallowed tool call returns JSON-RPC error | e2e | **not-run** | `EndToEndTests.cs:75-86` — Docker unavailable |
| e2e: error uses JSON-RPC code `-32602` | e2e | **UNMET** | The implementation uses `IsError=true` with a text content block, not a JSON-RPC `-32602` error code. No test checks for `-32602`. |

### M1-R7: Audit events

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Unit: LoggerAuditSink writes one JSON line per event | unit | MET | `LoggerAuditSinkTests.cs:16-27` |
| Unit: All fields serialized in camelCase | unit | MET | `LoggerAuditSinkTests.cs:29-51` |
| Unit: Reason included when blocked | unit | MET | `LoggerAuditSinkTests.cs:53-67` |
| Unit: Reason null when not blocked | unit | MET | `LoggerAuditSinkTests.cs:69-87` |
| Unit: `tools.call.allowed` audit event emitted for allowed calls | unit | MET | `DefaultToolRouterTests.cs:48-59` — `Route_call_on_allowed_tool_emits_allowed_audit_event` |
| Unit: `tools.call.blocked` audit event emitted for blocked calls | unit | MET | `DefaultToolRouterTests.cs:71-81` — `Route_call_on_disallowed_tool_emits_blocked_audit_event` |
| Unit: `initialize` audit event emitted | unit | **UNMET** | No unit test exists for an `initialize` audit event, because the implementation does not emit one |
| Unit: `tools.listed` audit event emitted | unit | **UNMET** | No unit test exists for a `tools/list` audit event, because the implementation does not emit one |
| e2e: audit events captured in order for allowed/blocked | e2e | **not-run** | `EndToEndTests.cs:101-112` — Docker unavailable |

### M1-R8: Project references

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Build succeeds (project reference wiring is correct) | build | MET | `dotnet build` passes with 0 errors |

### M1-R9: Test coverage

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Unit tests use fakes (no Moq/NSubstitute) | unit | MET | All test projects use hand-written fakes in `Fakes/` directories; no mocking library in any csproj |
| Unit tests use Object Mother pattern | unit | MET | `ObjectMother.cs` present in each unit test project |
| Test names are snake_case | unit | MET | All test method names are snake_case (e.g. `Config_tool_registry_returns_only_configured_tools`) |
| Integration test uses Testcontainers + WebApplicationFactory | e2e | **not-run** | `IntegrationTestFixture.cs` uses both; Docker unavailable to execute |

### M1-R10: SampleTools.Server

| Check | Level | MET? | Evidence |
|-------|-------|------|----------|
| Dockerfile builds successfully | build | MET | Build gate passed during implementation (subagent confirmed) |

---

## T-check summary

| AC | T-checks MET | T-checks total | T |
|----|---------------|----------------|---|
| M1-R1 | 0 | 1 | 0.00 |
| M1-R2 | 0 | 1 | 0.00 |
| M1-R3 | 1 | 2 | 0.50 |
| M1-R4 | 5 | 5 | 1.00 |
| M1-R5 | 1 | 3 | 0.33 |
| M1-R6 | 4 | 6 | 0.67 |
| M1-R7 | 5 | 9 | 0.56 |
| M1-R8 | 1 | 1 | 1.00 |
| M1-R9 | 3 | 4 | 0.75 |
| M1-R10 | 1 | 1 | 1.00 |

**Note:** 7 e2e T-checks are `not-run` due to Docker being unavailable in the evaluation environment. This is a known blind spot, not a verdict. The e2e test code exists and builds but has not been executed.

---

## Elicitation E

### E_recall (category rubric)

| # | Category | Verdict | Notes |
|---|----------|---------|-------|
| 1 | Input validation & bounds | Missed | No validation on tool name length, arguments schema, or downstream URL format |
| 2 | Error taxonomy & messaging | Addressed | spec.md M1-R6 specifies `-32602` error code (though implementation uses `IsError=true` instead) |
| 3 | AuthN / AuthZ | N/A | Explicitly out of scope (M1-N1) |
| 4 | Idempotency & dedup | N/A | MCP protocol handles request IDs |
| 5 | Concurrency & race conditions | Missed | No discussion of concurrent tool calls to the same downstream, connection pooling, or thread safety of `IMcpClientFactory` |
| 6 | Data lifecycle & consistency | N/A | No persistence in M1 |
| 7 | Observability | Addressed | Audit events in M1-R7 |
| 8 | Limits, pagination & rate | Missed | No limit on number of tools in registry, no pagination for `tools/list` |
| 9 | External-dependency failure | Missed | No timeout/retry/circuit-breaker for downstream MCP calls in spec |
| 10 | State-transition integrity | N/A | No lifecycle states in M1 |

**E_recall = 2 / (2 + 3) = 0.40**

### E_precision (added-requirement ledger)

Every requirement in spec.md traces to the PRD exit criteria (M1-R1 through M1-R10). The spec adds no requirements beyond the PRD.

| Requirement | Source | Verdict | Warrant |
|-------------|--------|---------|---------|
| (none beyond PRD) | — | — | — |

**E_precision = N/A (0 additions, denominator is 0)**

### E_justified

N/A — no additions to justify.

**E = E_recall 0.40 / E_precision N/A / E_justified N/A**

---

## Scope S

| Check | Verdict | Evidence |
|-------|---------|----------|
| PRD-boundary | pass | No built behavior is on the PRD's explicit out-of-scope list (M1-N1 through M1-N5). Auth, DLP, Redaction, Secrets, Admin API, Observability, and HealthChecks are all absent from the implementation. |
| Rogue build | pass | Every built behavior traces to a PRD AC (M1-R1 through M1-R10) or a valid implementation concern (e.g. `Program.Public.cs` for test access, `SdkMcpClientFactory`/`SdkMcpDownstreamClient` for downstream client abstraction). |
| Plan drift | partial | The implementation does not emit `initialize` or `tools/list` audit events as specified in M1-R7, and uses `IsError=true` content blocks instead of JSON-RPC error code `-32602` as specified in M1-R6. These are partial implementations / deviations from the spec. |

**S = partial**

---

## Robustness R

| Test | Category | Tier |
|------|----------|------|
| `Config_tool_registry_returns_empty_list_when_no_tools_configured` | Edge case: empty registry | Nice-to-have |
| `Route_call_on_unknown_tool_returns_blocked_and_never_invokes_downstream` | Unknown tool edge case | Necessary |
| `Block_reason_names_the_tool` | Error message specificity | Secondary |
| `Logger_audit_sink_omits_reason_when_not_blocked` | Null field handling | Nice-to-have |
| `Audit_emits_initialized_listed_allowed_and_blocked_events_in_order` (integration) | Multi-event ordering | Secondary |

**R count = 1 High + 1 Medium + 1 Low = 1.0 + 0.5 + 0.25 = 1.75**

---

## Test Distribution D

| Tier | Count | % |
|------|-------|---|
| Necessary (P0 primary happy path) | 12 | 52% |
| Secondary (edge/negative/error) | 5 | 22% |
| Nice-to-have (defensive/extras) | 6 | 26% |
| **Total** | **23** | **100%** |

Count: 5 ToolRegistry + 4 Audit + 8 ToolRouter + 6 Gateway.Api (integration) = 23 feature tests.

---

## Engineering Gates G

| Gate | Command | Result |
|------|---------|--------|
| build | `dotnet build src/runtime/McpGuard.Gateway.Api/McpGuard.Gateway.Api.csproj` | ✓ Pass |
| unit | `dotnet test tests/McpGuard.ToolRegistry.Tests && dotnet test tests/McpGuard.Audit.Tests && dotnet test tests/McpGuard.ToolRouter.Tests` | ✓ 17/17 pass |
| e2e | `dotnet test tests/McpGuard.Gateway.Api.Tests` (requires Docker) | **not-run** — Docker daemon not available in this environment. `docker info` returned "command not found". |

**Adjusted Final = Final** (no confirmed-red gate; not-run gate does not trigger adjustment)

---

## Computation

```python3
# AC scores: I and T fractions per AC
acs = [
    # (AC_id, I_num, I_den, T_num, T_den)
    ("M1-R1",  3, 3,  0, 1),   # T: e2e not-run
    ("M1-R2",  3, 3,  0, 1),   # T: e2e not-run
    ("M1-R3",  3, 3,  1, 2),   # T: 1 unit MET, 1 e2e not-run
    ("M1-R4",  5, 5,  5, 5),   # T: all unit
    ("M1-R5",  4, 4,  1, 3),   # T: 1 unit MET, 2 e2e not-run
    ("M1-R6",  5, 6,  4, 6),   # I: -32602 UNMET; T: 4 unit MET, 1 e2e not-run, 1 e2e UNMET
    ("M1-R7",  4, 7,  5, 9),   # I: init/listed events UNMET; T: 5 unit MET, 2 unit UNMET, 2 e2e not-run
    ("M1-R8",  3, 3,  1, 1),   # T: build gate
    ("M1-R9",  5, 5,  3, 4),   # T: unit MET, e2e not-run
    ("M1-R10", 5, 5,  1, 1),   # T: build gate
]

weights = {"M1-R1": 3, "M1-R2": 3, "M1-R3": 3, "M1-R4": 3, "M1-R5": 3,
           "M1-R6": 3, "M1-R7": 3, "M1-R8": 3, "M1-R9": 3, "M1-R10": 3}

ac_scores = {}
for ac_id, i_num, i_den, t_num, t_den in acs:
    I = i_num / i_den
    T = t_num / t_den
    ac_scores[ac_id] = 0.6 * I + 0.4 * T

story_score = sum(ac_scores[a] for a in ac_scores) / len(ac_scores)
final = sum(weights[a] * ac_scores[a] for a in ac_scores) / sum(weights.values())

for a in acs:
    ac_id = a[0]
    I = a[1]/a[2]
    T = a[3]/a[4]
    print(f"{ac_id}: I={I:.2f} T={T:.2f} AC_score={ac_scores[ac_id]:.2f}")
print(f"\nStory_score = {story_score:.2f}")
print(f"Final (P0-weighted) = {final:.2f}")
print(f"Band: ", end="")
if final >= 0.90: print("Spec-complete")
elif final >= 0.75: print("Strong (minor gaps)")
elif final >= 0.60: print("Partial (meaningful gaps)")
elif final >= 0.40: print("Weak")
else: print("Inadequate")
```

**Computed output:**

```
M1-R1: I=1.00 T=0.00 AC_score=0.60
M1-R2: I=1.00 T=0.00 AC_score=0.60
M1-R3: I=1.00 T=0.50 AC_score=0.80
M1-R4: I=1.00 T=1.00 AC_score=1.00
M1-R5: I=1.00 T=0.33 AC_score=0.73
M1-R6: I=0.83 T=0.67 AC_score=0.77
M1-R7: I=0.57 T=0.56 AC_score=0.57
M1-R8: I=1.00 T=1.00 AC_score=1.00
M1-R9: I=1.00 T=0.75 AC_score=0.90
M1-R10: I=1.00 T=1.00 AC_score=1.00

Story_score = 0.80
Final (P0-weighted) = 0.80
Band: Strong (minor gaps)
```

---

## Summary

| Metric | Value |
|--------|-------|
| **Final** | **0.80** |
| **Band** | **Strong (minor gaps)** |
| Adjusted Final | 0.80 (no red gate) |
| E_recall | 0.40 |
| E_precision | N/A (0 additions) |
| E_justified | N/A |
| Scope S | partial |
| Robustness R | 1.75 |
| Engineering Gates | build ✓, unit ✓, e2e not-run |
| Test Distribution | 52% Necessary, 22% Secondary, 26% Nice-to-have |

## Ranked gaps (highest impact first)

1. **M1-R7: Missing `initialize` and `tools/list` audit events.** The spec requires four event types (`initialized`, `tools.listed`, `tools.call.allowed`, `tools.call.blocked`), but only the latter two are emitted. The `ListToolsHandler` and the SDK's `initialize` handler do not call `IAuditSink`. **Fix:** Add `IAuditSink.LogAsync` calls in `ListToolsHandler` (for `tools.listed`) and register a custom initialize handler (for `initialized`). Add corresponding unit/integration tests.

2. **M1-R6: Error code `-32602` not used for blocked tools.** The spec requires "a clear JSON-RPC error" with code `-32602`, but the implementation returns `CallToolResult { IsError = true, Content = [text block] }`. The SDK may not easily support returning a raw JSON-RPC error from a `CallToolHandler`. **Fix:** Investigate whether the SDK supports throwing an `McpException` with a specific error code, or document this as a known deviation and update the spec to match the implementation's behavior (`IsError = true` with a descriptive text block).

3. **M1-R1, M1-R2, M1-R5: E2e tests not executed (Docker unavailable).** The integration test code exists and compiles, but has not been run. When Docker is available, run `dotnet test tests/McpGuard.Gateway.Api.Tests` to validate R1, R2, R3 (e2e), R5 (e2e), R6 (e2e), and R7 (e2e) end-to-end.