# McpGuard — Roadmap

Milestones are ordered. Each milestone has a goal, the feature themes it delivers, and exit
criteria that must be demonstrably green (via tests + manual run) before the milestone is
closed.

## M1 — MVP Tool Gateway  *(current)*

**Goal:** Deliver the first usable gateway path focused on tool-level governance.

**Themes:**
- Single MCP HTTP endpoint for client JSON-RPC requests.
- Minimal support for `initialize`, `tools/list`, `tools/call`.
- Tool registry model for known downstream tools.
- Tool allowlist so clients only see approved tools.
- Execution guard so denied or hidden tools cannot be called directly.
- Downstream MCP routing as the internal mechanism for executing approved calls.
- Basic audit events for initialize, tool discovery, allowed calls, and blocked calls.

**Exit criteria:**
- A client can initialize a session through McpGuard.
- A client can list tools and only receive approved tools.
- A client can call an approved tool and receive the downstream result.
- A direct call to a disallowed tool is blocked with a clear JSON-RPC error.
- Basic audit output exists for allowed and blocked paths.

**Deferred from M1 (explicit non-goals):** auth (bearer/OIDC), DLP context, redaction,
secrets brokering, Admin API, control-plane store wiring. Those projects stay as stubs.

**Spec:** `.specs/features/m1-mvp-tool-gateway/`

---

## M2 — Auth & Admin-managed registry

**Goal:** Secure the gateway entrance and move the allowlist out of static config into the
admin control plane.

**Themes:**
- OIDC/OAuth bearer token validation on `initialize` (issuer/audience/scope checks).
- Admin API CRUD for `ServerRegistry` + `CapabilityCatalog` + `PolicyStore`.
- Swap `ConfigToolRegistry` for a store-backed `IToolRegistry` implementation without
  changing the runtime pipeline.
- Tenant settings: per-tenant allowlist scoping.

**Exit criteria (draft):**
- Unauthenticated `initialize` is rejected with a clear JSON-RPC auth error.
- An operator can register a downstream server + its tools via the Admin API and have the
  gateway serve them without a restart.
- Allowlist changes in the control plane are reflected in `tools/list` within a bounded
  refresh window.

---

## M3 — DLP & Redaction

**Goal:** Prevent sensitive data from reaching downstream tools, and mask sensitive data in
results returned to clients.

**Themes:**
- `DlpContext` built per request from classifiers + tenant data labels.
- Redaction of tool call arguments before downstream routing.
- Redaction/tokenization of tool call results before returning to the client.
- `DlpPolicyStore` + `RedactionRules` managed via the Admin API.

**Exit criteria (draft):**
- A call argument containing a configured sensitive pattern is redacted before the
  downstream server sees it.
- A downstream result containing a configured sensitive pattern is masked before the client
  sees it.
- Audit events record that redaction occurred (without leaking the redacted content).

---

## M4 — Secrets brokering & Approvals

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

## M5 — Observability & Health

**Goal:** Make the gateway production-operable.

**Themes:**
- OpenTelemetry traces spanning initialize → policy → route → audit.
- Health checks (downstream reachability via `HealthChecks`, store reachability, auth
  provider reachability).
- Persistent audit sink (file/external) beyond the M1 logger sink.

**Exit criteria (draft):**
- A distributed trace for a `tools/call` shows gateway + downstream spans with the tool name,
  outcome, and latency.
- `/health` reports downstream reachability for each registered server.
- Audit events persist across gateway restarts.