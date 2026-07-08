---
name: pr-review
description: Reviews a GitHub PR for McpGuard against the spec-driven development output (.specs/features/**/spec.md acceptance criteria and tasks.md atomic tasks) plus a protected-paths gate, then posts a structured verdict comment to the PR via gh. Use when asked to "review this PR", "judge a PR", "review PR N", "check open PRs", "review my changes against the spec", or before merging a PR. Do NOT use for final spec-vs-implementation grading (use spec-driven-eval), for general code review with no matching spec, or for non-McpGuard repos.
license: CC-BY-4.0
metadata:
  author: Vinicius Evangelista - github.com/Vinicius-Evangelista
  version: '1.0.0'
---

# PR Review (Separate Judge, Local-Driven)

You are the **separate judge** for a McpGuard GitHub PR. The same agent that wrote the code should not be the one that says it is done. Your job is to fetch the PR diff, compare it against the sanctioned spec-driven contract, emit a structured verdict, and post it as a PR comment via `gh`. You do not write or fix code, and you do not restate what the diff obviously does.

This skill runs locally. It uses `git` and `gh` (the GitHub CLI) to fetch the PR, compute the diff, and post the comment. It uses no CI, no GitHub Actions, and no secrets beyond the `gh` token you already have authenticated with.

The contract is McpGuard's spec-driven development output under `.specs/`:
- `.specs/features/<feature>/spec.md` — the acceptance criteria (e.g. M1-R1..M1-R10).
- `.specs/features/<feature>/tasks.md` — the atomic tasks the PR should be implementing.
- `.specs/features/<feature>/evaluations/*.md` — prior graded runs (context only).

You do not grade on generic taste. You grade on whether the diff fulfills what was planned, and whether it touched files it should not have.

## Prerequisites

- `gh` installed and authenticated (`gh auth status` shows logged in). If not, stop and tell the user to run `gh auth login`.
- Current working directory is the McpGuard repo root (contains `AGENTS.md`, `.specs/`, `.opencode/`).
- `git` is on a branch that has the remote checked out, so `gh pr list` / `gh pr view` work.

## When to run

Run when the user asks to review a PR. The user may give you:
- A PR number: "review PR 42" → `PR_REF=42`.
- A PR URL: "review https://github.com/Vinicius-Evangelista/McpGuard/pull/42" → extract the number.
- An open PR list: "check open PRs" / "review open PRs" → list them and ask which to review.
- No reference: ask which PR to review (list open PRs first to make it easy).

## Workflow

Execute the steps in order. Steps 2 and 3 are deterministic gates; if they fail, stop and emit a `blocked` verdict. Steps 4–6 are judgment against the spec. Step 7 emits the verdict. Step 8 renders it. Step 9 posts it.

### Step 1 — Fetch the PR diff

Identify the PR. If the user gave a number, use it. If they said "open PRs", run `gh pr list` and show the table, then ask which one.

Fetch the metadata and diff:

```bash
PR_NUMBER=<number>
gh pr view "$PR_NUMBER" --json number,title,baseRefName,headRefName,headRefOid,state > pr-meta.json
BASE_REF=$(jq -r .baseRefName pr-meta.json)
HEAD_SHA=$(jq -r .headRefOid pr-meta.json)
```

Fetch the diff surface and full diff. Use the base ref and head SHA so the diff matches what GitHub shows:

```bash
git fetch origin "$BASE_REF" --depth=1
git diff --name-only "origin/$BASE_REF..$HEAD_SHA" > diff-surface.txt
git diff "origin/$BASE_REF..$HEAD_SHA" > diff.patch
```

If the PR is from a fork, `gh pr diff` is more reliable than the local diff (which may not have the fork's commits). Fall back to:

```bash
gh pr diff "$PR_NUMBER" > diff.patch
git diff --name-only "origin/$BASE_REF..$HEAD_SHA" > diff-surface.txt
```

If `diff-surface.txt` is empty after that, parse the file list out of `diff.patch` headers (`^diff --git a/...`).

Record the diff surface; it is the primary search scope for all evidence later.

### Step 2 — Protected paths gate (deterministic, cheap)

Run the skill's script against the diff surface:

```bash
bash .opencode/skills/pr-review/scripts/check_protected_paths.sh < diff-surface.txt > gates.json
```

It exits 1 if any file violates the protected-paths policy. If it exits 1, parse `gates.json` and emit `verdict: blocked` with the violations. Do not run the remaining steps — a blocked PR does not get a spec review.

Protected paths (enforced by the script):
- `*/bin/*` and `*/obj/*` — generated .NET build output.
- `.env*` — secrets, never committed.
- `appsettings.Production.json` and non-development `appsettings.*.json` overrides — real-secrets configs (local dev defaults like `appsettings.Development.json` are allowed).
- `Directory.Packages.props` — central NuGet versions; a change here without a paired source change in the same diff is flagged.

### Step 3 — Match the PR to a feature spec

Load `references/spec-matching.md` for the full matching rules. Summary:

- Scan `.specs/features/*/` for `spec.md` files.
- A feature matches if the diff surface overlaps the feature's expected source areas (path-prefix rules in the reference).
- If exactly one feature matches, that is the active spec. Load its `spec.md`, `tasks.md`, and the most recent `evaluations/*.md` (for prior context only — do not copy its grade).
- If multiple features match, pick the one with the highest diff-surface overlap; note the others.
- If no feature matches, emit `verdict: approved` with `feature_spec: null` and a note: "no spec matched; spec-driven review not applicable. Manual review required for conventions." Then skip to Step 8 with the gates result.

### Step 4 — Load the sanctioned contract

Read the matched `spec.md` and `tasks.md`. The acceptance criteria (e.g. M1-R1..M1-R10) define the sanctioned requirement set. The tasks in `tasks.md` define what was planned to be done. Prior `evaluations/*.md` files tell you what has already been graded — if a prior eval marked M1-R3 as satisfied and this PR doesn't touch that area, it stays satisfied (mark `untouched` in this PR's verdict, not `gap`).

Also read `AGENTS.md` — it is short (81 lines) and carries the project conventions (snake_case tests, no mocking libs, `Async` suffix, `McpGuard.<Area>` namespaces, central package versions, conventional commits). You won't run a full conventions sweep in v1, but you need it for context and for the test-coverage check in Step 6.

### Step 5 — Acceptance-criteria match (I-checks, scoped to the PR)

For each requirement in `spec.md`, determine from the diff surface only:

- **satisfied** — the diff addresses it, with `file:line` evidence inside the diff surface.
- **gap** — the diff touches the requirement's area but does not satisfy it. Give a concrete reason and the missing piece.
- **untouched** — not in this PR's scope. A PR does not have to deliver the whole spec at once. `untouched` is not a failure.

This is per-PR, not per-spec. A requirement already satisfied in a prior PR and untouched here stays `untouched` (or `satisfied` if you cite prior evidence — prefer `untouched` to keep the verdict focused on this PR).

Apply the conjunction rule from spec-driven-eval: if a requirement lists multiple items joined by "and"/","/with/including, treat each named field or entity as its own check. Confirming the parent method is called does not prove a named field is in the payload — open the constructed object at `file:line` and verify.

### Step 6 — Task traceability and test-coverage (T-checks)

Cross-reference the diff against `tasks.md`:

- **matches_diff** — a task in `tasks.md` maps cleanly to changes in the diff.
- **rogue** — the diff introduces behavior not in any task. Flag it; the PR did something unplanned.
- **drift** — a task marked done in `tasks.md` with no diff evidence, or a task not started but the diff or PR description claims it. Flag it.

For each requirement the diff satisfies, check the test coverage in the diff:
- **covered** — there is a test in the diff asserting that requirement, at the required level (unit for pure logic, integration for HTTP/contract/persistence side-effects).
- **gap** — the requirement is satisfied by production code in the diff but no test in the diff asserts it. This is the spec-driven-eval T-check, scoped to the PR.

When a test file is in the diff, verify it follows McpGuard conventions from `AGENTS.md`: snake_case names, no `Moq`/`NSubstitute` imports, hand-written fakes + Object Mother, `Async` suffix on async methods. A violation here is a `gap` with the convention rule cited.

### Step 7 — Emit the structured verdict

Output MUST be valid JSON matching `references/verdict-schema.md`. No prose around it in this step — just the JSON block.

```json
{
  "verdict": "approved" | "changes_requested" | "blocked",
  "confidence": 0.0,
  "feature_spec": "m1-mvp-tool-gateway",
  "diff_surface": ["..."],
  "gates": { "protected_paths": "pass" | "fail", "violations": [] },
  "requirements": [
    { "id": "M1-R3", "status": "satisfied" | "gap" | "untouched",
      "evidence": "file:line", "test": "covered" | "gap" }
  ],
  "task_traceability": [
    { "task": "T-001", "status": "matches_diff" | "rogue" | "drift", "detail": "..." }
  ],
  "gaps": ["concrete actionable gap 1"],
  "recommended_action": "merge" | "address_gaps_then_merge" | "request_changes" | "block_and_rework"
}
```

Verdict mapping:
- `blocked` — protected-paths gate failed. Never anything else.
- `changes_requested` — any `gap` in requirements or tests, or any `rogue`/`drift` in task traceability.
- `approved` — no gaps, no rogue, no drift, protected paths clean.

`recommended_action` is the human-facing version:
- `block_and_rework` ↔ `blocked`.
- `request_changes` ↔ `changes_requested` with serious gaps.
- `address_gaps_then_merge` ↔ `changes_requested` with minor gaps the author can fix quickly.
- `merge` ↔ `approved`.

### Step 8 — Render as markdown

Render the verdict as a markdown PR comment. Structure:

```
## PR Review — Separate Judge

**Verdict:** <verdict> · **Confidence:** <confidence> · **Recommended action:** <recommended_action>
**Feature spec:** <feature_spec or "none matched">
**PR:** #<number> <title>

### Gates
| Gate | Status |
|------|--------|
| Protected paths | <pass/fail> |

<violations table if any>

### Requirements
| ID | Status | Evidence | Test |
|----|--------|----------|------|
| M1-R1 | satisfied | src/...:42 | covered |
| M1-R3 | gap | — | gap |

### Task traceability
| Task | Status | Detail |
|------|--------|--------|
| T-001 | matches_diff | ... |
| T-??? | rogue | diff adds behavior not in any task |

### Gaps
1. <concrete gap>
2. <concrete gap>

### Notes
<anything the author should know>
```

Keep the markdown tight. No preamble, no "here is your review". The verdict line is the first thing the author reads.

### Step 9 — Post the comment

Write the markdown to a temp file and post it:

```bash
COMMENT_FILE=$(mktemp)
cat > "$COMMENT_FILE" <<'EOF'
<rendered markdown>
EOF
gh pr comment "$PR_NUMBER" --body-file "$COMMENT_FILE"
rm -f "$COMMENT_FILE"
```

Confirm the comment URL in your final reply to the user. If `gh pr comment` fails (e.g. no network, no permission), print the rendered markdown so the user can paste it manually.

If the user explicitly asks you NOT to post, skip Step 9 and just show the verdict markdown in your reply.

## Boundaries

- You are read-only over the subject. Never modify the code under review — not to fix a failing gate, not to "help", not even for trivial type errors. A red gate stays red; record it and put the fix in the `gaps` list.
- You judge against the sanctioned spec, not generic taste. "I would have written it differently" is not a gap.
- A PR that doesn't touch the spec's area is not a failure. Emit `approved` with `feature_spec: null` and stop.
- Do not run build or tests in v1. The deterministic gate is protected-paths only. Build/test gates may be added later.
- Evidence or zero. Every `satisfied` must cite `file:line` within the diff surface. No evidence ⇒ mark `gap`, not `satisfied`.
- Search before zero. Before marking `gap` for "not found", record the search performed — grep terms, files inspected. A `gap` means searched and genuinely missing, not "did not look".

## Anti-patterns

- Restating the diff instead of judging it against the spec.
- Marking a requirement `satisfied` because the diff touches the area, without `file:line` evidence the requirement is met.
- Marking a requirement `gap` without showing what was searched and why it is missing.
- Re-grading requirements already satisfied in prior PRs and untouched here — that is `untouched`, not `gap`.
- Running a generic conventions sweep when no spec matched — v1 is spec-driven or graceful no-op.
- Modifying the code under review to make a gate pass.
- Emitting prose instead of the JSON verdict in Step 7.
- Posting the verdict as approved without confidence >= 0.7 — if confidence is lower, default to `changes_requested` with a note that the judge is uncertain.
- Running `gh pr review --approve` or `--request-changes` — v1 only posts a comment, never sets the review state. Opt-in later once verdict quality is trusted.