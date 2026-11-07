# Commits to make

Pascal commits once per milestone (the "Milestone commit" block at the end). The checkpoints below are a record of the plan's "Checkpoint (Pascal commits)" lines.

## M0 checkpoints

- [x] Task 1: `chore: scaffold both lines, client, demo sites and CI` (done locally 2026-11-07; includes `packages.17.lock.json` / `packages.18.lock.json`, `Client/package-lock.json`, `tests/e2e/package-lock.json` and the built `wwwroot/`)
- [x] Task 2: `docs: S1 and S6 spike results`
- [x] Task 3: `docs: S2 and S3 spike results`
- [x] Task 4: `docs: S4 spike results`
- [x] Task 5: `docs: S5 and S7 spike results`
- [x] Task 6: `docs: M0 spikes and gates for both lines` (milestone review done; fixes applied)

## Paused here (2026-11-07)

Milestone: **M0 done.** Pascal reviews `docs/spikes.md` before M1 (README build approach).
Next task: **Task 7** (M1: options, contract and event model), on Opus in a fresh session.
Before M1: run the milestone commit below, then make the repo public and push (HANDOVER.md). CI has not run.

## Milestone commit

```bash
# from the repository root
git status --short          # check: no secrets, no appsettings.*.local, no user-secrets, no review docs, no spikes/
git add -A
git commit -m "docs: M0 spikes and gates for both lines"
git log -1 --oneline
```
