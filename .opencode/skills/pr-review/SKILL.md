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

Apply this gate inline against the diff surface (`diff-surface.txt`) — no script needed. Scan each path against the protected-paths rules below and collect violations.

Protected paths (flag any matching diff path):
- `*/bin/*` and `*/obj/*` — generated .NET build output.
- `.env*` — secrets, never committed (covers `.env`, `.env.local`, `.env.production`, ...).
- `appsettings.Production.json` and non-development `appsettings.*.json` overrides — real-secrets configs. `appsettings.json` (base) and `appsettings.Development.json` are allowed; any other `appsettings.<Env>.json` is flagged.
- `Directory.Packages.props` — central NuGet versions; a change here without a paired source change in the same diff is flagged.

If any path matches, emit `verdict: blocked` with the violations in `gates.violations` and do not run the remaining steps — a blocked PR does not get a spec review. Otherwise `gates.protected_paths = "pass"` and continue.

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
  "gaps": [
    { "file": "src/runtime/McpGuard.ToolRouter/IMcpClientFactory.cs", "line": 12,
      "severity": "minor" | "major", "message": "concrete actionable gap" }
  ],
  "recommended_action": "merge" | "address_gaps_then_merge" | "request_changes" | "block_and_rework"
}
```

Each entry in `gaps` carries `file` and `line` so it can be posted as an inline review comment on the exact line of the diff. When `verdict == "approved"`, `gaps` is `[]`.

Verdict mapping:
- `blocked` — protected-paths gate failed. Never anything else.
- `changes_requested` — any `gap` in requirements or tests, or any `rogue`/`drift` in task traceability.
- `approved` — no gaps, no rogue, no drift, protected paths clean.

`recommended_action` is the human-facing version:
- `block_and_rework` ↔ `blocked`.
- `request_changes` ↔ `changes_requested` with serious gaps.
- `address_gaps_then_merge` ↔ `changes_requested` with minor gaps the author can fix quickly.
- `merge` ↔ `approved`.

### Step 8 — Render the review

The review has two parts: a **summary comment** (one PR-level comment with the verdict) and a set of **inline review comments** (one per gap, anchored to the exact `file`/`line` in the diff).

#### 8a — Summary comment

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
<if no gaps: "None — no inline review comments.">
<if gaps: a one-line-per-gap index, each linking to the inline comment. Format:
1. [<severity>] `<file>:<line>` — <short message>
2. [<severity>] `<file>:<line>` — <short message>

See the inline review comments on the diff for details.

### Notes
<anything the author should know>
```

Keep the markdown tight. No preamble, no "here is your review". The verdict line is the first thing the author reads.

#### 8b — Inline review comments

For each entry in `gaps[]`, render an inline comment body. Each comment is anchored to its `file`/`line` in the diff. Body format:

```
**[<severity>] Gap — <short title>**

<message>

_Evidence: `<file>:<line>` · verdict: <verdict>_
```

`severity` is `minor` or `major`. Minor gaps are nits/contract cleanup/docs that don't block merge; major gaps are unmet acceptance criteria or missing tests. The summary comment's `Gaps` index links to these inline comments.

### Step 9 — Post the review

Posting has two steps: first the inline review comments (as a single pending GitHub review), then the summary comment. The inline comments must be posted as a review with `--request-changes` (when `verdict == "changes_requested"` or `"blocked"`) or `--comment` (when `verdict == "approved"`), so they land on the diff lines — not as a flat PR comment.

#### 9a — Post inline comments as a GitHub review

Build a JSON payload for `gh api` with one comment per gap, then submit it as a review. Write the payload to a temp file and post:

```bash
PR_NUMBER=<number>
HEAD_SHA=<head oid from Step 1>
REVIEW_FILE=$(mktemp --suffix=.json)

# Build the review payload. One "comments" entry per gap.
cat > "$REVIEW_FILE" <<EOF
{
  "commit_id": "$HEAD_SHA",
  "event": "REQUEST_CHANGES",
  "body": "See the inline comments for specific findings. Summary posted as a separate PR comment.",
  "comments": [
    { "path": "<gap.file>", "line": <gap.line>, "body": "<rendered inline comment body>" }
  ]
}
EOF

gh api repos/:owner/:repo/pulls/$PR_NUMBER/reviews --method POST --input "$REVIEW_FILE"
rm -f "$REVIEW_FILE"
```

Use `"event": "REQUEST_CHANGES"` when `verdict` is `changes_requested` or `blocked`; use `"event": "COMMENT"` when `verdict` is `approved` (gaps should be empty in that case, so this branch is rare).

**Line anchoring rules:**
- `gap.line` must be a line that exists in the diff hunk for `gap.file` (i.e. a line present in the PR head). If the gap is about a missing piece (no line to anchor to), anchor to the first line of the relevant hunk and phrase the comment as "missing here" rather than "this line is wrong".
- GitHub's review API uses `line` (the line in the new file) for single-line comments. For multi-line hunks, `line` is the last line of the range you want to highlight. Keep comments single-line-anchored unless the gap spans a clear range.
- If the file was deleted in the PR, inline comments cannot be posted on it — fall back to adding the gap to the summary comment's `Gaps` index with a note "(file deleted — see summary)".

#### 9b — Post the summary comment

Write the summary markdown (from 8a) to a temp file and post it as a regular PR comment:

```bash
COMMENT_FILE=$(mktemp)
cat > "$COMMENT_FILE" <<'EOF'
<rendered summary markdown>
EOF
gh pr comment "$PR_NUMBER" --body-file "$COMMENT_FILE"
rm -f "$COMMENT_FILE"
```

Confirm both the review URL and the summary comment URL in your final reply to the user. If `gh api` / `gh pr comment` fails (e.g. no network, no permission), print the rendered review payload and summary markdown so the user can post them manually.

If the user explicitly asks you NOT to post, skip Step 9 and just show the verdict markdown + inline comments in your reply.

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
- Running `gh pr review --approve` or `--request-changes` — the skill posts via `gh api .../reviews` with inline comments + a summary PR comment, never `gh pr review`.
- Dumping all findings into one big PR comment. Each gap is an inline review comment anchored to its `file`/`line`; the PR comment is only the summary.