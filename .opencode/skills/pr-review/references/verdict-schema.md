# Verdict Schema

The PR review verdict is a single JSON object. Step 7 of the skill emits exactly this object; Step 8 renders it to markdown. The GitHub Action parses this object (from the model's response) and posts the rendered markdown as a PR comment.

## Schema

```json
{
  "verdict": "approved" | "changes_requested" | "blocked",
  "confidence": 0.0,
  "feature_spec": "m1-mvp-tool-gateway" | null,
  "diff_surface": ["relative/path/to/file.cs", "..."],
  "gates": {
    "protected_paths": "pass" | "fail",
    "violations": [
      { "file": "...", "rule": "...", "reason": "..." }
    ]
  },
  "requirements": [
    {
      "id": "M1-R3",
      "status": "satisfied" | "gap" | "untouched",
      "evidence": "src/runtime/McpGuard.ToolRegistry/ConfigToolRegistry.cs:42",
      "test": "covered" | "gap"
    }
  ],
  "task_traceability": [
    {
      "task": "T-001",
      "status": "matches_diff" | "rogue" | "drift",
      "detail": "one-line explanation"
    }
  ],
  "gaps": [
    { "file": "src/runtime/McpGuard.ToolRouter/IMcpClientFactory.cs", "line": 12,
      "severity": "minor" | "major", "message": "concrete actionable gap" }
  ],
  "recommended_action": "merge" | "address_gaps_then_merge" | "request_changes" | "block_and_rework"
}
```

## Field semantics

### `verdict` (required)
The machine-readable outcome. Exactly one of:
- `approved` — protected paths clean, no requirement gaps, no rogue or drift.
- `changes_requested` — at least one `gap` in requirements or tests, or a `rogue`/`drift` in task traceability.
- `blocked` — the protected-paths gate failed. Never set for any other reason. When `verdict == "blocked"`, the remaining fields after `gates` may be empty arrays; the judge stops after the gate.

### `confidence` (required)
A float in `[0.0, 1.0]` expressing how confident the judge is in the verdict. Below 0.7, the judge should default to `changes_requested` with a note that it is uncertain. Confidence reflects evidence quality: many `file:line` citations with clear traces → high; thin or ambiguous evidence → low.

### `feature_spec` (required)
The matched feature spec folder name (e.g. `"m1-mvp-tool-gateway"`), or `null` if no spec matched. When `null`, the verdict is `approved` with a graceful no-op note — v1 does not judge conventions without a spec.

### `diff_surface` (required)
The list of files in the PR diff, relative to the repo root. Captured in Step 1.

### `gates` (required)
The deterministic gate results. In v1 only `protected_paths` is enforced.
- `protected_paths`: `"pass"` or `"fail"`.
- `violations`: array of `{ "file", "rule", "reason" }` from `scripts/check_protected_paths.sh`. Empty when `protected_paths == "pass"`.

### `requirements` (required when feature_spec is non-null)
One entry per acceptance criterion in the matched `spec.md` (e.g. M1-R1 through M1-R10).
- `id`: the requirement identifier as written in the spec.
- `status`:
  - `satisfied` — the diff addresses it; `evidence` is a `file:line` within the diff surface.
  - `gap` — the diff touches the requirement's area but does not satisfy it. The missing piece goes in `gaps`.
  - `untouched` — not in this PR's scope. A PR doesn't have to deliver the whole spec at once. `untouched` is not a failure.
- `evidence`: `file:line` citation within the diff surface, or `null`/omitted when `status != "satisfied"`.
- `test`:
  - `covered` — a test in the diff asserts this requirement at the required level (unit for pure logic, integration for HTTP/contract/persistence).
  - `gap` — the requirement is satisfied by production code in the diff but no test in the diff asserts it.
  - omitted when `status == "untouched"`.

### `task_traceability` (required when feature_spec is non-null)
One entry per task in the matched `tasks.md`, plus one `rogue` entry for any diff behavior that maps to no task.
- `task`: the task identifier from `tasks.md`, or `"?"` for rogue builds.
- `status`:
  - `matches_diff` — the task maps cleanly to changes in the diff.
  - `rogue` — the diff introduces behavior not in any task.
  - `drift` — a task marked done in `tasks.md` with no diff evidence, or a task not started but the diff or PR description claims it.
- `detail`: one-line explanation.

### `gaps` (required)
An array of objects, one per actionable finding. Each gap is posted as an **inline review comment** anchored to its `file`/`line` in the diff (Step 9a), so it must be specific enough to act on.

Each entry:
- `file` (required) — path within the diff surface to anchor the inline comment to.
- `line` (required) — line number in the PR head version of `file` that the comment anchors to. Must be a line present in the diff hunk. If the gap is about a missing piece, anchor to the first line of the relevant hunk and phrase the comment as "missing here".
- `severity` (required) — `"minor"` (nits, contract cleanup, docs — does not block merge) or `"major"` (unmet acceptance criteria, missing tests — blocks merge).
- `message` (required) — concrete, actionable text. Cite the file and what is missing. "Tests are weak" is not a gap; `{"file": "...", "line": 42, "severity": "major", "message": "M1-R3 is satisfied by ConfigToolRegistry.cs:42 but no test in the diff asserts tools/list returns only approved tools."}` is.

When `verdict == "approved"`, `gaps` is `[]` and no inline comments are posted.

### `recommended_action` (required)
The human-facing version of the verdict. Maps 1:1 to `verdict` with a severity split for `changes_requested`:
- `block_and_rework` ↔ `blocked`
- `request_changes` ↔ `changes_requested` with serious gaps
- `address_gaps_then_merge` ↔ `changes_requested` with minor gaps the author can fix quickly
- `merge` ↔ `approved`

## Validity rules

- The JSON must be valid and parseable on its own. No surrounding prose in Step 7.
- `verdict == "blocked"` ⇒ `gates.protected_paths == "fail"` and `recommended_action == "block_and_rework"`.
- `verdict == "approved"` ⇒ `gaps == []` and `recommended_action == "merge"`.
- `confidence < 0.7` ⇒ `verdict` should be `changes_requested` unless the PR is trivially clean.
- Every `satisfied` requirement must have a non-empty `evidence` in the diff surface. No evidence ⇒ `gap`.
- Every entry in `gaps` must have `file`, `line`, `severity`, and `message`. `line` must be a line present in the diff hunk for `file` (the PR head version).

## Example (approved, no spec matched)

```json
{
  "verdict": "approved",
  "confidence": 0.95,
  "feature_spec": null,
  "diff_surface": ["README.md"],
  "gates": { "protected_paths": "pass", "violations": [] },
  "requirements": [],
  "task_traceability": [],
  "gaps": [],
  "recommended_action": "merge"
}
```

## Example (changes_requested)

```json
{
  "verdict": "changes_requested",
  "confidence": 0.82,
  "feature_spec": "m1-mvp-tool-gateway",
  "diff_surface": ["src/runtime/McpGuard.ToolRegistry/ConfigToolRegistry.cs"],
  "gates": { "protected_paths": "pass", "violations": [] },
  "requirements": [
    {
      "id": "M1-R3",
      "status": "satisfied",
      "evidence": "src/runtime/McpGuard.ToolRegistry/ConfigToolRegistry.cs:42",
      "test": "gap"
    }
  ],
  "task_traceability": [
    { "task": "T-002", "status": "matches_diff", "detail": "implements the allowlist filter" }
  ],
  "gaps": [
    { "file": "tests/McpGuard.Gateway.Api.Tests/EndToEndTests.cs", "line": 42,
      "severity": "major",
      "message": "M1-R3 is satisfied by ConfigToolRegistry.cs:42 but no test in the diff asserts tools/list returns only approved tools." }
  ],
  "recommended_action": "address_gaps_then_merge"
}
```

## Example (blocked)

```json
{
  "verdict": "blocked",
  "confidence": 1.0,
  "feature_spec": null,
  "diff_surface": ["src/runtime/McpGuard.Gateway.Api/bin/Debug/net10.0/McpGuard.Gateway.Api.dll"],
  "gates": {
    "protected_paths": "fail",
    "violations": [
      {
        "file": "src/runtime/McpGuard.Gateway.Api/bin/Debug/net10.0/McpGuard.Gateway.Api.dll",
        "rule": "generated_build_output",
        "reason": "Generated .NET build output under bin/ must not be committed."
      }
    ]
  },
  "requirements": [],
  "task_traceability": [],
  "gaps": [
    { "file": "src/runtime/McpGuard.Gateway.Api/bin/Debug/net10.0/McpGuard.Gateway.Api.dll", "line": 1,
      "severity": "major",
      "message": "Remove the committed bin/ output and add it to .gitignore if not already present." }
  ],
  "recommended_action": "block_and_rework"
}
```