# Handover

**State (2026-11-10, after M4): 0.1 is built, reviewed and exercised on both lines. It is not released yet.** CI runs on GitHub: Build and Test and CodeQL were green for the M2 and M3 PRs (#1, #2) and on `main`, and PR #3 (M4) is merging when its checks are green. M0 to M4 (Tasks 1-30) are done, except your steps: release checks, the NuGet ID and Trusted Publishing, and the release itself. M5 (the recorder, 17.1 / 18.1) starts only after 0.1 is released, on Sonnet.

## What 0.1 is

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

1. ~~Milestone commit~~ (done, `8769ba1`), ~~push and PR~~ (done, PR #3; merged once its checks are green).
2. ~~Release checks~~: the first run failed (Forms test fixture on SQL Server). It is fixed in the release-polish PR; re-run on `main` after that merges.
3. Before the first tag:
   - ~~Trusted Publishing policy~~ and ~~`NUGET_USER` secret~~ (done by you);
   - ~~`nuget` environment with you as required reviewer and a `release/*` tag rule~~ (done);
   - restrict tag creation.
   SEC-F8 escalated these because they can't be checked from the repo.
4. Repo settings (README "Repo settings"): CodeQL is on. Check secret scanning and push protection, private vulnerability reporting, Dependabot alerts, squash-only with branch delete, a ruleset on `main`, and topics `umbraco`, `umbracopackage`, `dotnet`, `audit`, `activity`, `users`, `security`, `gdpr`, `backoffice`. Dismiss CodeQL `cs/web/missing-token-validation` if it appears (README).
5. Release:
   - tag **`release/0.0`** on the merge commit of PR #3, which publishes `17.0.0` and `18.0.0`;
   - run `gh release create` per line and mark 18 as Latest;
   - post the "own it, announce it" forum thread.
6. Line up your local `main` after the squash-merge: `git fetch && git reset --hard origin/main`.
7. Tell the next session that 0.1 is released, then start M5 (Task 31) **on Sonnet**.

## Earlier milestones (short)

- **M0:** spikes S1-S7, all GO (`docs/spikes.md`).
- **M1:** query layer, API and policy. The security Critical SEC-1 was fixed (details for readers only).
- **M2:** UI and e2e.
- **M3:** Automate, Forms, Archived Users, and the AI and uSync adapters, plus the start-up crash and DI-cycle fixes.

Review verdicts for every milestone are in the private workspace's `reviews/` folder.
