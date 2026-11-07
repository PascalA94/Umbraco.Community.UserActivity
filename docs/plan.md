# User Activity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Execution is **hybrid: a native build plus milestone reviews** (`qa-reviewer` at every milestone stop, plus `security-analyst` at the end of M1, in the 0.1 final review (Task 29) and at the recorder stop (Task 35)). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal (0.1, M0-M4):** Ship **one** NuGet package, `Umbraco.Community.UserActivity` `17.0.0` and `18.0.0`, from one commit: a per-user "Activity" tab on the user workspace and a global "Activity" view under the Users section, reading core `umbracoLog` + `umbracoAudit`, Automate and Forms rows through core tables, and Umbraco.AI, uSync, Archived Users (01) and Forms Audit Trail (02) through runtime adapters that load only when those packages are installed. Admin-only by default.

**Goal (next minor, M5):** the recorder (own table, going forward only) for CSP Manager, Workflow, Skybrud Redirects and UrlTracker, as `17.1.0` and `18.1.0`.

**Architecture:** A Razor class library with one Management API controller group (`user-activity`), a query engine that resolves the user once, asks each available and enabled `IUserActivitySource` for its top rows in a required date window and k-way merges them into an offset page, and one Lit client bundle that serves both views from shared components. Optional sources are compile-time-only references behind a guard and probe (design 3.2), so the nuspec depends on Umbraco only.

**Tech Stack:** .NET 10, `Umbraco.Cms.Api.Management` `[17.8.0, 18.0.0)` / `[18.3.0, 19.0.0)`, NPoco through `IScopeProvider`, TypeScript, Lit, Vite, `@umbraco-cms/backoffice` `^17.8.0` (type-checked against `^18.3.0`), `@hey-api/openapi-ts`, Vitest, xUnit, NSubstitute, `FakeLogger`, Playwright + `@axe-core/playwright` (release-checks only), GitHub Actions, gitleaks, osv-scanner, CodeQL.

**Spec:** the private workspace's `design.md` (copied to `docs/design.md`; its "Resolved by Pascal" sections are final and override older text). Portfolio rules: the portfolio README ("Token rules" and "CI and GitHub rules" win over this plan). Pitfalls: the Umbraco pitfalls reference. House style for the client, OpenAPI and user section: the Umbraco.Community.ArchivedUsers repo and the Umbraco.Community.BackofficeTidy repo (read only).

**Repo:** this repo (private, created). `SRC` = `src/Umbraco.Community.UserActivity`, `TESTS` = `tests/Umbraco.Community.UserActivity.Tests`, `CLIENT` = `SRC/Client`, `ADAPTERS` = `SRC/Sources/Adapters`.

**Before Task 1:** an independent plan review by `qa-reviewer` (README build approach). Its changes go in a "Plan review changes" section at the end of this file. **Workflow has a free licence, so nothing to prepare** (design P5).

## Time and model per milestone

Rates from the README (measured): about **0.32 h a task** for thin tasks, **0.7 h** for spike and security-heavy ones, and 1-2 h for the first CI run.

| Milestone | Tasks | AI time | Model | Why |
|---|---|---|---|---|
| M0 Spikes and gates | 1-6 | 4-5 h | **Opus** | Spikes on two lines; S7 now also installs several AI and uSync versions per line to measure adapter brittleness |
| M1 Query layer, settings, API, security | 7, 7a, 8-14 | 5.5-6.5 h | **Opus** | Raw SQL on core tables, the auth policy, IDOR and injection, plus source settings and availability |
| M2 UI | 15-19 | 2-2.5 h | **Sonnet** | Thin Lit work on known core components; e2e and axe at the end |
| M3 Sources and runtime adapters | 20-25 | 2.5-3 h | **Opus** (was Sonnet) | Optional-assembly loading fails silently or crashes start-up if it's slightly wrong; this is the new risk from the one-package decision |
| M4 Release 0.1 | 26-30 | 3.5-4.5 h | **Opus** | Privacy docs, first CI run, final review with a security-analyst pass, real exercise on both lines |
| **0.1 total** | 31 | **17-21 h** | | Plus about 2 h of Pascal's time: first CI run, release |
| M5 Recorder (next minor) | 31-36 | 3-3.5 h | **Sonnet** | A table, a job and thin handlers on the adapter pattern M3 proved; security-analyst at the stop |
| **Both releases** | 37 | **20-25 h** | | |

Reviewers keep their tiers (`qa-reviewer` and `security-analyst` on `opus`), whatever the session model.

## Global Constraints

- **One NuGet package.** The nuspec depends on `Umbraco.Cms.Api.Management` only. No add-on packages unless S7's fallback fires for one source, and then only for that source (Pascal decides at that gate).
- **Both lines from the start.** `net10.0`, `eng/lines.txt` = `17` and `18`.
  - `UmbracoMajor` `17` gives CMS `[17.8.0, 18.0.0)`; `18` (the default) gives `[18.3.0, 19.0.0)`.
  - `NuGetLockFilePath` = `packages.$(UmbracoMajor).lock.json`. Version `$(UmbracoMajor).$(PackageMinor).$(PackagePatch)`.
  - Adapter compile references per line in `Directory.Build.props` (`AICompileVersion`, `USyncCompileVersion`, `ArchivedUsersCompileVersion`, `FormsAuditTrailCompileVersion`; M5 adds `CspManager`, `Workflow`, `SkybrudRedirects`, `UrlTracker`), each referenced with `PrivateAssets="all" ExcludeAssets="runtime"`. A package with no release for a line is left out of that line's build with `#if` around its guard (S7 decides).
  - `scripts/test.sh` runs both lines. Expected once per line: `Passed!  - Failed:     0`.
- **The plan's build switch wins over the template's branch-per-line layout.** Scaffold with the template at `--umbraco-major 18`, then convert to the switch in Task 1, keeping the template's CI, config, test and e2e files.
- **Permanent names** (never change after the first release): NuGet ID `Umbraco.Community.UserActivity`; route `user-activity`, OpenAPI group `User Activity`, document `user-activity`; source aliases `core`, `automate`, `forms`, `ai`, `usync`, `archivedUsers`, `formsAuditTrail`, `recorder`; config names under `UserActivity:Sources` (`Core`, `Automate`, `Forms`, `AI`, `uSync`, `ArchivedUsers`, `FormsAuditTrail`); category strings (design 3.4); client aliases prefixed `UserActivity.`; table `communityUserActivityEvent` (M5).
- **Config section `UserActivity`**, keys and defaults from design 5 verbatim (`ShowIpAddresses: false`, `AllowSelfView: false`, `LogViews: true`, `Export:Enabled: false`, every `Sources:<Name>:Enabled: true`, `Recorder:RetentionDays: 0`), `ValidateOnStart`, errors name the key only.
- **No licence checks.** A source whose package isn't installed is hidden; one that is installed but unused just has no rows.
- **Optional-assembly rule:** nothing outside an adapter's own folder names an optional package's type. The composer calls the guard (strings and reflection only), then a separate `[MethodImpl(MethodImplOptions.NoInlining)]` method registers the adapter. A `NotInstalled` test host proves start-up with none of them present.
- **S1 [Critical]:** every controller is `ManagementApiControllerBase` with `[VersionedApiBackOfficeRoute("user-activity")]`. Never `[AllowAnonymous]`. Every action carries the `UserActivity.Read` policy or the per-user rule (design 7.2). All `GET`, `Cache-Control: no-store`.
- **Read-only on other stores.** No `INSERT`, `UPDATE` or `DELETE` against any table but `communityUserActivityEvent`, enforced by an architecture test that scans our SQL strings.
- **SQL:** parameters only; sort is an enum; listed columns, never `SELECT *`; `LIKE` escaping per design 3.8.
- **Never log** user names, emails, IPs, item names, search text, `Details` or `eventDetails`. Log source alias, counts, durations, exception types and an optional package's assembly version.
- **Client:** `@umbraco-cms/backoffice` is a `devDependency` and a Vite external; `npm ci --ignore-scripts`; committed `wwwroot/` matches a fresh build (CI drift check); no `innerHTML`, `unsafeHTML` or `unsafeStatic` (`scripts/check-xss.sh`); no custom colours or fonts, layout CSS only.
- **Pitfalls:** P1 (sources over factories, scopes per call), P2 [Critical] (no statics with request state), P7 (no network at start-up), S3 [Critical] (no user content as HTML).
- **CI (README rules, which override this plan):** public repo before the first CI push; PRs run fast checks only (four-leg build/test on SQLite, client build and drift, gitleaks over PR commits, osv-scanner `--config=./osv-scanner.toml`, check-vulnerable); the latest leg also restores the latest AI and uSync for the adapter integration tests; e2e, SQL Server and Playwright only in `workflow_dispatch` `release-checks.yml`; random per-run passwords (`openssl rand`, masked, `$GITHUB_ENV`); e2e on HTTPS; `UmbracoUseAppLocalIcu=false` in demo sites; scoped, visible-only locators; `workers: 1`; SHA-pinned actions; Dependabot `open-pull-requests-limit: 0`; no NuGet cache in `publish.yml`; fake secrets carry `// gitleaks:allow`.
- **Token rules:** one milestone per session; quiet output (`dotnet test --logger "console;verbosity=minimal"`, `dotnet build -v q`, line or dot reporters, `tail -n 50`); targeted tests per task, full suite at milestone ends; read only the task, Global Constraints, Review Focus and the design sections the task names; `/compact` only straight after a checkpoint.
- **Claude never commits, pushes, tags or publishes.** Each task ends with a "Checkpoint (Pascal commits)" line logged in `COMMITS-TODO.md`. Each milestone ends with a **Milestone commit** block (below). No AI attribution anywhere.

### Milestone stop (used by Tasks 6, 14, 19, 25, 30, 35)

1. Run the full suite on both lines: `./scripts/test.sh` and `cd CLIENT && npm test`. Expected: `Failed: 0` twice, Vitest green.
2. E2E only if the milestone changed UI (M2, M4): the Playwright spec on both demo sites over HTTPS.
3. Dispatch the milestone review named in the task, giving it `git diff <last milestone commit>..HEAD` plus the full files the diff touches, not the whole repo. Save the verdict to the private workspace's reviews folder (private, never the repo). Fix blocking findings under the owning task's tests. **A Critical security finding stops the session: ask Pascal.**
4. Write "Paused here (<date>)" in `COMMITS-TODO.md`, naming the next task.
5. Append the **Milestone commit** block to the end of `COMMITS-TODO.md`, then tell Pascal to run it before the next session:

```bash
git status --short          # check: no secrets, no appsettings.*.local, no user-secrets, no review docs
git add -A
git commit -m "<type>: <one line summarising the milestone>"
git log -1 --oneline
```

6. STOP.

## Review Focus

1. **A non-admin asks for someone else's activity** (per-user endpoint with another key, the detail endpoint with a guessed id, the global endpoint with `userKey` = self while only self-view is on): every one is 403. An admin opening **their own** tab gets 200. Test: Task 12 `PerUser_OtherKey_403`, `Detail_OtherActor_403`, `Global_SelfViewOnly_403`, `Admin_OwnTab_200`.
2. **An Automate run started by Sam** shows Sam as the actor and the service account as `Via`; the service account's tab shows it too; it appears **once**, not again as a raw core audit row. Test: Task 20 `AutomateRow_HumanIsActor_ServiceAccountIsVia` and Task 10 `ClaimedPrefix_SkippedByCore`.
3. **A content item named `<img src=x onerror=alert(1)>` or `=HYPERLINK("http://x")`** renders as text in the table and detail, and exports as `'=HYPERLINK(...)`. Test: Task 13 `Csv_FormulaPrefixed` and Task 19 e2e `hostile names render as text`.
4. **A site without Umbraco.AI or uSync, or with a version whose method changed:** the site boots, the source is hidden (not installed) or `failed` with one Warning (changed), and the rest of the view works. Test: Task 22 `NotInstalled_BootsAndHides`, `ProbeMissingMember_Unavailable`, `MissingMethodAtQuery_SourceFailedOthersOk`.
5. **Rows logged against id `-1`, `0`, a deleted user and a renamed uSync user** each show the right actor kind and never borrow another user's name; IPs never appear unless `ShowIpAddresses` is on. Test: Task 8 `Resolver_SpecialIds` theory, Task 23 `RenamedUser_ShowsStoredName`, Task 10 `IpAddress_NullByDefault`.

---

## File Map

```
Directory.Build.props  Directory.Packages.props  global.json  NuGet.config
LICENSE (MIT, Pascal Meyer)  README.md  SECURITY.md  CLAUDE.md  COMMITS-TODO.md  umbraco-marketplace.json
eng/lines.txt  osv-scanner.toml  scripts/{test,pack,check-xss}.sh
docs/{design,plan,spikes}.md                                   (nothing else)
assets/{icon.svg,icon.png,screenshots/}
src/Umbraco.Community.UserActivity.slnx
src/Umbraco.Community.UserActivity/
  UserActivityComposer.cs                      options, sources collection, guards, engine, policy, OpenAPI doc
  Configuration/{UserActivityOptions,SourceOptions,UserActivityOptionsValidator}.cs
  Sources/{IUserActivitySource,IClaimsCoreAuditRows,ActivityEvent,ActivityActor,ActivityEntity,
           ActivitySourceQuery,ActivitySourcePage,ActivitySourceInfo,Categories,
           UserActivitySourceCollectionBuilder,SourceAvailability}.cs
  Sources/Core/{CoreLogSource,CoreAuditSource,CoreSql,AuditTypeMap}.cs
  Sources/Automate/{AutomateSource,AutomateEventDetailsParser}.cs
  Sources/Forms/FormsSource.cs                 (shape from S2)
  Sources/Adapters/{OptionalAssemblyGuard,AdapterProbe}.cs
  Sources/Adapters/{AI,USync,ArchivedUsers,FormsAuditTrail}/{*Guard,*Source}.cs
  Identity/{UserIdentityResolver,ResolvedUser}.cs
  Query/{ActivityQueryEngine,ActivityQuery,ActivityPage,SourceStatus}.cs
  Security/{UserActivityPolicy,UserActivityAuthorizationHandler,ViewLogger}.cs
  Export/CsvWriter.cs
  Controllers/{ActivityEventsController,UserActivityEventsController,ActivityEventDetailController,
               FilterOptionsController,ActivityExportController}.cs
  Recorder/{RecorderSource,RecorderWriter,RecorderMigrationPlan,RetentionJob,ActivityEventDto}.cs   (M5)
  Recorder/Handlers/{CspManager,Workflow,SkybrudRedirects,UrlTracker}/{*Guard,*Handlers}.cs        (M5)
  Client/  (Vite, Lit; src/{index.ts,api/,components/,views/,conditions/,localization/})
samples/SampleSource/                          not packed
tests/Umbraco.Community.UserActivity.Tests/    unit + integration (SQLite), one file per production file
tests/Umbraco.Community.UserActivity.AdapterTests/   hosts with and without each optional package
tests/e2e/                                     Playwright, release-checks only
demo/DemoSite.V17/  demo/DemoSite.V18/         SQLite, not packed; Seed/ creates users and activity
.github/workflows/{build,release-checks,publish}.yml  .github/dependabot.yml  .github/ISSUE_TEMPLATE/
```

---

## M0: Spikes and gates (Opus, own session)

Throwaway spike code lives under `spikes/` and is deleted at the end of Task 6. Results go in `docs/spikes.md` with a GO/NO-GO line each.

### Task 1: Clone and scaffold, public before CI

**Files:** everything in the File Map down to empty folders, `TESTS/ScaffoldTests.cs`, `TESTS/PackageManifestTests.cs`, `demo/DemoSite.V17/`, `demo/DemoSite.V18/`

- [ ] **Step 1 (Pascal):** the repo `PascalA94/Umbraco.Community.UserActivity` exists, private and empty. Before anything is pushed, run Step 8's pre-public checks, then **make it public** (README rule 1). About: description, NuGet link, topics `umbraco`, `umbracopackage`, `dotnet`, `audit`, `user-activity`, `gdpr`. Secret scanning with push protection, CodeQL default setup (C# and JS/TS), squash-merge only.
- [ ] **Step 2:** `gh repo clone PascalA94/Umbraco.Community.UserActivity this repo. Confirm `git config user.email` prints `5427750+PascalA94@users.noreply.github.com`. Copy only `design.md` and `plan.md` into `docs/`; start `docs/spikes.md` with design section 0's [unknown] items. Expected: `ls docs` shows `design.md plan.md spikes.md`.
- [ ] **Step 3:** `dotnet new install the package template, then `dotnet new umbracopkg -n Umbraco.Community.UserActivity --umbraco-major 18` with the client option on (choose the parameters to fit this plan). Convert to the `UmbracoMajor` switch as in 17 and 18: constants, `UmbracoRange`, lock-file path, version prefix, `UmbracoFloat` for the latest legs, plus the empty adapter compile-version properties. Keep the template's CI, config, test and e2e files.
- [ ] **Step 4:** client scaffold as BackofficeTidy: one `bundle` extension `UserActivity.Bundle`, `@umbraco-cms/backoffice ^17.8.0` as a `devDependency`, Vite externals `[/^@umbraco-cms\/backoffice/]`, `vitest` + `happy-dom`. `StaticWebAssetBasePath` `App_Plugins/UserActivity`.
- [ ] **Step 5:** write `ScaffoldTests.Assembly_TargetsSelectedMajor`, `ScaffoldTests.Nuspec_DependsOnUmbracoOnly` (pack, read the nuspec, assert one dependency group per TFM holding only Umbraco packages) and `PackageManifestTests.Manifest_IdentityIsPermanent`. Run `./scripts/test.sh`. Expected: FAIL. Build the client, add the metadata, re-run. Expected: `Passed!` twice, both lock files, `wwwroot/index.js`.
- [ ] **Step 6:** demo sites from `Umbraco.Templates::17.8.0` and `::18.3.0`, SQLite, `UmbracoUseAppLocalIcu=false` before the template conditions, unattended admin `admin@example.test` with the password in `dotnet user-secrets` only. `ProjectReference` with the matching `UmbracoMajor`. A `Seed/` composer (Development only) creates users "Sam Admin" (`admin`), "Wren Writer" (`writer`) and "Ed Editor" (`editor`) with random passwords written to user-secrets, and some content.
- [ ] **Step 7:** `build.yml` (`permissions: {}`; jobs `build` four-leg matrix, `client` with build, Vitest, drift, `check-xss.sh` and `tsc --noEmit` against `^18.3.0`, `secrets`, `deps`; NuGet cache on) and `release-checks.yml` (`workflow_dispatch` only; HTTPS demo per line against SQL Server from `docker run`; random masked passwords; Playwright at `workers: 1`). Run `actionlint`. Expected: no output.
- [ ] **Step 8:** pre-public checks: `gitleaks dir . --redact` shows no leaks; the template's local-path lines are removed (the README pointer in `CLAUDE.md`); `grep -rnE "/Users/|~/Documents|~/Projects" . --exclude-dir=.git --exclude-dir=node_modules` prints nothing outside `docs/plan.md`'s Spec line (replace that line's path with "the private workspace").
- [ ] **Checkpoint (Pascal commits):** `chore: scaffold both lines, client, demo sites and CI`. Make the repo public (Step 1), push a branch, open a PR, confirm the checks are green in about 5 minutes, then add the ruleset requiring `build`, `client`, `secrets` and `deps`.

### Task 2: S1 user workspace tab and Users menu item, S6 authorisation

**Files:** `docs/spikes.md`, throwaway `spikes/S1/` client manifests and `spikes/S6/` handler

- [ ] **Step 1 (S1):** read the user workspace alias and context token from `@umbraco-cms/backoffice` (`packages/user/user/workspace/constants.ts`, `UMB_USER_WORKSPACE_CONTEXT`) and the Users section menu alias (`packages/user/section/menu/constants.ts`) on **both** `^17.8.0` and `^18.3.0`. Record the exact strings.
- [ ] **Step 2 (S1):** register a throwaway `workspaceView` (label "Activity", condition `Umb.Condition.WorkspaceAlias` = the user workspace alias) that prints the current user's unique from the workspace context. On each demo site, open Users > Wren > the tab, and Users > Sam Admin (own user) > the tab. Record: does it show, is the unique the user key, does it survive switching users, does it show on the admin's own user page.
- [ ] **Step 3 (S1):** register a throwaway `menuItem` "Activity" in the Users section menu that routes to a root workspace with an empty `umb-collection`. Record whether it shows after "User groups", and whether query strings (`?source=core&page=2`) survive a reload and back/forward on both lines.
- [ ] **Step 4 (S6):** a throwaway authorisation handler on a test Management API endpoint: read the caller's group aliases and Users section access on both lines, and find the core policy name for Users section access. Record. Call it as admin (200), as Ed Editor (403) and anonymously (401).
- [ ] **Step 5:** gate.

  | Outcome | Action |
  |---|---|
  | Tab and menu item work on both lines | **GO** |
  | Tab fails on a line | **Pascal decides:** (a) fallback on that line, a user entity action "View activity" opening the global view prefiltered, or (b) that line waits for a CMS minor that allows it |
  | Menu item can't be added to Users | **Pascal decides:** (a) a section view in Users, or (b) a dashboard in Users |
  | Query strings don't survive routing | **GO**, with filter state in a path segment instead (record the shape) |
  | Group or section access unreadable in the handler on a line | **NO-GO for that line** until solved: Pascal decides |

- [ ] **Checkpoint (Pascal commits):** `docs: S1 and S6 spike results`

### Task 3: S2 Forms history rows, S3 Automate audit rows

**Files:** `docs/spikes.md`, throwaway SQL in `spikes/S2S3/`

- [ ] **Step 1 (S2):** install Umbraco Forms (17.6.x / 18.2.x, unlicensed on localhost) on both demo sites. As Wren: create, save, trash, restore, roll back and delete a form. Query `umbracoLog` (listed columns) for rows after the start time. Record per action: `logHeader`, `entityType`, `userId`, `NodeId`, `logComment`, `parameters`. Also look for Forms' own history table or service, and whether it is public.
- [ ] **Step 2 (S2 gate):**

  | Outcome | Action |
  |---|---|
  | Rows in `umbracoLog` with the real user id | **GO**: `FormsSource` claims those `entityType`s; no Forms reference; available when `Umbraco.Forms.Core` is loaded (by name) |
  | Rows only in Forms' own store, with a public service | **Pascal decides:** (a) a fifth runtime adapter (`Umbraco.Forms.Core` compile-only), or (b) Forms out of 0.1 |
  | No user recorded | **NO-GO**: Forms out of 0.1; README says why |

- [ ] **Step 3 (S3):** install Umbraco.Automate on both demo sites. Run one automation manually as Wren, one on a schedule and one from the webhook trigger. Read the `umbracoAudit` rows with `eventType LIKE 'umbraco/automate/%'`. Record `performingUserKey`, `eventType` shape, the full `eventDetails` JSON shape with values replaced by `<redacted>` (shape only, no data), the `initiatedBy` forms seen, and whether a `CorrelationId` or run id is present.
- [ ] **Step 4 (S3 gate):** **GO** if the human key can be read from `eventDetails` on 18. On 17, S7 decides. If neither line has `initiatedBy`, **Pascal decides**: show the service account only, or Automate out of 0.1.
- [ ] **Checkpoint (Pascal commits):** `docs: S2 and S3 spike results`

### Task 4: S4 handler-time user for the recorder packages (feeds M5)

**Files:** `docs/spikes.md`, throwaway handlers in `spikes/S4/`

Runs now, though the recorder is the next minor, because Workflow's free licence needs no setup and the answer decides M5's scope.

- [ ] **Step 1:** install CSP Manager, Skybrud Redirects, UrlTracker and Umbraco Workflow (free licence) on the v18 demo site.
- [ ] **Step 2:** one throwaway `INotificationAsyncHandler<T>` per saved and deleted notification. Each logs only: the notification type, `HasUser` (is `IBackOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser` set), `UserKeyMatchesWren` (a bool), and `InAmbientScope` (is `ICoreScopeProvider.AmbientScope` non-null). Never the key or name.
- [ ] **Step 3:** as Wren, save and delete one item in each package from the backoffice. For Workflow, also approve a step. Record a row per notification. Repeat on the v17 site.
- [ ] **Step 4:** gate, per package: **GO** when `HasUser` and `UserKeyMatchesWren` are true on a backoffice save. **NO-GO** for that package otherwise; it leaves the M5 list and the README says why. If `InAmbientScope` is true, record it for design 4.1's rollback residual.
- [ ] **Checkpoint (Pascal commits):** `docs: S4 spike results`

### Task 5: S5 user matching and core table facts, S7 v17 vs v18 and adapter brittleness

**Files:** `docs/spikes.md`, throwaway `spikes/S7/` probe console app (reads assemblies only, never runs their code)

- [ ] **Step 1 (S5, tables):** on both lines and on SQLite plus SQL Server (local Docker), record for `umbracoLog` and `umbracoAudit`: the columns, the indexes, whether dates are UTC (write a row at a known UTC time and read it back), and whether `LogDto` and `AuditEntryDto` are public and usable from a package.
- [ ] **Step 2 (S5, scrubber):** read `LogScrubberJob` and the audit repository clean-up on both lines (`ilspycmd` or source at the release tag). Record which `umbracoLog` rows it deletes, at what age, and the setting name. This becomes design 4.2's first row.
- [ ] **Step 3 (S5, users):** record what `umbracoLog.userId` holds for the unattended admin, for a background job, and for a deleted user; what `umbracoAudit` holds for a failed login with an unknown username; and which `eventType` strings core writes for sign-in, sign-in failed, sign-out, password change and reset, user save and group save. Confirm `IUserIdKeyResolver` and `IUserService.GetAsync(key)` on 17.8.
- [ ] **Step 4 (S5, our packages):** in the Umbraco.Community.ArchivedUsers repo and the Umbraco.Community.FormsAuditTrail repo (read only), record what each stores per action, the user identity used, the public read service an adapter can call, and whether 01 already writes `umbracoAudit` rows (if yes, read those through core tables instead of an adapter, design 3.2). If 02 has no public read service, **Pascal decides**: add one there in its own release, or leave `formsAuditTrail` out of 0.1.
- [ ] **Step 5 (S7, line facts):** with Automate 17.x and Umbraco.AI on the v17 site: does `AuditTrailMiddleware` exist and write the same rows? Does `IAIAuditLogService` exist with the same signature? Does uSync 17 have `ISyncHistoryService`? Does 17.8 have distributed background jobs? Record a line-by-source table.
- [ ] **Step 6 (S7, adapter brittleness):** for Umbraco.AI and uSync, list every released version per line since the oldest we'd support (NuGet `v3-flatcontainer` index). For each, download the nupkg into its own empty folder and use the probe app (`System.Reflection.Metadata`, never loading or running their code) to check every type, method, parameter and property the adapter will use. Record a version × member table. Then on each demo site, install the oldest and the latest version in turn and call the real service once through a throwaway adapter compiled against the oldest. Record whether it binds.
- [ ] **Step 7 (S7 gate):**

  | Outcome | Action |
  |---|---|
  | Every member present on every version on a line | **GO**: one compile version per line |
  | A member changed once, with a clear version boundary | **GO**: the guard's probe picks between two small code paths, or the floor moves above the boundary (record which) |
  | Members change often, or binding fails on a released version | **Pascal decides** for that source only: (a) an add-on package `Umbraco.Community.UserActivity.<Name>` with an honest NuGet range, or (b) that source out of 0.1 |
  | No release with the service on a line | **GO**: the source is unavailable on that line (hidden), no extra package |

- [ ] **Checkpoint (Pascal commits):** `docs: S5 and S7 spike results`

### Task 6: M0 gate and stop

- [ ] **Step 1:** write the M0 summary at the top of `docs/spikes.md`: one GO/NO-GO line per spike, the fallbacks chosen, and the **Pascal decides** items still open.
- [ ] **Step 2:** update `docs/design.md` only where a spike changed a fact (mark the line `[verified: S<n>]`), and copy those edits back to the workspace `design.md`.
- [ ] **Step 3:** delete `spikes/`. `git status` shows only docs, scaffold and demo changes.
- [ ] **Step 4:** Milestone stop. Review: `qa-reviewer` (fresh, `opus`) reads `docs/spikes.md` and the diff, and checks each gate was applied as written. **Pascal reviews the spikes before M1** (README build approach). Milestone commit message: `docs: M0 spikes and gates for both lines`.

---

## M1: Query layer, settings, API and security (Opus, own session)

### Task 7: Options, contract and event model

**Files:** `SRC/Configuration/*`, `SRC/Sources/{IUserActivitySource,IClaimsCoreAuditRows,ActivityEvent,ActivityActor,ActivityEntity,ActivitySourceQuery,ActivitySourcePage,ActivitySourceInfo,Categories,UserActivitySourceCollectionBuilder}.cs`, `SRC/UserActivityComposer.cs`, `TESTS/{OptionsTests,ContractTests,ComposerTests}.cs`

**Interfaces:** design 3.3 and 3.4 verbatim (including `IsAvailable`); `builder.UserActivitySources()` extension. The contract lives in the main package, namespace `Umbraco.Community.UserActivity.Sources`.

- [ ] **Step 1:** failing tests: `Options_Defaults_MatchDesign` (`ShowIpAddresses` false, `AllowSelfView` false, `LogViews` true, export off, `Recorder:RetentionDays` 0); `Options_NegativeRetention_FailsNamingKey`; `Options_RangeOver366_Fails`; `Categories_ArePinned` (the exact list); `SourceAlias_Pattern` theory; `Composer_DuplicateAlias_FailsNamingBoth`; `Composer_DuplicateClaim_FailsNamingBoth`.
- [ ] **Step 2:** run them. Expected: FAIL.
- [ ] **Step 3:** implement. Records are immutable; `ActivityEvent.ToString()` prints source and id only.
- [ ] **Step 4:** `./scripts/test.sh`. Expected: PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: options, source contract and event model`

### Task 7a: Source settings and availability

**Files:** `SRC/Configuration/SourceOptions.cs`, `SRC/Sources/SourceAvailability.cs`, `SRC/Sources/Adapters/{OptionalAssemblyGuard,AdapterProbe}.cs`, `TESTS/{SourceSettingsTests,AvailabilityTests}.cs`

**Interfaces:** `UserActivity:Sources:<Name>:Enabled` (default `true`); `SourceAvailability.GetActive() : IReadOnlyList<IUserActivitySource>` (enabled **and** `IsAvailable`); `OptionalAssemblyGuard.IsLoaded(string assemblyName) : bool` (by name, no type use); `AdapterProbe.Check(Assembly, IReadOnlyList<MemberSpec>) : ProbeResult(bool Ok, string? MissingMember)`.

- [ ] **Step 1:** failing tests: `Sources_DefaultEnabled`; `Sources_NameCaseInsensitive` (`Sources:ai` equals `Sources:AI`); `Sources_Disabled_LeftOutOfEngineAndFilters`; `Sources_UnknownName_OneWarningNoError`; `Unavailable_HiddenEvenIfEnabled`; `Guard_ByName_NeverLoadsTypes`; `Probe_MissingMember_ReportsNameOnly`.
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement. `automate` and `forms` availability = their assembly name is loaded. No licence checks anywhere (a test greps for `License` in our source and finds only README text).
- [ ] **Step 4:** run. Expected: PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: per-source settings and availability`

### Task 8: User identity resolver

**Files:** `SRC/Identity/{UserIdentityResolver,ResolvedUser}.cs`, `TESTS/UserIdentityResolverTests.cs`

**Interfaces:** `Task<ResolvedUser?> ResolveFilterAsync(Guid key, CancellationToken)`; `Task<IReadOnlyDictionary<ActorRef, ActorDisplay>> ResolveManyAsync(IEnumerable<ActorRef>, CancellationToken)`, where `ActorRef` is one of `Id(int)`, `Key(Guid)`, `KeyString(string)`, `Username(string)`.

- [ ] **Step 1:** failing tests: `Resolver_SpecialIds` theory (`-1` → `DefaultAdmin`, `0` → `System`, unknown id → `Unknown` with "id 42", unknown key → `Unknown`); `Resolver_UsernameCaseInsensitive`; `Resolver_BatchesOneLookupPerKind`; `Resolver_CachesPerRequest`; `Resolver_DeletedUser_UsesStoredName`.
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement over `IUserIdKeyResolver` and `IUserService`, cached in `IRequestCache`. Values from S5 Step 3.
- [ ] **Step 4:** run. Expected: PASS.
- [ ] **Checkpoint (Pascal commits):** `feat: user identity resolver`

### Task 9: Core source, `umbracoLog`

**Files:** `SRC/Sources/Core/{CoreLogSource,CoreSql,AuditTypeMap}.cs`, `TESTS/Core/{CoreLogSourceTests,CoreSqlTests,AuditTypeMapTests}.cs`

- [ ] **Step 1:** failing tests:
  - `AuditTypeMap_EveryAuditType_HasCategory` (reflection over the enum on both lines);
  - integration on SQLite: rows written through `IAuditService` for save, publish, move, delete by Wren and Ed; `ByUserId_ReturnsOnlyThatUser`; `DateWindow_Inclusive`; `DefaultWindow_Last30Days`; `JoinsNodeName_AndFlagsDeleted`; `MinusOneObject_ShowsFileNotRecorded`; `Search_EscapesWildcards`;
  - `CoreSql_NoSelectStar`, `CoreSql_InjectionStrings_AreParameters` (the SQL text holds none of `'; DROP TABLE umbracoLog;--`, `%`, `_`, `[`);
  - `Total_Counts_WithSameFilters`.
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement with `Sql<ISqlContext>` and the DTOs where S5 says they're usable, otherwise `SqlSyntax`-quoted constants. Read-only scope, `autoComplete: true`. Add a why-comment linking design 3.8.
- [ ] **Step 4:** run. Expected: PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: core source for umbracoLog`

### Task 10: Core source, `umbracoAudit`, and claims

**Files:** `SRC/Sources/Core/CoreAuditSource.cs`, `TESTS/Core/CoreAuditSourceTests.cs`

- [ ] **Step 1:** failing tests: rows through `IAuditEntryService` for sign-in, failed sign-in, password reset, user save; `ByUserKey_Performer` (affected user shown in the detail); `EventTypeMap_FromS5` theory; `IpAddress_NullByDefault` and `IpAddress_ShownWhenSettingOn`; `ClaimedPrefix_SkippedByCore` (an `umbraco/automate/x` row is skipped when a claim exists, shown when not); `FailedSignIn_DetailsNotInSummary`.
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement. The claim list comes from the registered, available `IClaimsCoreAuditRows` sources.
- [ ] **Step 4:** run. Expected: PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: core source for umbracoAudit with ownership claims`

### Task 11: Query engine (merge, paging, partial results)

**Files:** `SRC/Query/*`, `TESTS/Query/ActivityQueryEngineTests.cs`, `TESTS/Query/PerfSeed.cs` (local only, skipped in CI)

- [ ] **Step 1:** failing tests with fake sources: `Merge_ThreeSources_OrderedByTimeSourceId`; `Merge_TiesStable`; `Page2_EqualsSliceOfFullMerge` (property test over random data); `DepthCap_SetsFlag_AndStops`; `Total_SumWhenAllCount_ElseInexact`; `SourceThrows_PartialResults_StatusFailed`; `SourceTimeout_PartialResults` (fake `TimeProvider`); `SourcesRunSequentially`; `DuplicateIdInSource_Dropped`; `ViaMatchesUserFilter`; `OnlyActiveSourcesQueried`.
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement per design 3.7. One Warning per failing source with alias and exception type only.
- [ ] **Step 4:** run. Expected: PASS. Then, locally only, seed 1M `umbracoLog` rows on SQL Server in Docker and time page 1 and page 100 of a 30-day window for one user. Record the numbers in `docs/spikes.md`. Over 2 s: **Pascal decides** (lower `MaxDepth`, or keyset paging).
- [ ] **Checkpoint (Pascal commits):** `feat: activity query engine with merge, paging and partial results`

### Task 12: Management API and policy

**Files:** `SRC/Controllers/{ActivityEventsController,UserActivityEventsController,ActivityEventDetailController,FilterOptionsController}.cs`, `SRC/Security/{UserActivityPolicy,UserActivityAuthorizationHandler}.cs`, `SRC/OpenApi/*` (as 01), `TESTS/Api/*`

**Interfaces:** design 5 endpoints, query names and response envelope verbatim. Operation ids `GetActivityEvents`, `GetUserActivityEvents`, `GetActivityEvent`, `GetActivityFilterOptions`.

- [ ] **Step 1:** failing tests:
  - `AllControllers_AreManagementApi_NoAnonymous` (reflection over the assembly);
  - admin 200 on every endpoint, including `Admin_OwnTab_200`; Ed Editor 403; anonymous 401;
  - `PerUser_OtherKey_403`; `PerUser_Self_NonAdmin_200_OnlyWhenAllowSelfView`; `Detail_OtherActor_403` (self-view user, guessed id); `Global_SelfViewOnly_403`;
  - `UnknownSource_400_NamesParameter`; `DisabledSource_400`; `TakeOver100_400`; `RangeOver366_400`; `SearchTooShort_400`; `NoStore_OnEveryResponse`;
  - `FilterOptions_ListsOnlyActiveSources`; `CustomGroup_InAllowedUserGroups_200`.
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement. Group aliases compared case-insensitively; the self-view rule lives in one place (`UserActivityAuthorizationHandler`) and the detail controller calls it with the loaded event's actor and `Via`. OpenAPI document `user-activity` registered per line, `#if UMBRACO_17` only if S1/S6 found a difference.
- [ ] **Step 4:** run. Expected: PASS on both lines. On each demo site, `curl -sk` without a token gives 401.
- [ ] **Checkpoint (Pascal commits):** `feat: user activity Management API with admin policy`

### Task 13: Export, view logging, leak and injection sweep

**Files:** `SRC/Export/CsvWriter.cs`, `SRC/Controllers/ActivityExportController.cs`, `SRC/Security/ViewLogger.cs`, `TESTS/{CsvWriterTests,ExportApiTests,ViewLoggerTests,LeakTests,ArchitectureTests}.cs`

- [ ] **Step 1:** failing tests:
  - `Csv_FormulaPrefixed` theory (`=`, `+`, `-`, `@`, tab, CR); `Csv_QuotesDoubled`; `Csv_Utf8Bom_FixedFilename`;
  - `Export_Disabled_404`; `Export_OverMaxRows_400`; `Export_SecondConcurrent_429`; `Export_NoIpColumnByDefault`;
  - `ViewLogger_TabOpen_OneRowPerHour`; `ViewLogger_Export_AlwaysLogs`; `ViewLogger_Off_NoRows`;
  - `LeakTests_NoPersonalDataInLogs`: sentinel name, email, IP, item name, search text and `eventDetails` through every API path and every failure path; none appear in `FakeLogger`, exception messages or `ToString()`;
  - `ArchitectureTests_NoWritesToOtherTables`; `ArchitectureTests_NoStatics` (P2); `ArchitectureTests_OptionalTypesOnlyInAdapterFolders` (no type from an optional assembly is named outside `Sources/Adapters/<Name>/` and, later, `Recorder/Handlers/<Name>/`).
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement per design 6.6, 7.1 and 7.3. View rows go through `IAuditEntryService` with `eventType` `community/useractivity/viewed` and `.../exported`.
- [ ] **Step 4:** run. Expected: PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: CSV export, view logging and leak tests`

### Task 14: M1 stop

- [ ] Milestone stop. Reviews, in parallel, on the M1 diff: `qa-reviewer` (fresh, `opus`) against Global Constraints, Review Focus 1 and 5, and design 3 and 5; `security-analyst` (fresh, `opus`) on design 7: IDOR on all three per-user paths, the admin-own-tab and self-view rules, the policy on both lines, SQL injection and `LIKE` escaping, export abuse (size, concurrency, formula injection), IP default off, log hygiene, and failed-login data exposure. Milestone commit message: `feat: query layer, source settings, Management API and security controls`.

---

## M2: UI (Sonnet, own session)

### Task 15: Client scaffold, generated client, shared components, URL state

**Files:** `CLIENT/src/api/*` (generated), `CLIENT/src/components/{activity-table,activity-filter-bar,activity-source-tag}.element.ts`, `CLIENT/src/state/activity-filter-state.ts`, `CLIENT/src/localization/en.ts`, Vitest files

- [ ] **Step 1:** generate the client from a demo site's `user-activity` OpenAPI JSON (`npm run generate:api`, as 01).
- [ ] **Step 2:** failing Vitest tests: `FilterState_RoundTripsUrl` (every filter, page, empty values dropped); `FilterState_DefaultIsLast30Days`; `FilterState_UnknownParamsIgnored`; `Table_RendersTextOnly` (a hostile name renders as text, no element created); `SourceTag_UsesLocalizedName`.
- [ ] **Step 3:** run `npm test`. Expected: FAIL.
- [ ] **Step 4:** implement with `umb-table`, `uui-*` and `umbLocalize` only; layout CSS only; S1's routing shape for the URL.
- [ ] **Step 5:** `npm test && npm run build && bash scripts/check-xss.sh`. Expected: pass, `wwwroot/` rebuilt, grep clean. Type-check against `^18.3.0` as 17's Task 5 Step 5.
- [ ] **Checkpoint (Pascal commits):** `feat: shared activity components and URL filter state`

### Task 16: Global Activity view under Users

**Files:** `CLIENT/src/views/global/*`, `CLIENT/src/manifests.ts`, Vitest files

- [ ] **Step 1:** failing tests: `Manifest_MenuItemInUsersSection` (S1 aliases); `GlobalView_AllFiltersPresent`; `SourceFilter_ListsOnlyActiveSources`; `Pagination_AnnouncesRange` (live region text "Showing 51 to 100 of 1,234 events").
- [ ] **Step 2:** implement the menu item, root workspace and collection with the filter bar (search, user picker, source, event type, item type, date presets and custom, default last 30 days), `uui-pagination`, depth-cap and partial-result boxes (design 6.1, 6.4).
- [ ] **Step 3:** `npm test && npm run build`. Expected: pass. On the v18 demo site as Sam Admin: the menu item shows, filters change the URL, reload restores them.
- [ ] **Checkpoint (Pascal commits):** `feat: global Activity view under the Users section`

### Task 17: Per-user Activity tab

**Files:** `CLIENT/src/views/user-tab/*`, `CLIENT/src/conditions/can-view.condition.ts`, Vitest files

- [ ] **Step 1:** failing tests: `Manifest_WorkspaceViewOnUserWorkspace` (S1 alias); `UserTab_UserFilterLocked` (no user picker, the request always carries the workspace's user key); `Condition_AdminSeesAnyTabIncludingOwn`; `Condition_NonAdminOwnTabOnlyWithSelfView`; `Condition_HiddenWhenFilterOptions403`; `Condition_CachedPerSession`.
- [ ] **Step 2:** implement per design 6.2, reusing Task 15's components.
- [ ] **Step 3:** `npm test && npm run build`. Expected: pass. On both demo sites: Sam sees the tab on Wren and on himself; Ed doesn't see it, and the API gives 403 if called directly.
- [ ] **Checkpoint (Pascal commits):** `feat: per-user Activity tab`

### Task 18: Detail sidebar, states, localisation

**Files:** `CLIENT/src/components/activity-detail-modal.element.ts`, `CLIENT/src/components/activity-states.element.ts`, `CLIENT/src/localization/en.ts`, Vitest files

- [ ] **Step 1:** failing tests: `Detail_ShowsViaAndDefaultAdminNote`; `Detail_NoIpRowWhenNull`; `Detail_JsonAsPreText`; `Detail_FocusReturnsToRow`; `Empty_ShowsRetentionHints`; `partial warning shows`; `Loading_SetsAriaBusy`; `Export_ButtonOnlyWhenEnabled`.
- [ ] **Step 2:** implement per design 6.3-6.6. Every string through `userActivity_*` keys.
- [ ] **Step 3:** `npm test && npm run build && bash scripts/check-xss.sh`. Expected: pass.
- [ ] **Checkpoint (Pascal commits):** `feat: event detail sidebar, empty, loading and partial states`

### Task 19: M2 stop (e2e, axe, screenshot draft)

**Files:** `tests/e2e/{activity.spec.ts,playwright.config.ts}`

- [ ] **Step 1:** Playwright spec (release-checks only, `workers: 1`, HTTPS, passwords from env vars, scoped visible-only locators, `expect(...).toPass()` on the click that opens a modal):
  - `admin filters global view and URL restores`;
  - `user tab shows only that user`, and `admin sees own tab`;
  - `detail opens and closes with keyboard, focus returns`;
  - `hostile names render as text` (seeded content named `<img src=x onerror=alert(1)>`; no dialog fires);
  - `editor sees no tab and gets 403 by URL`;
  - `axe has no violations` on both views and the open sidebar (`wcag2a`, `wcag2aa`, `wcag21aa`, `wcag22aa`).
- [ ] **Step 2:** run on both demo sites. Expected: all pass.
- [ ] **Step 3:** capture draft screenshots of both views with fake data into `assets/screenshots/` (final ones in Task 27).
- [ ] **Step 4:** Milestone stop. Review: `qa-reviewer` (fresh, `opus`) on the M2 diff against design 6, Review Focus 3, and the "feels native" goal (no custom colours, core components). Milestone commit message: `feat: global Activity view and per-user Activity tab`.

---

## M3: Sources and runtime adapters (Opus, own session)

Each source: golden-row tests first, then a thin mapper. No source may throw out of `QueryAsync` for bad data; it skips the row and counts it.

### Task 20: Automate source (core tables, no reference)

**Files:** `SRC/Sources/Automate/*`, `TESTS/Sources/AutomateSourceTests.cs`, `TESTS/Fixtures/automate/*.json` (shapes from S3, fake data only)

- [ ] **Step 1:** failing tests: `AutomateRow_HumanIsActor_ServiceAccountIsVia`; `Scheduled_IsAutomationKind`; `UnknownDetailsShape_FallsBackToPerformer_Flagged`; `DetailsDepthOver16_Skipped`; `ClaimsAutomatePrefix`; `NotInstalled_Hidden` (assembly name not loaded); per-line behaviour from S7.
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement over `umbracoAudit` with the claimed prefix, parsing `eventDetails` with `JsonDocument` (`MaxDepth = 16`).
- [ ] **Step 4:** run. Expected: PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: Automate source`

### Task 21: Forms source (shape from S2)

**Files:** `SRC/Sources/Forms/FormsSource.cs` (or `ADAPTERS/Forms/` if S2 chose an adapter), tests

- [ ] **Step 1:** failing tests from S2's recorded rows: each Forms action maps to its category; claims cover exactly S2's `entityType`s; a deleted form shows its stored name; `NotInstalled_Hidden`.
- [ ] **Step 2-4:** implement, run, expect PASS on both lines. If S2 was NO-GO, skip this task and record "Forms out of 0.1" for the README.
- [ ] **Checkpoint (Pascal commits):** `feat: Forms source`

### Task 22: Adapter pattern and the AI adapter

**Files:** `ADAPTERS/AI/{AIGuard,AISource}.cs`, `SRC/UserActivityComposer.cs`, `tests/Umbraco.Community.UserActivity.AdapterTests/*`

**Interfaces:** `AIGuard.TryRegister(IUmbracoBuilder) : SourceAvailabilityResult` (strings and reflection only; probes the members S7 listed); `AISource : IUserActivitySource`, alias `ai`, over `IAIAuditLogService.GetAuditLogsPagedAsync`.

- [ ] **Step 1:** failing tests:
  - unit, over a fake service: `FiltersByKeyString`; `NullUser_IsBackgroundSystem`; `PagesWithSkipTake_UsesTotal`; `RetentionHint_14Days_OrFromOptions`;
  - adapter host tests: `NotInstalled_BootsAndHides` (no AI assembly; start-up completes, `filter-options` has no `ai`); `Installed_Oldest_ReturnsRows` and `Installed_Latest_ReturnsRows` per line (S7 versions); `ProbeMissingMember_Unavailable` (a fake assembly built in the test without one method); `MissingMethodAtQuery_SourceFailedOthersOk` (one Warning with alias, exception type and assembly version only);
  - `Nuspec_HasNoAIDependency`.
- [ ] **Step 2:** run. Expected: FAIL.
- [ ] **Step 3:** implement per design 3.2: compile-only reference at the line's `AICompileVersion` (`PrivateAssets="all" ExcludeAssets="runtime"`), the guard, a `NoInlining` registration method, and failure isolation in the engine. Add a why-comment linking design 3.2.
- [ ] **Step 4:** `./scripts/test.sh` and the adapter tests. Expected: PASS on the lines S7 allows (the source is simply unavailable elsewhere).
- [ ] **Checkpoint (Pascal commits):** `feat: runtime adapter pattern and Umbraco.AI source`

### Task 23: uSync adapter

**Files:** `ADAPTERS/USync/{USyncGuard,USyncSource}.cs`, tests

- [ ] **Step 1:** failing tests: `MatchesUsernameCaseInsensitive`; `RenamedUser_ShowsStoredName`; `InMemoryFilter_CappedAt5000Entries` (history has no paging); `HistoryDisabled_Unavailable`; `RetentionHint_MayBeCleared`; plus the four adapter host tests from Task 22 for uSync.
- [ ] **Step 2-4:** implement on Task 22's pattern, run, expect PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: uSync source`

### Task 24: Archived Users and Forms Audit Trail adapters

**Files:** `ADAPTERS/{ArchivedUsers,FormsAuditTrail}/*`, tests

- [ ] **Step 1:** using S5 Step 4's findings, failing tests per source: actions map to categories (`archived` and `restored` map to `other` with a clear `RawType`); user identity matches; the four adapter host tests. If S5 found 01 writes `umbracoAudit` rows, `archivedUsers` is a core-table source with a claimed prefix instead, and its tests follow Task 20's shape.
- [ ] **Step 2-4:** implement, run, expect PASS. If S5 left a source out, skip it and record why.
- [ ] **Checkpoint (Pascal commits):** `feat: Archived Users and Forms Audit Trail sources`

### Task 25: M3 stop (contract docs, sample)

- [ ] **Step 1:** `samples/SampleSource/` (not packed, builds in CI) and a README section "Add your own source": the interface (in the main package), the event shape, claims, the `Sources:<Alias>:Enabled` setting a third-party source gets for free, and what is versioned and what isn't (design 5).
- [ ] **Step 2:** Milestone stop. Review: `qa-reviewer` (fresh, `opus`) on the M3 diff against design 3.2-3.6, Review Focus 2, 4 and 5, and the optional-assembly rule. Milestone commit message: `feat: Automate, Forms, AI, uSync, Archived Users and Forms Audit Trail sources`.

---

## M4: Release 0.1 (Opus, own session)

### Task 26: README privacy, sources and limits

**Files:** `README.md`

- [ ] **Step 1:** "Before you install": lawful basis note (design 7.1), tell staff, who can see what (admins any tab including their own; `AllowSelfView` for non-admins' own tab), `LogViews` on, export off by default, failed-login data warning, "not legal advice".
- [ ] **Step 2:** "IP addresses": off by default; the trade-off for compromised-account cases; how to switch `ShowIpAddresses` on for an investigation (admin-only) and off after.
- [ ] **Step 3:** "Sources": what each records and keeps (design 4.2 with the S2, S5 and S7 answers), the known core gaps (design 0), `Sources:<Name>:Enabled`, "a source appears only when its package is installed; no licence checks".
- [ ] **Step 4:** "Limits": user matching (design 3.5), de-dup by ownership (3.6), 30-day default window and depth cap, partial results, and "the recorder for CSP Manager, Workflow and redirects comes in 17.1 / 18.1".
- [ ] **Checkpoint (Pascal commits):** `docs: privacy, sources and limits`

### Task 27: Presentation, Marketplace and publish workflow

**Files:** `assets/*`, `umbraco-marketplace.json`, `README.md` (top), `.github/ISSUE_TEMPLATE/*`, `.github/CONTRIBUTING.md`, `.github/workflows/publish.yml`, `CLAUDE.md`

- [ ] **Step 1:** the README presentation checklist: icon, badges, "A community package, not made or supported by Umbraco HQ", "Requires Umbraco 17.8+ or 18.3+", one screenshot, absolute image URLs.
- [ ] **Step 2:** `umbraco-marketplace.json` with `AuthorDetails.ImageUrl`, tags from design 11, and two screenshots (global view with filters; a user's tab with the sidebar open), fake data only.
- [ ] **Step 3:** issue templates and `CONTRIBUTING.md` per README.
- [ ] **Step 4:** `CLAUDE.md` behaviours to preserve: permanent names, one package, the optional-assembly rule, read-only on other stores, the auth rule in one place, no personal data in logs, IPs off by default, text-only rendering, export off by default, one bundle for both lines. Keep it short and point to the README for token and CI rules.
- [ ] **Step 5:** `publish.yml`: on `release/*`, `scripts/pack.sh` (one package, both lines), trusted publishing, `id-token: write` only there, no NuGet cache, check-vulnerable first. `actionlint` clean. `unzip -l` shows `staticwebassets/index.js`; the nuspec shows Umbraco dependencies only and no AI, uSync, 01 or 02 DLLs in `lib/`.
- [ ] **Step 6 (Pascal):** reserve `Umbraco.Community.UserActivity` (`0.0.1-alpha`, push, unlist, 2FA) and add a Trusted Publishing policy.
- [ ] **Checkpoint (Pascal commits):** `docs: presentation, Marketplace and publish workflow`

### Task 28: First full GitHub CI run (Pascal, with the agent fixing)

- [ ] **Step 1 (agent):** pre-push checks: `./scripts/test.sh` passes on both lines; adapter tests pass; client tests, build and drift check pass; `gitleaks dir . --redact` clean; `ls docs` shows only `design.md plan.md spikes.md`; `git log --all --name-only --format= | grep -iE "scope\.md|review|council"` prints nothing; `grep -rn "uses: " .github/workflows | grep -vE "@[0-9a-f]{40}"` prints nothing; `grep -rnE "/Users/|~/Documents|~/Projects" . --exclude-dir=.git --exclude-dir=node_modules` prints nothing. State plainly in the handover: **CI has not run yet**.
- [ ] **Step 2 (Pascal):** push and open a PR. Expected: `build` (four legs), `client`, `secrets` and `deps` green in about 5 minutes.
- [ ] **Step 3 (Pascal):** `gh workflow run release-checks.yml --ref <branch>`. Expected: both lines green (HTTPS, SQL Server, the Task 19 spec and axe).
- [ ] **Step 4 (agent):** fix each failure under the owning task's tests, one round at a time. Record local-vs-GitHub differences in `docs/spikes.md`.
- [ ] **Checkpoint (Pascal commits):** `ci: fixes from the first GitHub run` (only if anything changed).

### Task 29: Final whole-branch review, with a security-analyst pass

- [ ] **Step 1:** dispatch `qa-reviewer` (fresh, `opus`) with the full branch, `docs/*` and the Umbraco pitfalls reference: findings by severity against Global Constraints, Review Focus, design 3-6 and the README rules, on both lines.
- [ ] **Step 2:** dispatch `security-analyst` (fresh, `opus`, in parallel) for design 7: authorisation and IDOR on every endpoint, the admin-own-tab and self-view rules, SQL injection, XSS from stored strings, CSV formula injection, export and paging caps, IP default off, personal data in logs, the optional-assembly loading (no type confusion or crash from a hostile or mismatched assembly), the GDPR README wording, and the dependency audit. **Critical findings can't be waived.**
- [ ] **Step 3:** save both verdicts to the private workspace's reviews folder (private). Fix blocking findings under the owning task's tests, rerun Task 28 Steps 1-3. Expected: green locally and on GitHub.
- [ ] **Checkpoint (Pascal commits):** `fix: final review findings`

### Task 30: Exercise it for real, on 17 and 18, then release 0.1

Run on **both** demo sites over HTTPS, with DevTools open (Console, Network).

- [ ] **Step 1:** as Wren: edit and publish content, sign out, fail one sign-in, run an automation, use Copilot once (if AI is on that line), run a uSync import, save a form. As Sam: open Users > Wren > Activity, then Sam's own tab. Expected: each action once, the right actor and `Via`, the right source tags, retention hints in the source filter, no IP column or row.
- [ ] **Step 2:** global view: filter by source, type, item type, a custom range and search; copy the URL into a new tab. Expected: the same view. With `ShowIpAddresses` on: IPs show in the detail for Sam only. With `Export:Enabled` on: open the CSV in a text editor and check a `=`-named item is prefixed.
- [ ] **Step 3:** sources: set `Sources:uSync:Enabled` false and restart: uSync disappears from the filter. Remove Umbraco.AI from the v18 demo site and restart: the site boots and `ai` is gone, with no error.
- [ ] **Step 4, negatives:** as Ed, the tab is absent and `curl -sk` to each endpoint gives 403; with `AllowSelfView` on, Ed sees only his own tab and gets 403 on Wren's and on a guessed detail id.
- [ ] **Step 5:** `grep -rciE "Wren|wren@|<searched text>|127\.0\.0\.1" demo/**/umbraco/Logs` prints only zeros for our log categories.
- [ ] **Step 6:** Milestone stop (no review beyond Task 29; record results in `docs/spikes.md`). Milestone commit message: `docs: acceptance results for 0.1`.
- [ ] **Step 7 (Pascal):** run `release-checks.yml`, then push the tag `release/0.1`. Expected: `17.0.0` and `18.0.0` on NuGet from one commit. `gh release create` per line, mark the 18 release Latest. Post an "own it, announce it" forum thread.

---

## M5: Recorder (next minor, `17.1` / `18.1`, Sonnet, own session)

Starts only after 0.1 is released. Before Task 31, re-read S4 in `docs/spikes.md`: drop any package S4 marked NO-GO. Re-run the S7 probe (Task 5 Step 6) for the GO packages' released versions. If one is too brittle, **Pascal decides** for that package: an add-on, or leave it out.

### Task 31: Recorder table, migration, writer, source and retention job

**Files:** `SRC/Recorder/*`, `TESTS/Recorder/*`

- [ ] **Step 1:** failing tests: `Migration_CreatesTableAndIndexes_Idempotent` (SQLite); `Writer_OwnScope_SwallowsAndLogsTypeOnly`; `Writer_CapsFieldsAndDetails`; `RecorderSource_MapsRows`; `Retention_DefaultZero_KeepsForever_LogsSettingOnce`; `Retention_Days_DeletesInBatchesOf1000`; `Retention_Negative_FailsStartUp`; `RecorderHint_KeptForeverWhenZero`; `UserDeleted_NullsUserName`.
- [ ] **Step 2-4:** implement per design 4 (distributed job, or the S7 fallback on 17.8), run, expect PASS on both lines.
- [ ] **Checkpoint (Pascal commits):** `feat: recorder table, writer and retention job`

### Task 32: CSP Manager, Skybrud Redirects and UrlTracker handlers

**Files:** `SRC/Recorder/Handlers/{CspManager,SkybrudRedirects,UrlTracker}/*`, tests

- [ ] **Step 1:** per package that S4 marked GO, failing tests: saved and deleted map to one row each; the user comes from a stubbed `IBackOfficeSecurityAccessor`; no user gives `UserKey = null`; a throwing writer never throws into the handler caller; `DetailsJson` holds only allow-listed keys (never the CSP policy text or redirect query strings); the adapter host tests (`NotInstalled_BootsAndRegistersNothing`, oldest and latest version bind).
- [ ] **Step 2-4:** implement on the M3 adapter pattern (compile-only reference, guard, `NoInlining` registration), run, expect PASS.
- [ ] **Checkpoint (Pascal commits):** `feat: recorder handlers for CSP Manager and redirects`

### Task 33: Workflow handlers

- [ ] **Step 1-4:** as Task 32, for the Workflow notifications S4 marked GO (save, approve, reject), using the free licence on the demo site.
- [ ] **Checkpoint (Pascal commits):** `feat: recorder handlers for Workflow`

### Task 34: README recorder and retention

**Files:** `README.md`

- [ ] **Step 1:** "Recorder": what it records, going forward only, no backfill, the rollback residual (design 4.1).
- [ ] **Step 2:** "Set a retention limit": `Recorder:RetentionDays` defaults to 0 (keep forever), the same as Forms Audit Trail, so nothing is deleted that a site owner hasn't chosen to delete; **set a number of days** to match your policy, because UK GDPR's storage limitation principle says personal data shouldn't be kept longer than needed (365 is a common choice for security logs). Put this in the install steps, not only in the reference.
- [ ] **Checkpoint (Pascal commits):** `docs: recorder and retention`

### Task 35: M5 stop

- [ ] Milestone stop. Reviews on the M5 diff: `qa-reviewer` (fresh, `opus`) against design 4, with "never throws into another package's save" checked explicitly; `security-analyst` (fresh, `opus`) on what the handlers store (no secrets or policy text in `DetailsJson`), the retention default and its README wording, and the new optional assemblies. Milestone commit message: `feat: recorder for CSP Manager, Workflow and redirects`.

### Task 36: Release the recorder minor

- [ ] **Step 1:** on both demo sites, as Wren, save and delete in each recorded package; as Sam, see the rows under the `recorder` source. With `RetentionDays: 0`, the start-up log names the setting once and the UI hint says "Kept forever (no limit set)". Remove CSP Manager and restart: the site boots, nothing registers.
- [ ] **Step 2 (Pascal):** run `release-checks.yml`, set `PackageMinor` to 1, tag `release/0.2`. Expected: `17.1.0` and `18.1.0` on NuGet. `gh release create` per line, 18 as Latest.
- [ ] **Checkpoint (Pascal commits):** `docs: recorder acceptance results`

---

## Resolved by Pascal (2026-11-07)

Copied from `design.md`:
- Two views from one query layer and component set; global view under Users; filter state in the URL; admin-only by default; native look; reader-first sources; a small public provider contract inside the main package; the Core provider built in.
- **P1** name `Umbraco.Community.UserActivity`, private repo created. **P2** recorder in the next minor (M5), 0.1 = M0-M4. **P3** last 30 days by default with paging; recorder retention 0 (keep forever) with a setting and a README instruction to set a limit. **P4** admins see any tab including their own; `AllowSelfView` (default false) for non-admins' own tab. **P5** Workflow free licence before M0, Forms unlicensed on localhost, no licence checks in the package. **P6** one package, per-source settings, runtime adapters for AI, uSync, 01 and 02, add-on only as S7's per-source fallback. **P7** log admin views and exports, on by default. **P8** `ShowIpAddresses` default false, admin-only when on.

Plan review changes and any security review changes are appended below when they run.

## Plan review changes (2026-11-07)

Independent plan review by `qa-reviewer` (fresh, opus); the review itself stays private in the private plan review. These changes override the task text above.

**M0**
- **S7 is metadata only (Task 5 Step 6).** Drop "install the oldest and the latest version in turn and call the real service". Real binding is proved by Task 22/23's `Installed_Oldest/Latest` host tests. "Oldest we'd support" = the oldest AI or uSync release whose Umbraco dependency admits the line floor (17.8.0 / 18.3.0). The gate's "two code paths" row means: a reflection-invoked path for the changed member, or the floor moves above the boundary (record which).
- **Public docs are sanitised copies (Task 1 Steps 2 and 8, Task 28).** The workspace `design.md` and `plan.md` are the masters. The repo's `docs/` copies are produced by a sanitiser in this folder: private paths become "the private workspace", research evidence paths become "research pass", and this section is replaced by a one-line note. Edit the master, then regenerate; never edit `docs/design.md` or `docs/plan.md` by hand. The pre-public grep is `grep -rnE "/Users/|~/|audit-src|\.\./[0-9]{2}-|scope\.md|plan-review" . --exclude-dir=.git --exclude-dir=node_modules` (`qa-reviewer` is left out: it names the reviewer agent, not private content). Expected: nothing.
- **Throwaway code stays out of git.** `.gitignore` has `spikes/` and `*.nupkg`. S7 downloads go outside the repo, one empty folder per nupkg. Task 6 Step 3: `git ls-files spikes` prints nothing.
- **Spike records hold shapes only.** Column names, types and shapes; values `<redacted>` or synthetic; never IPs, user keys, real names or typed usernames. S5 Step 3's failed login uses `nobody@example.test`.
- **S5 gate (Task 5, new Step 4a):** DTOs not public → `SqlSyntax`-quoted constants; dates not UTC → convert in `CoreSql` and record; no usable index on `umbracoLog.userId` or `umbracoAudit.performingUserKey` → **Pascal decides** before Task 11; 02 without a read service → as Step 4.
- **S3 gate (Task 3 Step 4):** GO per line where `initiatedBy` is present; a line without it shows the service account only and records the residual. If neither line has it, Pascal decides (as written).
- **Task 4:** install the four packages on v17 too; no v17 release = NO-GO for that line.
- **Task 6 exit check:** `grep -nE "Result: *$|GO/NO-GO: *$" docs/spikes.md` prints nothing.
- **Task 1 details:** latest legs restore unlocked; Step 7 also checks `osv-scanner.toml`, Dependabot limit 0 and SHA pins; the demo seed reads passwords from config (user secrets locally, env vars in CI) and never writes secrets; seed also creates hostile-named content (`<img src=x onerror=alert(1)>`, `=HYPERLINK("http://x")`); SQL Server in Docker uses `--platform linux/amd64` on Apple Silicon; S1 installs both backoffice versions with `npm ci --ignore-scripts`.

**M1-M5**
- Task 8: `Resolver_SpecialIds` adds `SuperUserKey` and `"system"`.
- Task 9: add `ClaimedEntityType_SkippedByCoreLog`.
- Task 12: add `FilterOptions_SelfViewUser_200`, `FilterOptions_NonAdminNoSelfView_403`, `Export_SelfViewUser_403`, `AllowedGroup_WithoutUsersSection_403`, `Detail_SelfViewUser_OwnViaEvent_200`, and 400s for `take<=0`, `skip<0`, `from>to`.
- Task 13: fake `TimeProvider` for `ViewLogger_TabOpen_OneRowPerHour` plus a different-subject case. "Read-only on other stores" means no SQL writes; view rows written through `IAuditEntryService` are by design.
- Task 20: add `FilterByHumanKey_FindsRun` and `FilterByServiceAccount_FindsRun`; the human filter is an escaped `LIKE` on `user:<key>` in `eventDetails`.
- Tasks 22-24, 32: AdapterTests build once per integration version (`-p:<Name>RuntimeVersion=`); rows are seeded through the integration's own service or table; the oldest version runs in release-checks, the latest on the PR latest leg. `Nuspec_HasNoAIDependency` covers uSync, 01 and 02 too.
- Task 27: GitHub topics, NuGet tags and Marketplace tags agree; repo settings add private vulnerability reporting and delete-branch-on-merge.
- Task 30 Step 5: filter logs to `"SourceContext":"Umbraco.Community.UserActivity` before grepping the sentinels (with `shopt -s globstar`). Expected: 0.
- Task 31: the migration idempotency test also runs on SQL Server in release-checks.
- Time table: reviewers are dispatched at opus by this plan (qa-reviewer's index tier is sonnet).

**Open for Pascal (before M4, not M0):**
- **Release tag scheme.** The plan says `release/0.1`; the template's `publish.yml` triggers on `release/<minor>.<patch>`; the README release steps say tag `vX.Y.Z`. Pick one before Task 27.
- **Task 30 "use Copilot once"** needs an AI provider account and key. Name the provider and key custody (user secrets), or seed an Umbraco.AI audit row instead.

## Pascal decides answers after M0 (2026-11-07)

These override the task text above.
- **Forms Audit Trail out of 0.1.** Task 24 builds only the `archivedUsers` source, and as a **core-table source** over `umbracoLog` (`entityType` `User`, header `Custom`, comment prefix `ArchivedUsers:`) following Task 20's shape; its claim covers entity type plus comment prefix. No 01 or 02 compile reference, so `ArchivedUsersCompileVersion` and `FormsAuditTrailCompileVersion` stay empty. The README (Task 26) says Forms Audit Trail history is not shown in 0.1. `formsAuditTrail` and `Sources:FormsAuditTrail` stay reserved names. The `NotInstalled` host test (Task 22) still runs with neither package present.
- **`umbracoAudit` without an index:** Task 10's `CoreAuditSource` bounds each query by the id range for the date window (PK seek for the first and last id in the window), then filters inside it. Task 11's perf seed covers `umbracoAudit` as well as `umbracoLog`.
