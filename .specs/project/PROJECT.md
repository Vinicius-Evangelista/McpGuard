# McpGuard — Project Vision

## What

McpGuard is an open-source .NET 10 MCP (Model Context Protocol) gateway. It sits between
MCP clients (AI agents, IDEs, chat clients) and downstream MCP servers (GitHub, Jira,
internal APIs, databases, etc.), enforcing governance over which tools a client may discover
and call.

## Why

The MCP ecosystem makes it trivial to expose powerful tools to LLMs, but production usage
demands a control point: operators need to allowlist tools, audit every call, and — later —
apply DLP, redaction, secrets brokering, and approval workflows before a tool ever executes.
McpGuard is that control point. It is a transparent proxy: clients speak standard MCP
JSON-RPC and never know McpGuard is in the path.

## North Star

> A client must never be able to discover or call a tool that the operator has not approved,
> and every decision the gateway makes is recorded.

## Scope boundaries

In scope: MCP JSON-RPC gateway, tool allowlist, execution guard, downstream routing, audit,
DLP context, redaction, secrets brokering, auth (OIDC/OAuth), admin control plane, health,
observability.

Out of scope: hosting LLMs, generating tool calls, replacing the MCP protocol, building a
general API gateway, re-implementing the official MCP C# SDK.

## Principles

- **Transparent proxy first.** McpGuard is a gateway, not a tool host. Tools live downstream.
  The gateway approves, routes, observes — it does not execute tool logic itself.
- **Pluggable stores.** Every registry/policy/secrets surface is behind an interface. M1 uses
  config; later milestones swap in the admin control plane without changing the runtime
  pipeline.
- **Audit everything.** Every initialize, discovery, allowed call, and blocked call is an
  event. No silent decisions.
- **SDK over hand-rolled.** Use the official `ModelContextProtocol` C# SDK (maintained with
  Microsoft) for JSON-RPC framing, Streamable HTTP, and session management. Own the policy
  layer, not the protocol layer.
- **Tests are first-class.** No mocking libraries. Fakes + Object Mother. Integration tests
  run the real downstream server in a container via Testcontainers.

## Tech stack

- .NET 10, ASP.NET Core, C# with nullable reference types and implicit usings.
- Official `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` NuGet packages.
- Central package management via `Directory.Packages.props`.
- xUnit for tests; Testcontainers for integration tests; `WebApplicationFactory` for in-process
  gateway hosting.
- Docker for the sample downstream MCP server fixture.

## Source of truth for architecture

- `docs/01-system-context.mmd` — system context (actors + external dependencies).
- `docs/02-runtime-components.mmd` — runtime component decomposition.
- `docs/03-control-plane-components.mmd` — control-plane component map.
- `docs/04-request-flow.mmd` — primary request-flow sequence (initialize → tools/list →
  tools/call with DLP, redaction, secrets, audit decision points).
- `AGENTS.md` — repository guidelines (build/test commands, coding style, commit conventions).