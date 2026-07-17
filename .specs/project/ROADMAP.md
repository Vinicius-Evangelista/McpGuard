# McpGuard — Roadmap

Milestones are ordered. Each milestone has a goal, the feature themes it delivers, and exit
criteria that must be demonstrably green (via tests + manual run) before the milestone is
closed.

Milestone split follows the Notion "McpGuard Roadmap Backlog" (M0–M4 on Notion, expanded here
with M5/M6 for operational work).

## M1 — MVP Tool Gateway  *(complete 2026-07-09)*

**Goal:** Deliver the first usable gateway path focused on tool-level governance.

**Themes:**
- Single MCP HTTP endpoint for client JSON-RPC requests.
- Minimal support for `initialize`, `tools/list`, `tools/call`.
- Tool registry model for known downstream tools.
- Tool allowlist so clients only see approved tools.
- Execution guard so denied or hidden tools cannot be called directly.
- Downstream MCP routing as the internal mechanism for executing approved calls.
- Basic audit events for initialize, tool discovery, allowed calls, and blocked calls.

**Exit criteria (all met):**
- A client can initialize a session through McpGuard.
- A client can list tools and only receive approved tools.
- A client can call an approved tool and receive the downstream result.
- A direct call to a disallowed tool is blocked with a clear JSON-RPC error.
- Basic audit output exists for allowed and blocked paths.

**Spec:** `.specs/features/m1-mvp-tool-gateway/` — graded Spec-complete (`Final = 0.9952`,
2026-07-09). Open follow-ups (T-8 audit assertion, `-32602` vs `isError` delta, `Program.cs`
cleanup) are rolled into M2.

---

## M2 — Admin-Controlled Tool Registry  *(current)*

**Goal:** Move tool and server configuration out of static MVP configuration and into explicit
control-plane capabilities.

**Themes:**
- Admin API for registering downstream MCP servers.
- Capability discovery (`tools/list`) on register + on-demand re-sync for server-provided
  tools.
- Tool metadata catalog with server id, tool name, description, schema, and enabled state.
- Allowlist and denylist management (toggle `Allowed`/`Visible` at runtime, no restart).
- Health checks for registered downstream MCP servers.
- Store-backed `IAsyncToolRegistry` feeding the gateway from a persistent SQLite store (live
  read, 0-second refresh window), alongside M1's stable sync `IToolRegistry`.
- Downstream-unreachable failure handling in `DefaultToolRouter` (closes the M1 eval
  category-9 miss).
- Close the open M1 follow-ups (T-8 audit assertion, `-32602` vs `isError` spec
  reconciliation, `Program.cs` duplicate cleanup).

**Exit criteria:**
- An admin can register and inspect MCP servers.
- Tool metadata can be discovered or maintained centrally.
- Tool visibility can be changed without editing runtime code.
- Runtime gateway behavior uses registry data from the control-plane store.
- A `tools/call` to a tool on an unreachable downstream server returns a clear `isError`
  result and emits a `tools.call.blocked` audit event with a `downstream-unreachable` reason.

**Deferred from M2 (explicit non-goals):** auth, tenant scoping, `PolicyStore`,
`TenantSettings`, DLP, redaction, secrets, approvals, observability, persistent audit. Those
projects stay as stubs.

**Spec:** `.specs/features/m2-admin-tool-registry/`

---

## M3 — Policy + Identity

**Goal:** Make tool visibility and execution decisions identity-aware and policy-driven.

**Themes:**
- OIDC/OAuth token validation for MCP clients (issuer/audience/scope checks).
- Tenant, user, role, and scope context for gateway decisions.
- Policy engine for discovery and execution authorization.
- Policy decision model with allow, deny, and future approval-required outcomes.
- Policy store for control-plane managed access rules.
- Auth on the Admin API surface.

**Exit criteria (draft):**
- Tool discovery can vary by caller identity or tenant.
- Tool execution can be allowed or denied based on policy context.
- Policy decisions are explainable enough for audit and debugging.
- Unauthorized requests fail consistently before downstream tool execution.

---

## M4 — DLP, Redaction, Audit, Observability

**Goal:** Add enterprise guardrails around sensitive data, accountability, and operations.

**Themes:**
- `DlpContext` built per request from classifiers + tenant data labels.
- Redaction of tool call arguments before downstream routing.
- Redaction/tokenization of tool call results before returning to the client.
- `DlpPolicyStore` + `RedactionRules` managed via the Admin API.
- Structured audit log for discovery, execution, redaction, and blocked decisions.
- Metrics, logs, traces, and health checks for gateway operations.

**Exit criteria (draft):**
- Sensitive inputs can be inspected before routing.
- Sensitive outputs can be inspected before returning to the client.
- Redaction behavior is policy-driven and auditable.
- Operators can observe gateway health, request volume, decision outcomes, and downstream
  failures.

---

## M5 — Secrets Brokering & Approvals

**Goal:** Supply downstream credentials without exposing them to the client or the tool
arguments, and gate high-impact tools behind an approval workflow.

**Themes:**
- `SecretsProvider` integration; `SecretsBroker` injects credentials at route time.
- Approval workflow for tools marked `approval-required`.
- Audit enrichment: approval id, approver, secret reference (not value).

**Exit criteria (draft):**
- An approved tool whose downstream requires an API key receives it via the secrets broker;
  the key never appears in client arguments or audit output.
- A call to an `approval-required` tool without an active approval is blocked with a clear
  pending-approval error.

---

## M6 — Observability & Health

**Goal:** Make the gateway production-operable.

**Themes:**
- OpenTelemetry traces spanning initialize → policy → route → audit.
- Persistent audit sink (file/external) beyond the M1 logger sink.
- Health checks beyond M2's downstream reachability (store reachability, auth provider
  reachability).

**Exit criteria (draft):**
- A distributed trace for a `tools/call` shows gateway + downstream spans with the tool name,
  outcome, and latency.
- Audit events persist across gateway restarts.

---

## Notes

- Tasks are intentionally deferred until the milestones are accepted.
- The MVP product language should stay centered on tool-level control rather than generic
  request routing.
- Routing remains necessary, but it is not the primary user-facing value proposition for M1.