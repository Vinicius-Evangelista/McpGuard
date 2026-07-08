# Spec Matching Rules

Step 3 of the skill maps the PR diff surface to a feature spec under `.specs/features/<feature>/`. A feature matches when the diff surface overlaps the feature's expected source areas. This file defines the rules.

## Why matching matters

The judge grades the diff against the sanctioned contract. The contract lives in `.specs/features/<feature>/spec.md` (acceptance criteria) and `tasks.md` (atomic tasks). Without a match, there is no contract to judge against, and v1 emits a graceful no-op (`verdict: approved`, `feature_spec: null`).

## Matching procedure

### 1. Enumerate candidate features

List every folder directly under `.specs/features/`:

```bash
ls -d .specs/features/*/ 2>/dev/null
```

Each folder is a candidate. The folder name is the feature id (e.g. `m1-mvp-tool-gateway`).

### 2. For each candidate, read its source-area hints

A feature spec may declare its expected source areas explicitly or implicitly:

- **Explicit hints** — `spec.md` or `tasks.md` may name paths or path prefixes in the requirement text or task descriptions (e.g. "src/runtime/McpGuard.Gateway.Api/**", "tests/McpGuard.ToolRegistry.Tests/**"). Collect every path prefix mentioned.
- **Implicit hints** — if no explicit paths are found, infer from the feature name and the project structure:
  - `m1-mvp-tool-gateway` ↔ `src/runtime/McpGuard.Gateway.Api/**`, `src/runtime/McpGuard.ToolRegistry/**`, `src/runtime/McpGuard.ToolRouter/**`, `src/runtime/McpGuard.Audit/**`, `src/runtime/McpGuard.SampleTools.Server/**`, `tests/McpGuard.*.Tests/**`.
  - For other features, fall back to `src/**` and `tests/**` as a broad match, then narrow using the requirement text.

### 3. Compute overlap with the diff surface

For each candidate, count how many files in the diff surface fall under any of its source-area prefixes. The candidate with the highest overlap wins. A tie-breaker prefers the longer-prefix match (more specific).

### 4. Threshold

- Overlap >= 1 file ⇒ the feature matches. Even a single file in the feature's area is enough to make it the active spec.
- Overlap == 0 for all candidates ⇒ no match. Emit `verdict: approved`, `feature_spec: null`, and stop. Do not run a conventions sweep in v1.

### 5. Load the contract

For the matched feature, read:
- `.specs/features/<feature>/spec.md` — the acceptance criteria (the `M1-Rn` requirements table).
- `.specs/features/<feature>/tasks.md` — the atomic tasks.
- `.specs/features/<feature>/evaluations/*.md` — the most recent prior evaluation, if any. Used for context only: if a prior eval marked a requirement satisfied and this PR doesn't touch that area, the requirement stays satisfied (mark `untouched` in this PR's verdict, not `gap`).

### 6. Handle multiple matches

If two or more features have non-zero overlap, pick the one with the highest overlap and note the others in the `notes` field of the verdict markdown: "Also touches feature X (overlap: N files); review focused on Y."

If overlap is exactly equal, prefer the feature whose `spec.md` was most recently modified.

## Path-prefix rules

A diff file `f` matches a source-area prefix `p` when:
- `f == p` (exact match), or
- `p` ends with `/**` and `f` starts with the prefix with `/**` stripped, or
- `f` starts with `p` and the next character is `/`.

Examples:
- `src/runtime/McpGuard.ToolRegistry/ConfigToolRegistry.cs` matches `src/runtime/McpGuard.ToolRegistry/**`.
- `tests/McpGuard.ToolRegistry.Tests/ConfigToolRegistryTests.cs` matches `tests/McpGuard.ToolRegistry.Tests/**`.
- `README.md` does not match `src/runtime/McpGuard.ToolRegistry/**`.

## Non-source files

Docs, `.specs/`, `AGENTS.md`, `README.md`, `Directory.Packages.props`, `docs/*.mmd` are not part of any feature's source area by default. A PR that only touches these files does not match a feature spec — emit `approved` with `feature_spec: null`. Exception: if a feature's `tasks.md` explicitly lists a doc file as part of the feature (e.g. "update README M1 section"), include that file in the feature's area.

## Examples

### Example 1 — single feature match

Diff surface:
```
src/runtime/McpGuard.ToolRegistry/ConfigToolRegistry.cs
src/runtime/McpGuard.ToolRegistry/IToolRegistry.cs
tests/McpGuard.ToolRegistry.Tests/ConfigToolRegistryTests.cs
```

Candidate features:
- `m1-mvp-tool-gateway` — area includes `src/runtime/McpGuard.ToolRegistry/**` and `tests/McpGuard.ToolRegistry.Tests/**`. Overlap: 3 files.

Result: `feature_spec: "m1-mvp-tool-gateway"`. Load its `spec.md` and `tasks.md`.

### Example 2 — no match

Diff surface:
```
README.md
docs/04-request-flow.mmd
```

No feature's source area includes these. Result: `feature_spec: null`, `verdict: approved`, note "no spec matched; spec-driven review not applicable."

### Example 3 — multiple matches

Diff surface:
```
src/runtime/McpGuard.Gateway.Api/Program.cs
src/runtime/McpGuard.ToolRegistry/ConfigToolRegistry.cs
```

If two features both claim `src/runtime/McpGuard.ToolRegistry/**`, pick the one with the higher total overlap across the diff and note the other in the markdown.

## Anti-patterns

- Matching a feature just because the PR title mentions it — match on the diff surface, not the title.
- Marking a feature as matched when overlap is 0 — no match means no match.
- Loading every feature's spec "just in case" — only load the matched feature's contract. Context is expensive.
- Re-grading a prior-evaluated requirement as `gap` when the PR doesn't touch it — that is `untouched`, not `gap`.