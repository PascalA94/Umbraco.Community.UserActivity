# Handover

**State (2026-11-10): 0.1 is released; M5 (the recorder, 17.1.0 / 18.1.0) is built and reviewed but not committed or released.** 17.0.0 and 18.0.0 are on nuget.org from tag `release/0.0`. The M5 work is in the working tree: run the "Milestone commit" block at the end of `COMMITS-TODO.md`. **Next: Task 36** (exercise the recorder on the demo sites, then you bump `PackageMinor` to 1, run release checks and tag `release/1.0`).

## M5: the recorder (built, not released)

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
  - Recorder entity types (`cspPolicy`, `workflowConfig`, `workflowInstance`, `redirect`) have no localised labels in the client yet; they show as their raw names.
  - From 0.1: the Minors listed below are unchanged.

### M5 test results (2026-11-10, final run)

| Command | 17 | 18 |
|---|---|---|
| `bash build/test.sh` | unit 307, integration 136, adapter 72 | unit 303, integration 136, adapter 62 |
| adapter tests against each line's newest releases (CSP 17.0.5 / 18.1.0, Workflow 17.5.4 / 18.2.5, UrlTracker 17.1.0) | pass | pass |
| `cd src/Umbraco.Community.UserActivity/Client && npm run build && npm test` | Vitest 54/54 | |
| `bash build/check-xss.sh` | exit 0 | |
| `UmbracoMajor=<line> bash build/check-vulnerable.sh` (security pass) | clean | clean |

**Not verified in M5** (all go to Task 36 Step 1): the recorder job under a real scheduler; the backoffice user for Skybrud add and edit (S4 saw it only for delete); which Workflow approved, rejected or completed notification fires per approval shape; whether UrlTracker or Skybrud raise notifications from non-backoffice triggers; no Playwright e2e was run (the only UI change is one hint string, covered by Vitest); `python3 ~/.claude/lib/qa.py plan` reports "QA is not configured on origin/main; nothing runs", so there was nothing to run.

**Task 36 Step 1, partial (2026-11-11):** the SQL Server run passed on both lines in a throwaway container: unit 307 / 303, adapter 72 / 62, integration 135 passed and 1 skipped of 136 (17 / 18). The skipped test was not identified; check the test output once. Not done: the demo exercise with CSP Manager, Skybrud, UrlTracker and Workflow (the demo references none of them), the job under a real scheduler, and the start-up log and "Kept forever" hint on a running demo. Step 2 is yours: bump `PackageMinor` to 1, run `release-checks.yml`, tag `release/1.0`, `gh release create` per line (18 as Latest).

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
- QA-F7: uSync actors aren't resolved to a user. They show the stored username, which looks like "S sam@example.test" with no link.
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
