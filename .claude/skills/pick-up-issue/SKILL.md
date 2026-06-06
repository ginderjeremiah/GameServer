---
name: pick-up-issue
description: >-
  Pick up an unclaimed open GitHub issue from this repository and take it end-to-end to a
  ready-for-review pull request. Use this when the user explicitly asks to pick up, grab, claim,
  or start a new issue to work on — e.g. "please pick up a new issue to work on", "grab the next
  open issue", "claim and start the top claude issue" — or when they name a specific issue to
  implement (e.g. "pick up #74"). It self-assigns the chosen issue, auto-selects the
  highest-priority eligible one (claude-labeled first, bugs before enhancements, oldest first),
  and skips issues that are already claimed (open PR or existing assignee) or have genuinely
  unmet prerequisites. Do NOT use it for merely listing or summarizing the backlog, for creating
  / triaging / commenting on / closing issues, or for code changes the user is already directing
  step by step.
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
2. Exclude the ineligible (already claimed, or blocked with unmet prerequisites)
3. Rank and auto-pick the top issue
4. Self-assign the chosen issue
5. Understand the issue and read the right docs
6. Agree on the approach (interactive only when it adds value)
7. Implement to the project's standards
8. Verify (build, tests, lint)
9. Commit, push, open the PR
10. Offer to watch the PR

---

### 1. Identify the repo and gather candidates

Derive `owner`/`repo` from the origin remote (`git remote get-url origin`) rather than
hardcoding — this skill should keep working if the repo is renamed or forked.

List the open issues. The `claude` label is the priority signal, so pull those first; keep the
rest as a fallback. `claude` is a *priority*, not a hard filter — only fall back to non-`claude`
issues if no `claude` one is eligible, unless the user explicitly says "claude only".

### 2. Exclude the ineligible

Starting work that's already claimed or can't proceed yet wastes effort and risks duplicate PRs,
so trim the candidate list before ranking.

**Already claimed — skip.** Either signal means skip:

- *Open PR.* Detect it two ways, since neither alone is reliable here: run a search like
  `repo:OWNER/REPO is:issue is:open -linked:pr` to drop issues GitHub has formally linked to a PR,
  and **also** list the open PRs and scan each title + body for issue references (`#N`,
  `closes/fixes/resolves #N`), treating any referenced still-open issue as taken. The second pass
  matters because this repo's PRs don't always use closing keywords.
- *Existing assignee.* An issue already assigned to someone is being worked — that's exactly how
  this skill claims its own pick (step 4). Skip it. (If it's assigned only to **you** from an
  abandoned earlier attempt, you may resume it.)

**Blocked — but verify before excluding.** An issue often *mentions* a prerequisite that may since
have been satisfied. Excluding on the keyword alone would bury work that's actually ready, so when
you spot a prerequisite, go check its **real, current** status and only exclude if it's genuinely
still outstanding:

- *Prereq references an issue/PR* ("after #N", "blocked by #N", "depends on #N", "follow-up to #N"):
  look up #N. Closed or merged → prerequisite met, the issue is eligible. Still open → still blocked.
- *Prereq described in prose* ("after proper JWTs are implemented", "needs bearer-token auth first"):
  search the codebase for evidence it's been done. Present in the code → met → eligible. Clearly
  absent → blocked.
- *Process / design prereq* ("mock the design first"): check the issue's comments and any linked
  resources for a sign-off or a link showing it's done. Evidence it's satisfied → eligible. If you
  can't find any, treat it as still blocked but **flag it** rather than silently dropping it — you
  may simply be missing context.

Reflect what you found in the selection announcement: if a once-gated issue is now unblocked, say
why ("#27 was gated on JWT auth, which is now implemented in `…`, so it's eligible"); if it's
genuinely still blocked, say that; if you couldn't tell, keep it as a lower-priority candidate and
flag the uncertainty. **Never exclude an issue for a prerequisite without first checking whether
that prerequisite is already met.**

### 3. Rank and auto-pick

Sort the eligible candidates by this precedence and take the top one — **don't ask which to pick**:

1. Has the `claude` label (claude-labeled first)
2. Is a `bug` (bugs before enhancements / other types)
3. Oldest `created_at` first
4. Lowest issue number (final tiebreak)

Announce the choice so the decision is transparent: the issue number + title, the one-line reason
it won, and a short note of anything you excluded as claimed or blocked (including any prereqs you
verified as already met).

- **User named a specific issue** ("pick up #74"): skip selection and use it. Still run the
  eligibility checks from step 2 and flag it if it looks claimed or blocked before diving in.
- **Nothing eligible**: say so plainly, list what was excluded and why, and stop. Don't invent work
  or start a blocked/claimed issue anyway.

### 4. Self-assign the chosen issue

Immediately after picking — before you start the work — claim the issue by assigning it to
yourself. This makes the work visible and stops a parallel session from grabbing the same issue
(it's why an existing assignee counts as "claimed" in step 2).

- Resolve your GitHub login with `mcp__github__get_me`.
- Add yourself to the issue's assignees with `mcp__github__issue_write` (`method: "update"`,
  `assignees: [<your-login>]`). You're normally just adding yourself; keep any existing assignees
  you intend to preserve.

Do this for a user-named issue too.

### 5. Understand the issue and read the right docs

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

### 6. Agree on the approach (interactive only when it adds value)

The goal is to avoid building the wrong thing without pestering the user over trivial fixes.

- **Clear, contained, one obvious approach** → state a brief plan (a few bullets) and proceed.
- **Ambiguous, large/multi-part, or a real design fork** → surface the options and ask with
  `AskUserQuestion` (or plain questions) before writing code. Examples that warrant a decision:
  an issue offering "remove *or* implement this", or one that explicitly asks you to "research a
  better solution". For a large issue, propose what's in this PR vs. a follow-up and confirm the
  scope. For substantial changes, consider proposing a plan and waiting for sign-off.

### 7. Implement to the project's standards

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

### 8. Verify before you push

Run what CI runs, scoped to what you changed. Integration tests reuse the Postgres (`:5499`) and
Redis (`:6399`) containers the session-start hook already started (see `.container-info.json`).

- **Backend** (repo root): `dotnet build`, then `dotnet test --no-build --no-restore`.
- **Frontend** (`UI/new-svelte`, after `npm ci` if needed): `npm run lint` and `npm run test:unit:ci`.
- **E2E** (Playwright) is heavyweight — run it only for critical-path UI changes.

Report failures honestly and fix them. Don't open a PR on red — a green local run is the whole
point of doing this before pushing.

### 9. Commit, push, open the PR

- Work on the session's current branch; don't switch branches without permission. If you're somehow
  on `main`/`master`, create a branch first.
- Write a clear commit message that summarizes the change and references the issue (e.g.
  `Fix silent exception swallowing in DataProviderSynchronizer (#73)`). Follow the repo's and
  environment's commit/PR footer conventions.
- Push with `git push -u origin <branch>`; on network errors retry with backoff.
- Open a **ready-for-review** PR (not a draft) via `mcp__github__create_pull_request`. The body
  should have a summary, how it addresses the issue, a test plan/checklist, and a `Closes #N` line
  so the issue auto-closes on merge.

### 10. Offer to watch the PR

After opening the PR, proactively offer to watch it for CI failures and review comments via
`subscribe_pr_activity`, then let the user decide.

---

## Worked example (illustrative)

Eligible after filtering: a `claude` `bug` from June 2, a `claude` `bug` from June 4, and a
`claude` `enhancement` from June 1. Ranking picks the **June-2 bug** — `claude` + `bug` beats the
enhancement, and it's older than the June-4 bug. But if that June-2 bug's body said "depends on
#40", you'd look up #40 first: if #40 is merged, the bug stays eligible and is picked; if #40 is
still open, the bug is excluded as blocked and the June-4 bug wins instead.

## Guardrails

- This skill picks up and implements issues. It is **not** for creating, triaging, commenting on,
  or closing issues, and not for merely listing/summarizing the backlog.
- Self-assign the issue the moment you pick it, and treat an existing assignee as "already claimed"
  — together these keep parallel sessions from colliding on the same pick.
- Never fabricate work when nothing is eligible, and never start a blocked or already-claimed issue.
- Don't exclude an issue for a prerequisite without first verifying that the prerequisite is still
  genuinely unmet.
