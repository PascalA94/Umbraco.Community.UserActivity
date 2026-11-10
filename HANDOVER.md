# Handover

**State (2026-11-10, after M3): M3 (Tasks 20-25) is done.** Five more sources: `automate` (Automate's `umbracoAudit` rows, with the person who started a run as actor and the service account as `Via`), `forms` (Forms' `umbracoLog` rows, with names from `UFForms`), `archivedUsers` (Archived Users' own `umbracoLog` rows), and two runtime adapters, `ai` (Umbraco.AI's audit service) and `usync` (uSync's history, 17.3.0 and later). The adapters use compile-only references (PackageDownload plus a DLL reference), so the nupkg still depends on Umbraco only. They are registered only when their integration is installed and the probe passes. Also new: `samples/SampleSource` and a README section, "Add your own source".
- `bash build/test.sh`: unit 243, integration 122, adapter 31 on 17 and on 18; Vitest 43. Client Vitest 43/43, `build/check-xss.sh` clean, `build/check-vulnerable.sh` clean on both lines.
- New test project `src/Umbraco.Community.UserActivity.AdapterTests`. It runs in its own process against the real Umbraco.AI.Core and uSync.History: by default at the oldest supported release per line (17.0.0 / 18.0.0 for AI, 17.3.0 / 18.0.0 for uSync), and on the CI latest leg at the newest. I also ran AI 17.7.0 / 18.7.0 with the latest uSync locally: 29/29 each.
- **Finding, fixed:** with Umbraco.AI absent, the site failed to start. Umbraco's type scan threw on our assembly because an adapter's async state machine held a struct over an AI type. The fix: adapters never await an integration's type (`AdapterTasks`). The guard is `ArchitectureTests_EveryTypeLoads_WithoutOptionalAssemblies` (Debug and Release), plus `NotInstalledTests` on a real host.
- **Finding, fixed:** I first pinned ImageSharp 3.2.0 to clear Umbraco.AI's vulnerable transitive packages in the test project, but 3.2.0 needs a Six Labors licence key to build. The AdapterTests now reference only the Umbraco.AI.Core DLL, so ImageSharp isn't in any graph. The CI latest leg resolves the newest Umbraco.AI.Core from nuget.org (`build.yml`, new step).
- **Finding, fixed after the review:** my QA-1 fix made `CoreClaims` read the source collection while it was being built. The collection holds the core source, which needs `CoreClaims`, so the backoffice host deadlocked at boot (every HTTP integration test hung). The claims now read the collection lazily again; `CoreClaims_AsRegistered_ResolveWithoutACycle` fails on the eager version and passes on the fix. This may also explain the unexplained M1 stall noted below: not confirmed.
- **Contract addition (public, permanent once released):** `IClaimsCoreAuditRows.ClaimedLogRows` (a default interface member, so existing implementers keep compiling) and the record `ClaimedLogRows(EntityType, LogHeader, CommentPrefix)`. Archived Users needed it, because claiming every `User` row would take core's rows. The validator now refuses whole-type claims on `User`, `UserGroup` and `Member` log rows.
- **qa-reviewer (M3): pass with changes, no Critical.** Fixed, each with a test that failed first:
  - QA-1: uSync's `IsAvailable` could throw on every request. It now never throws, core's claims only ask claimants, and a throwing `IsAvailable` counts as unavailable.
  - QA-3: `ShowIpAddresses` is now enforced in the engine for every source, including third-party ones.
  - QA-4 (re-graded to Important): the claims rules above, and the README corrected (reserved aliases, stored name matched as a username).
  - QA-10 (re-graded to Important): adapter plumbing and the built-in sources are internal now, so only the contract is public API.
  The verdict is in the private workspace `reviews/m3-qa-review.md`.
- **Deferred Minors (your call):**
  - QA-2: the probe can't say "no parameters" and doesn't check return types.
  - QA-5: no test for the NULL handling in the partial-claim SQL.
  - QA-6: the uSync local-time test can't fail on a UTC CI runner; set `TZ` on the CI test step.
  - QA-7: uSync with more than 5,000 entries reports an inexact total even when the window is inside the newest 5,000.
  - QA-8: the sample doesn't register its `INewsletterLog`; the README says so.
  - QA-9: doc drift. `Directory.Build.props` and the design 3.2 / plan Global Constraints masters still describe `PackageReference PrivateAssets` for the adapters, and two `CoreClaims` summaries are misplaced.
  - QA-11: an IP-pattern detail test for Forms, Archived Users and uSync.
  - QA-12: the CI AI-version resolver should sort numerically and skip unlisted versions.
- **Rulings to know about:**
  - Automate's scheduled and webhook `initiatedBy` shapes still haven't been observed. Every kind except `user:` shows as automation, and an unreadable value falls back to the service account, flagged.
  - The Forms source claims only `entityType` `Form`. Folder rows, if Forms writes any, stay in core.
  - The AI source returns nothing while an entity-type filter is on, and never reads prompts, responses or error text.
  - uSync times: Local is converted, Unspecified is taken as UTC.
  - uSync rows have a hashed id, so the history file's server path never leaves the server.
  - An adapter whose probe fails is hidden, with one Warning at start-up. For example, uSync 17.0.x has no history service, so those sites get one Warning per boot.
  - The full list is in the ledger rulings, summarised in this session's final message.
- **Not verified:**
  - CI (nothing has run on GitHub).
  - SQL Server for the new SQL (the partial-claim exclusion, Automate's `LIKE` on `eventDetails`, the `UFForms` join). Release-checks covers it.
  - Real Automate, Forms, AI or uSync rows on a demo site: the tests use S2/S3 shapes, substitutes of the real AI/uSync interfaces, and Archived Users rows written through core's `IAuditService` as 01 writes them.
  - Whether Umbraco.AI's `SearchText` also matches prompt or response text. If it does, a search could reveal content we keep out of details. Check before 0.1 (Task 29).
  - No e2e run: M3 changed no UI.
- **For M4 (Task 26 README):**
  - uSync matches by username, so a renamed user's old runs show under the stored name, and if someone else later takes that username, those runs show on their tab.
  - uSync history can be cleared from uSync's dashboard.
  - Automate shows only steps that change CMS data.
  - Forms Audit Trail history is not shown in 0.1.

**Pascal's steps:** run the "Milestone commit" block at the end of `COMMITS-TODO.md`, then start M4 (Task 26) in a fresh session **on Opus**.

**Earlier state (2026-11-10, after M2): M2 (Tasks 15-19) is done.** The client now has the shared components and URL filter state, the global Activity view under Users, the per-user Activity tab (with the `CanView` condition), the detail sidebar, the empty, loading, partial and depth-cap states, CSV download and English localisation. It uses a generated API client (`npm run generate:api`).
- `bash build/test.sh`: 163/163 unit and 89/89 integration on both lines. Client Vitest 43/43. `build/check-xss.sh` clean. `tsc` clean against backoffice 17.8 and 18.3.
- Playwright `tests/e2e/specs/activity.spec.ts` (global filters and URL restore, tab locked to its user and the admin's own tab, keyboard open/close with focus return, hostile names as text in the table and sidebar, editor sees nothing, axe on both views and the sidebar): 6 passed on the 17 demo and on the 18 demo, SQLite, local. Run it with the demo up and `SAM_PASSWORD`, `WREN_PASSWORD`, `ED_PASSWORD`, `ADMIN_PASSWORD` and `TEST_USER_PASSWORD` in the environment (the first three are the demo seed passwords).
- Draft screenshots are in `assets/screenshots/` (`SCREENSHOTS=1`; they show the demo's fake data, including the hostile item names). Task 27 replaces them.
- **qa-reviewer (M2): pass with changes.** Fixed, each with a test: first list waits for filter-options so the default window follows `DefaultRangeDays`; one `clearFilters` for both Clear buttons; one persistent live region (empty, loading, results); accessible names for the user picker and for each row button; `?user=` on the tab cannot change the endpoint (e2e checks the request path); hostile entity, actor and raw type in the sidebar; `__proto__`-style provider strings; export file name and delayed URL revoke; search capped at 100; pagination label localised; `aria-sort` removed (no sort control yet). The verdict is in the private workspace `reviews/m2-qa-review.md`. Not run: a fresh failing-first check for each of these fixes (the tests were written with them), unlike M1.
- **Left open from the review:** QA-6 (no demo user has the Users section outside the allowed groups, so `CanView` is only unit-tested in its pure parts, not end to end); QA-9 (the menu item shows to Users-section users outside the allowed groups and gives a 403 toast); export 400/429 messages; range vs `MaxRangeDays` and a short-search hint; a When sort toggle; custom-range inputs after Back; the access cache survives an in-page re-login.
- **Control change to confirm (security):** `build/check-xss.sh` now also fails on any `.term` that is not called with a literal key (casts and aliases), and exempts one file, `components/localize.ts`, which only resolves keys starting with `userActivity_` (provider keys fall back to the source alias). This closes a bypass the reviewer found; the single exemption is for you or `security-analyst` to accept at the M4 review.
- **Rulings:** `uui-table` with an `aria-label` instead of `umb-table`; a workspace view instead of a collection for the global view; `uui-input type=date` instead of `umb-input-date`; Lit boolean attributes default to false (`hideUser`, `approximate`) because `?attr=${false}` cannot override a true default (the e2e found it).
- Not verified: CI (nothing has run on GitHub); the 403 for an editor in the browser (needs a bearer token; M1 integration tests cover it); SQL Server with the UI.
- The demo site rewrites `demo/.../appsettings.json` (adds a site Id) when it starts; I reverted it. Revert it again if you run the demo before the commit.

**Pascal's steps:** run the "Milestone commit" block at the end of `COMMITS-TODO.md`, then start M3 (Task 20) in a fresh session **on Opus**.

**Earlier state (2026-11-09):** M1 (Tasks 7, 7a, 8-14) is done. It covers options and the source contract, per-source settings and availability, the identity resolver, the core source (`umbracoLog` and `umbracoAudit`), the query engine, the Management API with its policy, CSV export, view logging and the leak and architecture tests. Both M1 reviews ran, and their fixes are applied.
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

**Next session starts at:** Task 26 (M4), on Opus.
