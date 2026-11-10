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

## Paused here (2026-11-10, after M4)

Milestone: **M4 done** (Tasks 26-30): 0.1 is built, reviewed and exercised on both lines. Unit 247, integration 122, adapter 32 on 17 and on 18; Vitest 46; Playwright 7 passed per line.
Reviews: qa-reviewer pass with changes, security-analyst yes with conditions, no Critical. QA-F1, QA-F2, QA-F3 (profile route, Pascal's choice), QA-F4, SEC-F1 and SEC-F5 fixed; the Minors deferred are in HANDOVER.md.
CI runs on every PR (green for #1 and #2). Next: Pascal's steps in HANDOVER.md "Your to-do list" (release checks, NuGet ID and Trusted Publishing, tag `release/0.0`).
Next task: **Task 31** (M5, the recorder), **on Sonnet** (`--model sonnet`), and only after 0.1 is released.

## Milestone commit

```bash
# from the repository root
git status --short          # check: only README.md, CLAUDE.md, COMMITS-TODO.md, HANDOVER.md, umbraco-marketplace.json, docs/, assets/screenshots/, .github/, src/ changed or added (two old screenshots deleted); no secrets, .env, appsettings.*.local, bin/obj, spikes/, test-results or review docs
git add -A
git commit -m "feat: 0.1 release readiness with privacy docs, profile route and final review fixes"
git log -1 --oneline
```

