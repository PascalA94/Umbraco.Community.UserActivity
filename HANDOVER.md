# Handover

**State (2026-11-07):** M0 (Tasks 1-6) is done, and every change from the milestone review (pass with changes) is applied.
- Scaffold, demo and CI are in place; spikes S1-S7 are recorded in `docs/spikes.md` (M0 summary at the top).
- `bash build/test.sh`: 7/7 unit and 2/2 integration tests on both Umbraco 17 and 18.
- Client: Vitest 1/1. `build/check-xss.sh` and `actionlint` are clean.
- The demo boots and seeds on both lines over HTTPS.

**CI:** CI has **not** run. "Done" means green on GitHub.

**Pascal's steps, in order:**
1. Run the "Milestone commit" block at the end of `COMMITS-TODO.md`.
2. Pre-public checks (they pass locally; repeat them after committing):
   - `gitleaks git . --redact`
   - the local-path grep from `docs/plan.md` "Plan review changes". It should print only the grep commands quoted in `docs/plan.md`.
   - The CLAUDE.md README pointer no longer names a local path.
3. Make the repo public: About, topics, secret scanning with push protection, CodeQL, squash-only (`.github/REPO-SETUP.md`).
4. Push a branch and open a PR. Expected green: `build` (4 legs), `client`, `secrets`, `osv` and `deps`. Then add the ruleset requiring them.
5. Review `docs/spikes.md` before starting M1.

**Not verified, and why:**
- **SQL Server:** not run, because Docker was down. Its schema facts come from CMS source; Task 11's local SQL Server run confirms them, so start Docker before Task 11.
- **Automate scheduled and webhook runs:** not observed. Covered in Task 20.
- **Skybrud on 18, the Workflow approve notification and a stable UrlTracker 18:** checked in M5.
- **Self-service password change event type:** not observed. Covered in Task 10.
- **nuspec range assertion:** it passed on its first run; I never saw it fail.

**Decided by Pascal this session:**
- Forms Audit Trail is not a source in 0.1 (alias reserved).
- `umbracoAudit` queries are bounded by id range.
- Versions follow Umbraco: 17.0.0 and 18.0.0, already wired.
- No changed-value diffs in 0.1.

**Open decisions:**
- Release tag scheme: `release/<minor>.<patch>` (the template's `publish.yml`) or `vX.Y.Z` (the README). Needed before Task 27.
- Task 30 "use Copilot once": needs an AI provider account and key, or a seeded audit row instead.

**Next session starts at:** Task 7 (M1).
