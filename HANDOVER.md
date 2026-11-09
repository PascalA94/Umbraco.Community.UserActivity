# Handover

**State (2026-11-09):** M1 (Tasks 7, 7a, 8-14) is done. It covers options and the source contract, per-source settings and availability, the identity resolver, the core source (`umbracoLog` and `umbracoAudit`), the query engine, the Management API with its policy, CSV export, view logging and the leak and architecture tests. Both M1 reviews ran, and their fixes are applied.
- `bash build/test.sh`: 163/163 unit and 89/89 integration tests on both Umbraco 17 and 18 (SQLite). Three full green runs in a row at the end.
- On SQL Server (local Docker, 18 line), the integration suite passes 89/89. The perf seed (1M `umbracoLog` + 1M `umbracoAudit` rows) stays under 170 ms for every case, including a 366-day search. Results are in `docs/spikes.md`, "M1 results".
- Client: Vitest 1/1 (no client changes in M1). `build/check-xss.sh` is clean. The build has no warnings.
- Demo sites on 17 and 18 boot with the package, and an anonymous `curl -sk` to every endpoint gets 401.

**CI:** CI has **not** run on GitHub. "Done" means green there.

**M1 reviews** (verdicts saved privately in the workspace `reviews/` folder):
- **security-analyst:** no sign-off at first, because of one Critical (SEC-1: self-view users received raw event details, including the default admin's name and email on their own sign-in rows). You chose the fixes in session. Fixed:
  - SEC-1: details go to readers only.
  - SEC-2: opening an event and the unfiltered global view now write "viewed" rows.
  - SEC-3: out-of-range dates give 400.
  - SEC-4: a failed audit write no longer blocks the next hour of view rows.
  - SEC-5: claims can't take core-owned or empty prefixes, and overlap checks ignore case.
  - SEC-6: tests for disabled and locked-out readers.
  - SEC-7: the contract says details never hold IPs.
  - SEC-8 was checked and is not a finding: a request with only the cookie gets 401, and the token cookie is `SameSite=Strict`. `CrossSiteRequestTests` keeps checking it.
- **qa-reviewer:** pass with changes. Fixed:
  - QA-1: `AllowedUserGroups` config used to *add to* `admin` instead of replacing it.
  - QA-2: core's database work is now bounded by `SourceTimeoutSeconds`, and searches aren't counted.
  - QA-3: an export with a failed source returns 503 instead of an incomplete file.
  - QA-4 to QA-12 are fixed or covered by new tests.
  - View and export audit rows no longer store the viewer's IP.
- Each fix has a test that failed first. For the fixes covered only by integration tests, I reverted the fix and watched the test fail.

**Pascal's steps, in order:**
1. Run the "Milestone commit" block at the end of `COMMITS-TODO.md`.
2. Start M2 (Task 15) in a fresh session **on Sonnet** (`--model sonnet`).
3. When convenient, push a branch and open a PR for the first GitHub CI run (still pending since M0).

**Not verified, and why:**
- **One slow stretch of test runs (about 50 minutes).** Three runs of the 18-line integration suite took 15-19 minutes instead of about 35 s. In one of them, a single test got a 401 straight after authenticating. All the time was spent outside the test bodies (each test took about 1 s), and the testhost was busy on the CPU. It started right after I removed the SQL Server container. Every run after it was green and fast (six filtered runs, then two full both-line runs). Cause not found; watch for it in CI.
- **The command timeout on SQLite** doesn't interrupt a running query, because SQLite has no server-side query timeout. The bound works on SQL Server.
- **SQL Server** was checked locally on the 18 line only. CI's release-checks run covers both lines.
- Unchanged from M0: Automate scheduled and webhook runs (Task 20); Skybrud on 18, the Workflow approve notification and a stable UrlTracker 18 (M5).

**For later milestones (from the reviews):**
- M3: an IP-pattern test over every adapter's details (SEC-7). The username-only matching for uSync must only use the stored username.
- M4 README:
  - search text and user keys travel in query strings (proxy and APM logs);
  - only these accesses are recorded as views: the tab, user-filtered or unfiltered global views, event opens and exports;
  - the export and view throttles are per server.

**Decided by Pascal:**
- 2026-11-07: Forms Audit Trail is not a source in 0.1; `umbracoAudit` queries are bounded by id range; versions 17.0.0 and 18.0.0; no changed-value diffs.
- 2026-11-09: event details are for readers only; detail opens and the unfiltered global view are logged; the Importants and security Lows were fixed before the M1 commit.

**Open decisions:**
- Release tag scheme: `release/<minor>.<patch>` (the template's `publish.yml`) or `vX.Y.Z` (the README). Needed before Task 27.
- Task 30's "use Copilot once" needs an AI provider account and key, or a seeded audit row instead.

**Next session starts at:** Task 15 (M2), on Sonnet.
