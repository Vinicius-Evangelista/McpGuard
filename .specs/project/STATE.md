# McpGuard — Persistent State

Memory across sessions: decisions made, blockers hit, lessons learned, deferred ideas, and
preferences. Append-only per section; date each entry.

## Decisions

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
  - Test names: simplest descriptive sentence, first letter uppercase, snake_case, no
    `Should_`/`Returns` verbs. Examples:
    `Initialize_negotiates_protocol_and_returns_session_id`,
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
- [ ] Fix the solution-level `dotnet build McpGuard.slnx` failure (currently documented
  in AGENTS.md as failing during restore with no diagnostics) or update AGENTS.md to
  reflect the resolved state.
- [ ] Run integration tests (Task 6) when Docker is available — tests build but require
  Docker to execute.
- [ ] Run spec-driven-eval against M1 spec to grade completion.

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

## Session log

- **2026-07-03** — M1 kickoff. Explored scaffold (20 empty projects, 4 architecture
  diagrams, no code, no refs, no packages). Ran the tlc-spec-driven Specify → Discuss →
  Design → Tasks flow. Captured 6 gray-area decisions via user discussion. Wrote
  `.specs/project/`, `.specs/codebase/`, `.specs/features/m1-mvp-tool-gateway/`.
- **2026-07-03** — M1 implementation. Executed tasks 1–7. All builds pass. Unit tests: 5/5
  ToolRegistry, 4/4 Audit, 8/8 ToolRouter = 17 total. Integration tests build but require
  Docker to execute. Updated README, STATE.