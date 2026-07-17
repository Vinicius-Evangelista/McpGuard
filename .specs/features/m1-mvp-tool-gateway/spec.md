# M1 — MVP Tool Gateway — Specification

Status: **complete** (2026-07-09). Implementation graded Spec-complete by spec-driven-eval
(`Final = 0.9952`, band ≥ 0.90). See
`evaluations/p0-m1-mvp-tool-gateway-20260709T004017Z.md` for the report and
`evaluations/_ac-baseline.md` for the frozen checklist. One follow-up test check
(M1-R7 T-8) was closed by M2 (T1, 2026-07-16); the design-vs-impl delta on the blocked-call
error shape was reconciled by canonicalizing the `isError` shape (M2 T2, 2026-07-16). Both
are recorded in `.specs/project/STATE.md`.
Source PRD: M1 milestone in `.specs/project/ROADMAP.md` and the user's M1 brief.

## Goal

Deliver the first usable gateway path focused on tool-level governance. A client speaking
standard MCP JSON-RPC can initialize through McpGuard, list only approved tools, call an
approved tool and get the downstream result, be blocked from disallowed tools with a clear
JSON-RPC error, and have every decision audited.

## Traceability: requirements → exit criteria

| ID | Requirement | Exit criterion |
|----|-------------|----------------|
| M1-R1 | Gateway exposes a single MCP Streamable HTTP endpoint at `POST /mcp` accepting client JSON-RPC requests. | "Single MCP HTTP endpoint for client JSON-RPC requests" |
| M1-R2 | `initialize` is handled: protocol version and capabilities are negotiated and an `InitializeResult` is returned; a session is established (SDK-managed `Mcp-Session-Id`). | "A client can initialize a session through McpGuard" |
| M1-R3 | `tools/list` returns only tools present in the configured allowlist. Each returned tool carries name, description, and input schema from the registered downstream tool. | "A client can list tools and only receive approved tools" |
| M1-R4 | A `ToolRegistry` model holds the mapping of approved tool name → downstream server URL + tool descriptor + `Allowed` + `Visible` flags. Source for M1: the `McpGuard:Tools` config section in `appsettings.json`, bound via `IOptions<ToolRegistryOptions>` and surfaced through `IToolRegistry`. | "Tool registry model" + "Tool allowlist" |
| M1-R5 | `tools/call` for an approved tool: the gateway routes the call to the mapped downstream MCP server via an MCP client, and returns the downstream result to the client. | "A client can call an approved tool and receive the downstream result" |
| M1-R6 | `tools/call` for a tool NOT in the allowlist, OR a tool in the registry but marked `Allowed=false` or `Visible=false`, is blocked with a clear JSON-RPC error. No downstream call is made. The blocked result uses the SDK's `CallToolResult { IsError = true }` shape with a `TextContentBlock` message naming the tool and the block reason. (Reconciled 2026-07-16 by M2 T2: the earlier `-32602` envelope clause was dropped in favor of the `isError` shape, which matches the MCP SDK's tool-call error contract.) | "A direct call to a disallowed tool is blocked with a clear JSON-RPC error" |
| M1-R7 | An `IAuditSink` emits one structured JSON line per event to `ILogger`. Event types: `initialize` → outcome `initialized`; `tools/list` → outcome `tools.listed`; allowed `tools/call` → outcome `tools.call.allowed`; blocked `tools/call` → outcome `tools.call.blocked`. Each line includes timestamp, session id, method, tool name (when applicable), outcome, and reason (when blocked). | "Basic audit output exists for allowed and blocked paths" |
| M1-R8 | `Gateway.Api` references `ToolRegistry`, `ToolRouter`, and `Audit`. `ToolRouter` references `ToolRegistry` and `Audit`. The runtime slice in use is wired; empty-stub projects (`Auth`, `DlpContext`, `Redaction`, `Secrets`, `Policy`, `Observability`) are NOT referenced by the gateway in M1. | architecture hygiene |
| M1-R9 | xUnit test projects cover: `ConfigToolRegistry` allowlist behavior, `DefaultToolRouter` allowed/blocked routing (with fakes), `LoggerAuditSink` output shape, and an end-to-end integration test driving a real containerized sample MCP server through the gateway via Testcontainers + `WebApplicationFactory<Program>`. Tests follow the conventions in `.specs/codebase/TESTING.md` (no mocks; fakes + Object Mother; snake_case names). | validates R1–R7 |
| M1-R10 | A `McpGuard.SampleTools.Server` console project exposes two real MCP tools (`echo(message: string) → string`, `add(a: int, b: int) → int`) over `/mcp` and ships with a `Dockerfile` so it can be run as a container. It is added to `McpGuard.slnx`. | enables M1-R9 integration proof |

## Non-goals (explicit deferral)

| ID | Non-goal | Deferred to |
|----|----------|-------------|
| M1-N1 | No bearer/OIDC auth on `initialize`. No `IAuthenticator` interface is introduced in M1. | M2 |
| M1-N2 | No DLP context, no redaction before/after downstream call. `DlpContext` and `Redaction` projects stay empty. | M3 |
| M1-N3 | No secrets brokering; no `ISecretsBroker`. `Secrets` project stays empty. | M4 |
| M1-N4 | No Admin API CRUD; no control-plane store wiring. `Admin.Api` and all control-plane projects stay as stubs. | M2 |
| M1-N5 | No OpenTelemetry traces, no `/health` endpoint. `Observability` and `HealthChecks` stay empty. | M5 |

## Acceptance

M1 is done when:
- All exit criteria are demonstrated green by the M1-R9 test suite.
- `dotnet build` succeeds for every runtime project touched + every test project.
- `dotnet test` passes for all four test projects (with Docker available for the integration
  project).
- A `spec-driven-eval` run against this spec grades every M1-R requirement as fulfilled.