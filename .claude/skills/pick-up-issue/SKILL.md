---
name: pick-up-issue
description: >-
  Find an unclaimed open GitHub issue in this repository and take it end-to-end to a
  ready-for-review pull request. Use this whenever the user wants to pick up / grab / claim /
  start the next available issue, work down the backlog, or asks something like "what should I
  work on next?" — especially for `claude`-labeled issues, but also when they name a specific
  issue to implement (e.g. "pick up #74"). It auto-selects the highest-priority eligible issue
  (claude-labeled first, bugs before enhancements, oldest first), skips any issue that already
  has an open PR or unmet prerequisites, nails down the approach when there's ambiguity, then
  implements, tests, and opens the PR. Supports a dry-run/preview mode ("just tell me what
  you'd pick") that stops after proposing the approach. Do NOT use it for creating, triaging,
  commenting on, or closing issues, or for ad-hoc changes the user is already directing
  step-by-step.
---

# Pick Up an Issue

Pull the next worthwhile issue off this repo's backlog and carry it all the way to a
ready-for-review PR — without grabbing work that's already taken or can't be done yet.

The selection runs unattended (auto-pick, no "which one?" prompt). The interactivity lives at
the **approach** stage: once an issue is chosen, you confirm the plan before writing code, but
only when the issue is genuinely ambiguous, large, or has a real design fork. Trivial,
unambiguous fixes don't need a round-trip — just state the plan briefly and go.

All GitHub work uses the `mcp__github__*` tools (the `gh` CLI is not available here).

## Workflow

1. Identify the repo and gather candidates
2. Exclude the ineligible (taken or blocked)
3. Rank and auto-pick the top issue
4. Understand the issue and read the right docs
5. Agree on the approach (interactive only when it adds value)
6. Implement to the project's standards
7. Verify (build, tests, lint)
8. Commit, push, open the PR
9. Offer to watch the PR

---

### 1. Identify the repo and gather candidates

Derive `owner`/`repo` from the origin remote (`git remote get-url origin`) rather than
hardcoding — this skill should keep working if the repo is renamed or forked.

List the open issues. The `claude` label is the priority signal, so pull those first; keep the
rest as a fallback (see step 3). `claude` is a *priority*, not a hard filter — only fall back to
non-`claude` issues if no `claude` one is eligible, unless the user explicitly says "claude only".

### 2. Exclude the ineligible

Starting work that's already claimed or can't proceed wastes effort and risks duplicate PRs, so
filter the candidate list down before ranking.

**Taken (already has an open PR).** Detect this two ways, because neither alone is reliable here:
- Run a search such as `repo:OWNER/REPO is:issue is:open -linked:pr` to drop issues GitHub has
  formally linked to a PR.
- GitHub only auto-links PRs that use closing keywords, and this repo's PRs don't always do that.
  So **also** list the open PRs and scan each title + body for issue references (`#N`,
  `closes/fixes/resolves #N`). Treat any referenced still-open issue as taken.

**Blocked (unmet prerequisites).** Read each remaining candidate's body and recent comments for
dependency signals and drop the ones that genuinely can't proceed yet:
- explicit blockers — "blocked by #N", "depends on #N", "after #N is implemented", "needs … first",
  "mock the design first" (e.g. an issue that says "after proper JWTs are implemented", or
  "Need to mock the design first").
- "follow-up to #N" / "after #N" where **#N is still open**. If #N is already merged/closed, it's
  not a blocker.

When a prerequisite is clearly unmet, exclude the issue. When it's ambiguous, keep it but note the
concern so it surfaces during the approach step rather than silently dropping real work.

### 3. Rank and auto-pick

Sort the eligible candidates by this precedence and take the top one — **don't ask which to pick**:

1. Has the `claude` label (claude-labeled first)
2. Is a `bug` (bugs before enhancements / other types)
3. Oldest `created_at` first
4. Lowest issue number (final tiebreak)

Then briefly announce the choice so the decision is transparent: the issue number + title, the
one-line reason it won, and a short note of anything you excluded as taken or blocked.

- **User named a specific issue** ("pick up #74"): skip selection and use it. Still run a quick
  eligibility check and flag it if it looks taken or blocked before diving in.
- **Dry-run / preview** ("just tell me what you'd pick", "what's next, don't start it"): do steps
  1–5 and stop after proposing the approach. Make no changes.
- **Nothing eligible**: say so plainly, list what was excluded and why, and stop. Do not invent
  work or pick a blocked/taken issue anyway.

### 4. Understand the issue and read the right docs

Read the full issue (body + comments), then explore the actual code paths it points at before
forming a plan — the issue's suggested resolution is a starting point, not gospel.

`CLAUDE.md` is the project's source of truth for coding standards; it's normally auto-loaded, but
make sure you've internalized it. Then read the doc(s) for the affected area, because the project
requires it:

- Backend / API / EF Core / domain / data-access work → `docs/backend.md`
- Frontend / Svelte / UI work → `docs/frontend.md`
- Game features / mechanics / battle / balance → `docs/game-design.md`

Battle logic lives in **both** the frontend and backend and must stay consistent; if the issue
touches it, read both docs.

### 5. Agree on the approach (interactive only when it adds value)

The goal is to avoid building the wrong thing without pestering the user over trivial fixes.

- **Clear, contained, one obvious approach** → state a brief plan (a few bullets) and proceed.
- **Ambiguous, large/multi-part, or a real design fork** → surface the options and ask with
  `AskUserQuestion` (or plain questions) before writing code. Examples that warrant a decision:
  an issue offering "remove *or* implement this", or one that explicitly asks you to "research a
  better solution". For a large issue, propose what's in this PR vs. a follow-up and confirm the
  scope. For substantial changes, consider proposing a plan and waiting for sign-off.

### 6. Implement to the project's standards

Follow `CLAUDE.md` and the area docs. The standards that bite most often:

- DRY; **always** use braces on `if`/`else`/`while` blocks; leave no dead code behind (remove
  now-unused variables/functions/imports when you replace functionality).
- The docs are the source of truth even where existing code disagrees. Don't copy a pattern that
  contradicts them — and if you spot such code, raise it as a follow-up (a note in the PR or a
  follow-up issue), don't silently propagate it.
- **Tests are required**: unit tests for all domain logic (cover edge cases and error conditions),
  integration tests for interactions between dependencies, e2e only for critical user paths. If you
  touch battle logic, mirror the same test scenarios and expected results in **both** the frontend
  and backend suites.
- If you make an important design decision, document it concisely in the right `docs/` file
  ("Important Design Decisions" or the relevant feature section) — but don't over-document routine
  bug fixes.

### 7. Verify before you push

Run what CI runs, scoped to what you changed. Integration tests reuse the Postgres (`:5499`) and
Redis (`:6399`) containers the session-start hook already started (see `.container-info.json`).

- **Backend** (repo root): `dotnet build`, then `dotnet test --no-build --no-restore`.
- **Frontend** (`UI/new-svelte`, after `npm ci` if needed): `npm run lint` and `npm run test:unit:ci`.
- **E2E** (Playwright) is heavyweight — run it only for critical-path UI changes.

Report failures honestly and fix them. Don't open a PR on red — a green local run is the whole
point of doing this before pushing.

### 8. Commit, push, open the PR

- Work on the session's current branch; don't switch branches without permission. If you're somehow
  on `main`/`master`, create a branch first.
- Write a clear commit message that summarizes the change and references the issue (e.g.
  `Fix silent exception swallowing in DataProviderSynchronizer (#73)`). Follow the repo's and
  environment's commit/PR footer conventions.
- Push with `git push -u origin <branch>`; on network errors retry with backoff.
- Open a **ready-for-review** PR (not a draft) via `mcp__github__create_pull_request`. The body
  should have a summary, how it addresses the issue, a test plan/checklist, and a `Closes #N` line
  so the issue auto-closes on merge.

### 9. Offer to watch the PR

After opening the PR, proactively offer to watch it for CI failures and review comments via
`subscribe_pr_activity`, then let the user decide.

---

## Worked example (illustrative)

Eligible after filtering: a `claude` `bug` from June 2, a `claude` `bug` from June 4, and a
`claude` `enhancement` from June 1. Ranking picks the **June-2 bug** — `claude` + `bug` beats the
enhancement, and it's older than the June-4 bug. If that issue's body said "depends on #40" and #40
were still open, it would have been excluded as blocked and the June-4 bug would win instead.

## Guardrails

- This skill picks up and implements issues. It is **not** for creating, triaging, commenting on,
  or closing issues.
- Never fabricate work when nothing is eligible, and never start a blocked or already-claimed issue.
- Selection is intentionally not announced on the issue itself (no "claiming" comment). If parallel
  sessions start colliding on the same top pick, revisit this and consider a self-assign/comment.
