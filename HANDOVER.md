# Handover

**State (2026-11-11): 0.1 and the recorder minor are released** (17.0.0 / 18.0.0 from tag `release/0.0`; 17.1.0 / 18.1.0 from tag `release/1.0`, `a3a9003`). **Uncommitted patch 17.1.1 / 18.1.1** (`PackagePatch` bumped to 1; tag `release/1.1` when you release): the real-package exercise found one recorder bug and the work added entity-type labels, the uSync user link and a recorder screenshot. See "Real-package exercise" below.

## M5: the recorder (released as 17.1.0 / 18.1.0)

- **What it is:** table `communityUserActivityEvent` (created by a package migration plan at start-up), a writer that never throws into another package's save, a `recorder` source, and a daily distributed retention job.
- **Handlers** (name-based guard, reflection probe, compile-only references, nuspec still Umbraco-only):
  - CSP Manager (both lines): saves only (the package raises no delete notification).
  - Skybrud Redirects (both lines, 17.0.5 or later): add, edit, delete. S4's "no save notification" was wrong; it had only hooked Saved.
  - UrlTracker: **17 only**; it has betas only on 18, so there is no guard on 18.
  - Workflow (both lines): settings, approval groups, instance created, approved, rejected, completed; config saved on 18.
- **Stored:** when, user key and name at the time, item name (cut at `?` and `#`), and a short allow-list of flags. Never CSP text, redirect destinations or query strings, request bodies, group emails or IPs. A regex UrlTracker redirect stores the label "Pattern redirect". When a notification carries more than 100 items a "truncated" row records the count.
- **Retention:** `Recorder:RetentionDays`, default 0 = keep forever, 1-36,500 allowed, negative or higher fails start-up. One start-up line while it is 0. The README's install section and "Set a retention limit" tell owners to set a limit.
- **Your decision this milestone:** a deleted user's name stays in the recorder table (design 4.1). The README gives the SQL to delete or blank rows by `UserKey` for an erasure request, and the design master and public copy say so.
- **Source hidden** until a recorded package registers a handler. Turning `Sources:Recorder:Enabled` off also stops recording; existing rows stay until retention removes them.
- **Client:** the filter options carry `retentionKeptForever`, and the empty state says "Recorded changes: kept forever (no limit set)." The source name key `userActivity_sourceRecorder` was missing and is added.
- **Writer and ambient scopes:** the writer joins a caller's scope and completes it in a `finally`, so a failed insert never vetoes the caller's save (proved on SQLite with a trigger). Skybrud's delete therefore commits or rolls back together with our row.

### M5 review (Task 35)

Verdicts, summarised (the full text stays in this session's reports; saved privately as `reviews/m5-qa-review.md` and `reviews/m5-security-review.md`).
- **qa-reviewer (opus): pass with changes.** No Critical, 4 Important. **security-analyst (opus): sign-off yes with conditions.** No Critical or High. Both conditions are met: the erasure decision (M1) and the README rollback wording (L1).
- **Fixed, each under a test:**
  - QA-1 / M1: erasure contradiction. Resolved by your choice; the test is renamed `DeletedUser_FallsBackToStoredName`.
  - QA-2: README "Erasure and retention" now names the recorder table.
  - QA-3: no upper bound on `RetentionDays` (a huge value would have broken every Activity request). Bound 36,500; `Options_RetentionUpperBound_Is36500Days`, `RecorderHint_BadValue_NeverThrows`.
  - QA-4: a vacuous guard test replaced by real ones (`ProbeFailed_RegistersNothing_AndLeavesTheRegistryEmpty`, `PassingAssembly_InjectedIntoTheGuard_Registers`).
  - L3: a regex UrlTracker redirect could store a token-bearing pattern. Now a fixed label.
  - L5: silent 100-row cap now writes a "truncated" row (UrlTracker, Workflow groups).
  - L6 / QA-7: the architecture write rule is tighter (Insert only in `RecorderWriter.cs`, DELETE only in `RecorderSql.cs`), with negative cases.
  - L2: test that a rolled-back caller also rolls back our row (a characterisation test; it was not red first).
  - QA-6 / L1 / QA-10: README rollback and log wording, stale XML doc.
- **Deferred Minors (your call):**
  - QA-5: on SQL Server a deadlock-victim or severity 20+ error in our insert can doom Skybrud's delete transaction; the SQL Server variant of the ambient-failure test is not written.
  - QA-8: with the recorder switched off and `RetentionDays` 0, nothing reminds the owner that old rows are kept forever (the README says it).
  - QA-9: no host-level test that Umbraco's real event aggregator calls the factory-registered handlers, or that the distributed job host picks up `RetentionJob`; Workflow has no throwing-writer case.
  - L4: `check-vulnerable.sh` doesn't see the four `PackageDownload` compile references (exact-pinned; OSV found no advisories on 2026-11-10). Add them to the osv-scanner inputs.
  - Recorder entity types now have English labels (patch candidate); other languages fall back to English.
  - From 0.1: the Minors listed below are unchanged.

### M5 test results (2026-11-10, final run)

| Command | 17 | 18 |
|---|---|---|
| `bash build/test.sh` | unit 307, integration 136, adapter 72 | unit 303, integration 136, adapter 62 |
| adapter tests against each line's newest releases (CSP 17.0.5 / 18.1.0, Workflow 17.5.4 / 18.2.5, UrlTracker 17.1.0) | pass | pass |
| `cd src/Umbraco.Community.UserActivity/Client && npm run build && npm test` | Vitest 54/54 | |
| `bash build/check-xss.sh` | exit 0 | |
| `UmbracoMajor=<line> bash build/check-vulnerable.sh` (security pass) | clean | clean |

### Real-package exercise (2026-11-11, patch candidate)

Local only (untracked): `demo/RecorderLocal/` (a copy of the demo with CSP Manager 17.0.5 / 18.1.0, Skybrud Redirects 17.0.5 / 18.0.0, Workflow 17.5.4 / 18.2.5, and UrlTracker 17.1.0 on 17) and `tests/e2e/local/` Playwright scripts, all listed in `.git/info/exclude`. In that demo Wren is seeded into Administrators so she can reach Settings; none of it is in the package. Wren made the changes through each package's own API with her own session token, Sam and Ed approved or rejected Workflow items, and Sam read the `recorder` rows. Everything ran over HTTPS on a separate SQLite database per line.

**Answers**
- **Skybrud add and edit under a backoffice user:** yes. `RedirectAddedNotification`, `RedirectSavedNotification` and `RedirectDeletedNotification` all carried Wren, on both lines. The `?token=...` query string was cut from the stored name.
- **Workflow notifications per approval shape** (identical on 17 and 18): a final approval (a one-stage flow, or the last stage) raises only `Completed`; a non-final stage approval raises only `Approved`; a rejection at any stage raises only `Rejected` (the instance stays Rejected, no `Completed`); cancel raises only a cancelled notification, which the recorder does not handle (README says so). Rows carry the approver, not the initiator.
- **UrlTracker and Skybrud outside a backoffice user:** UrlTracker yes. A scheduled publish of a renamed page (Umbraco's background job, no user) made UrlTracker create an automatic redirect, and the recorder stored `RedirectCreatedNotification` with a null user. A rename published by Wren stored its automatic redirect under Wren. Skybrud 17.0.5 raised nothing on a rename or a scheduled publish; it only raises from its own add, edit and delete.
- **Recorder job under a real scheduler:** it registered as `UserActivityRecorderRetention` and ran by itself. With `RetentionDays: 1`, six seeded 3-day-old rows were removed and the newer rows stayed ("Recorder retention removed 6 events older than 1 days."). 17 only.
- **`RetentionDays: 0`:** the start-up line appears once per boot, and does not appear with a limit set. The hint shows in the empty state as "Recorded changes: kept forever (no limit set)." (only when a search matches nothing). Checked on 17; the start-up line also seen on 18. On the very first boot of a brand-new database the line does not log until the second boot (the source is not available yet); harmless.
- **CSP Manager removed:** the site boots, `recorder` still lists (the other packages remain) and the 33 stored rows are readable. With all four packages removed the site boots, no start-up line is logged and the `recorder` source is absent (`source=recorder` returns 400). 17 only.
- **The skipped SQL Server test:** `Writer_FailureInsideACallersScope_LeavesTheCallersWorkCommitted` (`Assert.Ignore`: it needs a SQLite trigger; this is the deferred QA-5). `PerfSeed.Page1_And_Page100_Under2Seconds` is `[Explicit]` and not counted. SQL Server (throwaway container, generated password, since removed): integration 135 passed, 1 skipped of 136 on 17 and on 18.

**Bug found and fixed (under tests, patch candidate)**
- Creating a Workflow approval group was never recorded: Workflow raises `WorkflowGroupCreatedNotification` on create and `WorkflowGroupSavedNotification` only on edit. Fixed in `WorkflowGuard` (probe members and registration) and `WorkflowHandlers.OnGroupCreated`. Tests: `GroupCreated_IsExactlyOneRow_NamedAfterTheGroup_AndNeverItsEmail`, the handler-count assertion and the probe list; they failed first.

**Also in this patch candidate**
- English labels for `cspPolicy`, `workflowConfig`, `workflowInstance` and `redirect` (`Client/test/entity-type-labels.test.ts`, red first). `wwwroot/` rebuilt.
- QA-F7: `ActorRef.For` now looks up a User or ServiceAccount actor that has only a stored name by username (only uSync produces these), so uSync rows link to the user. The adapter folders are untouched, so the optional-assembly rule holds. Tests in `UserIdentityResolverTests`, red first.
- `screenshot-05-recorder.png` (synthetic users and items, no IPs), referenced in README "Recorder" and `umbraco-marketplace.json`. The README now says which Workflow instance events are recorded.

**Results:** `bash build/test.sh`: 17 unit 310, adapter 75, integration 136; 18 unit 306, adapter 64, integration 136. Vitest 59. `check-xss.sh` clean. Playwright against the real demo: 9 passed and 1 skipped (the screenshot spec needs `SCREENSHOTS=1`) on each line.

**Not verified / gaps left for you**
- **Workflow config (fixed in the patch candidate):** 17 now records `WorkflowNodeConfigSaved` and `WorkflowContentTypeConfigSaved`, and both lines record `WorkflowNodeConfigDeleted` and `WorkflowContentTypeConfigDeleted`, as one "Workflow configuration" row with no details. Tests failed first. Verified live: a 17 node-config save writes a row. The two delete notifications could not be triggered in the real package (emptying a flow raises Saved, and the document-type route returns 403 for Wren), so they are covered only by tests built on Workflow's real types.
- Workflow `Resubmitted` and `Cancelled` are not recorded, and Workflow instance rows have no item name (they show as "Workflow", action "Other").
- Retention and CSP removal were checked on both lines (18: 5 old rows removed by the job; site boots without CSP Manager).
- Workflow's own backoffice logs a SignalR 404 (`/umbraco/WorkflowHub`) in the browser console in the demo; it is Workflow's, not ours. No other console errors or failed requests, and no warnings or errors from our code in the server logs.
- The packages were driven through their APIs rather than their editors (UrlTracker's New redirect form was used once), so the recorder UI itself has only the screenshot and the empty-state hint check.

### Your to-do list for the recorder minor

1. Run the Milestone commit block in `COMMITS-TODO.md`, push and open the PR; PR checks must go green.
2. Start Task 36 in a fresh session (Step 1: exercise on both demo sites).
3. Set `PackageMinor` to 1 in `Directory.Build.props`, run **Release checks** (SQL Server and e2e), tag `release/1.0`, then `gh release create` per line (18 as Latest). Expected: 17.1.0 and 18.1.0.
4. The earlier 0.1 to-do items (nuget.org page check, repo settings) are below. The forum post is skipped by your standing choice.

---

## 0.1 (released)

### What 0.1 is

One NuGet package, `Umbraco.Community.UserActivity`, packed as `17.0.0` (Umbraco `[17.8.0, 18.0.0)`) and `18.0.0` (`[18.3.0, 19.0.0)`) from one commit. Its nuspec depends on Umbraco only.
- **Views:** a global **Activity** view under Users, an **Activity** tab on each user, and (new in M4) a **Your activity** box in the current user's profile that opens your own activity in a sidebar.
- **Sources:** `core` (`umbracoLog` + `umbracoAudit`), `automate`, `forms` and `archivedUsers` read core tables. `ai` (Umbraco.AI) and `usync` (uSync 17.3+) are runtime adapters with compile-only references, a name-based guard and a reflection probe. A source shows only when its package is installed.
- **Access:** readers are users in `AllowedUserGroups` (default `admin`) who also have the Users section; they see every tab, their own included. With `AllowSelfView` on, every other user sees their own activity only (profile route, or their tab if they have the Users section). Details and IP addresses go to readers only. IPs are off by default, export is off by default and view logging is on.
- **Docs:** README sections "Before you install", "IP addresses", "Use" + "Settings", "Sources" (what each keeps), "What Umbraco doesn't record", "Limits" and "Add your own source". Marketplace JSON, two screenshots (fake data), issue templates, CONTRIBUTING and CLAUDE.md "Behaviours to preserve".

## Decided by you this session

- Release tags are `release/<PackageMinor>.<PackagePatch>[-suffix]`, so **0.1 is tag `release/0.0`** (gives 17.0.0 and 18.0.0). The recorder minor will be `release/1.0`. The plan masters now say so.
- Task 30 exercised Umbraco.AI with one audit row written through Umbraco.AI's own audit service, not a real provider.
- QA-F3: the profile route was added in 0.1 (below).

## Final review (Task 29)

Verdicts are saved privately: `reviews/m4-final-qa-review.md` and `reviews/m4-final-security-review.md`.
- **qa-reviewer (opus): pass with changes.** No Critical, 4 Important.
- **security-analyst (opus): sign-off yes, with conditions.** No Critical or High. The conditions:
  1. fix SEC-F1, which is done;
  2. you confirm the GitHub-side publish settings (your to-do 4);
  3. CI goes green on GitHub.

**Fixed.** Each fix has a test that failed first:
- **QA-F1 (Important):** with a search on, or uSync over 5,000 entries, the total was capped at the rows on screen, so the pager offered no page 2. The engine now adds one when a source says more rows exist. Test: `InexactTotal_WithMoreRows_PromisesANextPage`.
- **SEC-F1 / QA-F6 (Medium):** Umbraco.AI's `SearchText` matches error messages that we never show, so a search could probe hidden error text. The `ai` source no longer searches: it is left out while a search is on, and `SearchText` is neither passed nor probed. Tests: `CannotSearch_SoTheEngineSkipsItDuringASearch` and `FiltersByKeyString`, plus the probe list test.
- **SEC-F5 (re-graded Low → Important):** an unexpected exception from an odd or hostile Umbraco.AI or uSync assembly could stop the site starting. Each guard now runs inside `AdapterRegistrations.Safely`, and the probe treats any exception as "not found". The Warning names the exception type only. Tests: `Register_Throws_ProbeFailedNamingTheExceptionTypeOnly`, `Probe_UnexpectedException_IsNotFound`.
- **QA-F3 (Important, your decision):** users without the Users section had no way to reach self-view. Now:
  - a `userProfileApp` (`UserActivity.UserProfileApp.Activity`) opens `UserActivity.Modal.MyActivity`, which holds the shared view locked to you with filters kept in memory;
  - design 6.2 is updated;
  - tested by `profile-app.test.ts` and by hand on both lines.
- **QA-F2 (Important):** the plan said tag `release/0.1`, which `check-release-version.sh` would refuse. The plan now says `release/0.0` and `release/1.0`.
- **README wording:**
  - QA-F4 / SEC-F2: a reused username shows the earlier person's uSync rows, and uSync history can be cleared from its dashboard.
  - SEC-F6: our "viewed" and "exported" rows are kept until you remove them.
  - SEC-F7: failed-sign-in text never goes into an export.
  - QA-F5: the menu item shows to Users-section users and the view says "not allowed".
  - QA-F9: switching off a claiming source hides its rows everywhere.
  - QA-F10: which sources a filter leaves out; search totals are minimums.
- **Presentation:**
  - QA-F11: real screenshots, and the NuGet and Marketplace tags now agree (`backoffice` added).
  - SEC-F8: `persist-credentials: false` on the publish build checkout.

**Not fixed (my rulings):**
- SEC-F8's SDK pin through `global.json`. Changing every build's SDK selection right before the release is riskier than leaving the `10.x` float.

**Deferred Minors (your call):**
- SEC-F3: for self-view users, search still matches fields they can't see. It only covers their own rows; `performingDetails` isn't searched.
- SEC-F4: no per-user rate limit on the list and detail endpoints.
- SEC-F9: Automate scheduled and webhook `initiatedBy` shapes are still unobserved.
- QA-F7: fixed in the patch candidate (the engine now looks up a username-only actor by username).
- QA-F8: the pager ignores the depth cap.
- From M3: QA-2, QA-5, QA-6, QA-8, QA-9, QA-11 and QA-12, unchanged.
- Seen in Task 30:
  - user items show "user / 2" (type and id) instead of the user's name;
  - each sign-in is followed by two core "System, Saved, user" rows (core updating the last-login date).

**Profile route review (after the fix pass):** a fresh `qa-reviewer` read the profile route (verdict: ship with changes, no Critical; saved privately as `reviews/m4-profile-route-review.md`). All six findings were fixed before the release:
- PR-1: component tests for "Your activity" (`profile-route.render.test.ts`). Each was proved by breaking the code it guards.
- PR-2: Playwright tests for the route and for its absence.
- PR-3: one modal alias constant.
- PR-4: explicit weight and a hidden host.
- PR-5: no module-level host in the access cache.
- PR-6: README wording.

**First release-checks run (on `2d3f084`): failed.** SQL Server integration failed on both lines in three Forms tests. Their stand-in `UFForms` table used SQLite-only types; the package code was fine. The fixture now uses Forms' SQL Server types, and the full integration suite passes on SQL Server locally (122/122 on 17 and 18).

**Presentation:** a new icon (a history clock on the navy tile) and four screenshots from a site with all five sources: the global view, a user's tab with an Automate run open, the filters, and "Your activity". They are in the README and the Marketplace JSON. The test `screenshots` in `tests/e2e/specs/activity.spec.ts` (`SCREENSHOTS=1`) retakes them.

## Exercise (Task 30)

Details are in `docs/spikes.md`, "M4 acceptance". On throwaway sites (`spikes/`, git-ignored) with Forms, Automate, uSync and Umbraco.AI installed, on SQL Server, on both lines:
- each action shows once with the right actor and source;
- the failed sign-in names nobody;
- there is no IP column by default, and with `ShowIpAddresses` on the reader sees the IP;
- the CSV prefixes `=` and keeps HTML as text;
- switching uSync off hides it;
- removing Umbraco.AI still boots with no error;
- the self-view profile route works, and other people's activity gives 403;
- anonymous requests get 401;
- our log categories contain no names, emails, item names or IPs.

The Playwright spec on the demo passed on 17 and 18 (7 passed, 1 skipped each).

## Test results (2026-11-10, final run)

| Command | 17 | 18 |
|---|---|---|
| `bash build/test.sh` | unit 247/247, integration 122/122, adapter 32/32 | same |
| integration on SQL Server (`Tests__Database__DatabaseType=SqlServer`) | 122/122 | 122/122 |
| `cd src/Umbraco.Community.UserActivity/Client && npm ci --ignore-scripts && npm run build && npm test` | Vitest 53/53, no `wwwroot/` drift | |
| client `tsc --noEmit` against `@umbraco-cms/backoffice@^18.3.0` | | clean |
| `bash build/check-xss.sh` | exit 0 | |
| `UmbracoMajor=<line> bash build/check-vulnerable.sh` | clean | clean |
| `bash build/pack.sh <dir>` | 17.0.0: deps Umbraco only, `staticwebassets/index.js`, only our DLL | 18.0.0: same |
| `npx playwright test --reporter=line` (demo up, env vars as `tests/e2e/.env.example`) | 9 passed, 1 skipped | 9 passed, 1 skipped |
| `actionlint`; `gitleaks git . --redact` | clean; no leaks | |

## Not verified, and why

- **Release checks** (SQL Server and the Playwright e2e in CI) haven't run on the M4 branch. PR checks (Build and Test, CodeQL, secrets, osv) run on every PR.
- **A real Automate run** (Task 30 Step 1). It needs a service user, workspace and automation set up by hand. The S3 row shapes are covered by integration tests.
- On 17, the IP, export, uSync-off and AI-removal checks weren't repeated. They ran on 18 only, and the code is the same.
- **SQLite first-boot stall (core, not this package).** On a fresh SQLite database, the demo seeder sometimes hangs creating the second user while core rebuilds its database cache. The stack showed no frame of ours, and a restart fixes it. CI's e2e uses SQL Server. This may be what caused the M1 slow stretch.
- SQL Server integration runs of the M3/M4 SQL. Release-checks covers both lines.
- From earlier milestones: Automate scheduled and webhook runs; Skybrud, the Workflow approve notification and a stable UrlTracker 18 (all M5).

## Your to-do list, in order

Done: milestone commit, PRs #3 to #5 (merged), release checks, the `nuget` environment, Trusted Publishing, tag `release/0.0`, publish (approved) and the GitHub release.

1. Check the package page once nuget.org has indexed it (10-30 minutes after 21:55 UTC): https://www.nuget.org/packages/Umbraco.Community.UserActivity. The Umbraco Marketplace picks it up from the `umbraco-marketplace` tag, usually within a day.
3. Repo settings (README "Repo settings"), if not done yet: secret scanning and push protection, private vulnerability reporting, Dependabot alerts, squash-only with branch delete, and topics `umbraco`, `umbracopackage`, `dotnet`, `audit`, `activity`, `users`, `security`, `gdpr`, `backoffice`. Rulesets for `main` and release tags exist, and CodeQL is on. Dismiss CodeQL `cs/web/missing-token-validation` if it appears (README).

## Earlier milestones (short)

- **M0:** spikes S1-S7, all GO (`docs/spikes.md`).
- **M1:** query layer, API and policy. The security Critical SEC-1 was fixed (details for readers only).
- **M2:** UI and e2e.
- **M3:** Automate, Forms, Archived Users, and the AI and uSync adapters, plus the start-up crash and DI-cycle fixes.

Review verdicts for every milestone are in the private workspace's `reviews/` folder.
