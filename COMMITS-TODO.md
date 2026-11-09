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

## Paused here (2026-11-09)

Milestone: **M1 done** (Tasks 7, 7a, 8-14), both lines green.
Reviews: every Critical, Important and security Low finding is fixed under a test that failed first (HANDOVER.md has the list).
Next task: **Task 15** (M2: client scaffold, generated client, shared components, URL state), **on Sonnet** in a fresh session (`--model sonnet`, per the plan's "Time and model per milestone").
Before M2: run the milestone commit below. CI has still not run on GitHub.

## Milestone commit

```bash
# from the repository root
git status --short          # check: only src/, docs/spikes.md, COMMITS-TODO.md, HANDOVER.md changed; no secrets, appsettings.*.local, user-secrets, review docs or .superpowers/
git add -A
git commit -m "feat: query layer, source settings, Management API and security controls"
git log -1 --oneline
```
