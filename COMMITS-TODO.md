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

## Paused here (2026-11-10, after M3)

Milestone: **M3 done** (Tasks 20-25), both lines green (unit 243, integration 122, adapter 31 on 17 and on 18; Vitest 43).
Review: qa-reviewer pass with changes, no Critical; QA-1, QA-3, QA-4 and QA-10 fixed under tests; eight Minors deferred (HANDOVER.md).
Next task: **Task 26** (M4: README privacy, sources and limits), **on Opus** in a fresh session (`--model opus`, per the plan's "Time and model per milestone").
Before M4: run the milestone commit below. CI has still not run on GitHub. Two open decisions are due before Task 27 (HANDOVER.md, "Open decisions").

## Milestone commit

```bash
# from the repository root
git status --short          # check: only src/, samples/, .github/workflows/build.yml, README.md, COMMITS-TODO.md, HANDOVER.md changed or added; no secrets, .env, appsettings.*.local, bin/obj, test-results or review docs
git add -A
git commit -m "feat: Automate, Forms, AI, uSync and Archived Users sources"
git log -1 --oneline
```
