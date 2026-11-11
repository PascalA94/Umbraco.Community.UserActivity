# Commits to make

Pascal commits once per milestone (the "Milestone commit" block at the end). The checkpoints below are a record of the plan's "Checkpoint (Pascal commits)" lines.

## M0 checkpoints

- [x] Task 1: `chore: scaffold both lines, client, demo sites and CI` (done locally 2026-11-07; includes `packages.17.lock.json` / `packages.18.lock.json`, `Client/package-lock.json`, `tests/e2e/package-lock.json` and the built `wwwroot/`)
- [x] Task 2: `docs: S1 and S6 spike results`
- [x] Task 3: `docs: S2 and S3 spike results`
- [x] Task 4: `docs: S4 spike results`
- [x] Task 5: `docs: S5 and S7 spike results`
- [x] Task 6: `docs: M0 spikes and gates for both lines` (milestone review done; fixes applied)

## M1 checkpoints

- [x] Task 7: `feat: options, source contract and event model`
- [x] Task 7a: `feat: per-source settings and availability`
- [x] Task 8: `feat: user identity resolver`
- [x] Task 9: `feat: core source for umbracoLog`
- [x] Task 10: `feat: core source for umbracoAudit with ownership claims`
- [x] Task 11: `feat: activity query engine with merge, paging and partial results`
- [x] Task 12: `feat: user activity Management API with admin policy`
- [x] Task 13: `feat: CSV export, view logging and leak tests`
- [x] Task 14: M1 stop. qa-reviewer: pass with changes. security-analyst: no sign-off on one Critical (SEC-1), now fixed. Pascal chose the fixes in session.

## M2 checkpoints

- [x] Task 15: `feat: shared activity components and URL filter state`
- [x] Task 16: `feat: global Activity view under the Users section`
- [x] Task 17: `feat: per-user Activity tab`
- [x] Task 18: `feat: event detail sidebar, empty, loading and partial states`

- [x] Task 19: M2 stop. qa-reviewer: pass with changes; every Important fixed under tests (HANDOVER.md has the list and what was left)

## M3 checkpoints

- [x] Task 20: `feat: Automate source`
- [x] Task 21: `feat: Forms source`
- [x] Task 22: `feat: runtime adapter pattern and Umbraco.AI source`
- [x] Task 23: `feat: uSync source`
- [x] Task 24: `feat: Archived Users source` (Forms Audit Trail out of 0.1, Pascal 2026-11-07)

- [x] Task 25: M3 stop. Sample source and README "Add your own source"; qa-reviewer: pass with changes; both Importants and two re-graded Minors fixed under tests (HANDOVER.md has the list)

## M4 checkpoints

- [x] Task 26: `docs: privacy, sources and limits`
- [x] Task 27: `docs: presentation, Marketplace and publish workflow` (Step 6, reserve the NuGet ID and add Trusted Publishing, is Pascal's)
- [x] Task 28 Step 1: pre-push checks clean (push and PR #3 done; CI already green on PRs #1 and #2; release-checks still to run)
- [x] Task 29: `fix: final review findings` (qa-reviewer pass with changes, security-analyst yes with conditions; no Critical)
- [x] Task 30 Steps 1-6: `docs: acceptance results for 0.1` (both lines; Step 7, the release, is Pascal's)

## M5 checkpoints

- [x] Task 31: `feat: recorder table, writer and retention job`
- [x] Task 32: `feat: recorder handlers for CSP Manager and redirects` (CSP Manager, Skybrud Redirects, UrlTracker on 17)
- [x] Task 33: `feat: recorder handlers for Workflow`
- [x] Task 34: `docs: recorder and retention` (README "Recorder", "Set a retention limit", erasure bullet)
- [x] Task 35: M5 stop. qa-reviewer: pass with changes; security-analyst: yes with conditions, no Critical. Importants and the two security conditions fixed (HANDOVER.md has the list)

## Paused here (2026-11-10, after M5)

Milestone: **M5 done** (Tasks 31-35): the recorder is built and reviewed on both lines. Unit 307 / 303, integration 136, adapter 72 / 62 (17 / 18); Vitest 54; `check-xss.sh` clean.
Reviews: qa-reviewer pass with changes, security-analyst yes with conditions, no Critical. Fixed under tests, plus README and design. Your choice recorded: the recorder keeps names after a user is deleted, and the README gives the erasure SQL.
Next task: **Task 36** (release the recorder minor). Step 1 is the demo-site exercise (an agent can do it). Step 2 is yours: bump `PackageMinor` to 1, run release checks, tag `release/1.0`.
Open for Task 36 Step 1: the Skybrud add and edit user at handler time, which Workflow notifications fire per approval shape, a SQL Server run of the recorder tests, and whether UrlTracker or Skybrud raise notifications outside a backoffice user.

## Earlier: Paused here (2026-11-10, after M4)

Milestone: **M4 done** (Tasks 26-30): 0.1 is built, reviewed and exercised on both lines. Unit 247, integration 122, adapter 32 on 17 and on 18; Vitest 46; Playwright 7 passed per line.
Reviews: qa-reviewer pass with changes, security-analyst yes with conditions, no Critical. QA-F1, QA-F2, QA-F3 (profile route, Pascal's choice), QA-F4, SEC-F1 and SEC-F5 fixed; the Minors deferred are in HANDOVER.md.
**0.1 released 2026-11-10:** 17.0.0 and 18.0.0 on nuget.org from tag `release/0.0` (`28dec72`), GitHub release marked Latest. M4 follow-ups merged as PRs #3 to #5.
Next task: **Task 31** (M5, the recorder), **on Sonnet** (`--model sonnet`). 0.1 is released, so M5 can start.

## Earlier milestone commit (M4, already done)

```bash
# from the repository root
git status --short          # check: only README.md, CLAUDE.md, COMMITS-TODO.md, HANDOVER.md, umbraco-marketplace.json, docs/, assets/screenshots/, .github/, src/ changed or added (two old screenshots deleted); no secrets, .env, appsettings.*.local, bin/obj, spikes/, test-results or review docs
git add -A
git commit -m "feat: 0.1 release readiness with privacy docs, profile route and final review fixes"
git log -1 --oneline
```

## Milestone commit

```bash
# from the repository root
git status --short          # check: only README.md, COMMITS-TODO.md, HANDOVER.md, Directory.Build.props, docs/spikes.md, docs/design.md and src/ changed or added (new Recorder/ folders, regenerated wwwroot chunks); no secrets, .env, appsettings.*.local, bin/obj, spikes/, test-results or review docs
git add -A
git commit -m "feat: recorder for CSP Manager, Workflow and redirects"
git log -1 --oneline
```

## Paused here (2026-11-11, Task 36 Step 1 partial)

SQL Server run passed on both lines (integration 135 passed, 1 skipped). The demo exercise with third-party packages was not done (Pascal chose the partial option). Next: Pascal's Step 2 (bump `PackageMinor` to 1, release checks, tag `release/1.0`). Nothing is left in the plan after Task 36.

## Milestone commit (Task 36 notes)

```bash
git status --short          # check: only HANDOVER.md and COMMITS-TODO.md changed
git add -A
git commit -m "docs: record recorder acceptance results"
git log -1 --oneline
```

