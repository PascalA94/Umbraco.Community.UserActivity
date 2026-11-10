# Spikes (M0)

Results of the M0 spikes S1-S7 for both lines (17.8 and 18.3). Each spike ends with a `GO/NO-GO:` line.

**Recording rule:** names, types and shapes only. Values are `<redacted>` or synthetic. Never IPs, user keys, real names or typed usernames. The demo users are synthetic ("Sam Admin", "Wren Writer", "Ed Editor"); a failed sign-in uses `nobody@example.test`.

## M0 summary

M0 ran on 2026-11-07, on CMS 17.8 and 18.3 (SQLite spike sites, package metadata and CMS source at the release tags).

| Spike | GO/NO-GO | Fallback or note |
|---|---|---|
| S1 user tab, Users menu, URL state | **GO** both lines | Aliases identical on 17 and 18; filter state in query strings |
| S6 authorisation | **GO** both lines | Server section alias is `users`; the auth handler must not be Scoped |
| S2 Forms history | **GO** both lines | `umbracoLog` `entityType` `Form`, real user id; names from `UFForms` |
| S3 Automate rows | **GO** both lines | `initiatedBy` `user:<key>`; `replay:<runId>` is not a user; rows only for data-changing actions; scheduled and webhook not observed |
| S4 recorder handler user (M5) | **GO** CSP Manager, Workflow, UrlTracker on 17; **NO-GO pending M5** UrlTracker on 18 (beta only) and Skybrud Redirects (no save notification found) | Skybrud add raises no notification and its delete runs inside an ambient scope; Workflow approve raised no `Approved` notification; M5 re-checks (see "M5 handler probe": Skybrud has add/edit/delete notifications from 17.0.5) |
| S5 core tables and users | **GO** both lines | DTOs internal → `SqlSyntax` constants; UTC; sign-in actor is the *affected* user; `umbracoAudit` has no index; `archivedUsers` becomes a core-table source |
| S7 line facts and adapter brittleness | **GO** both lines | AI: all members on all 32 releases; uSync: history from 17.3.0; no add-on packages |

**Pascal decided (2026-11-07):**
- `formsAuditTrail`: **not in 0.1** (first chose a read service in 02, then dropped the source the same day to avoid coupling). Form actions stay covered by the `forms` source.
- `umbracoAudit` has no index: bound queries by id range for the date window (Task 10/11), confirmed by Task 11's perf test.

**Still open (not blocking M1):**
- Release tag scheme (`release/<minor>.<patch>` in the template vs `vX.Y.Z` in the README), before Task 27.
- Task 30's "use Copilot once" needs an AI provider account and key, or a seeded audit row instead.
- SQL Server facts were taken from source; Task 11's SQL Server run confirms them (needs Docker running).
- Automate scheduled and webhook `initiatedBy` shapes not observed: Task 20 fixtures treat them as `Automation`; observe on a demo site in M3 (Task 20).
- `umbracoLog` user filter rides inside `IX_umbracoLog_datestamp` (implementer ruling): Task 11 perf test.
- Self-service password change `eventType` not observed: check in Task 10.
- M5 (recorder) re-checks: Skybrud save/edit notification and 18 line; UrlTracker 18 stable; Workflow approve/complete notification.
- Demo seed in CI: release-checks passes the seed passwords; whether e2e needs a second boot is decided in M2 (Task 19).

## Open items from design section 0

| Item | Spike |
|---|---|
| `LogDto` and `AuditEntryDto` public and usable from a package | S5 |
| Which `umbracoLog` rows `LogScrubberJob` deletes, at what age, and the setting name | S5 |
| Dates stored as UTC in `umbracoLog` and `umbracoAudit`, on SQLite and SQL Server, both lines | S5 |
| Indexes on `umbracoLog` and `umbracoAudit` | S5 |
| Core facts on 17.8 (services, tables, DTOs) | S5 |
| User workspace alias, context token and Users menu alias; whether a third-party `workspaceView` shows on the user workspace | S1 |
| Caller's groups and section access readable in an authorisation handler; core Users-section policy name | S6 |
| Forms history: where rows go, with which user | S2 |
| Automate `eventDetails` shape, `initiatedBy` forms, `CorrelationId`; Automate 17.x audit middleware | S3, S7 |
| Handler-time user for CSP Manager, Workflow, Skybrud Redirects, UrlTracker | S4 |
| Umbraco.AI `IAIAuditLogService` and uSync `ISyncHistoryService` on 17; member stability across released versions | S7 |
| Distributed background jobs on 17.8 | S7 |
| Archived Users (01) and Forms Audit Trail (02): what they store, user identity, public read service, whether 01 writes `umbracoAudit` | S5 |

## S1: user workspace tab and Users menu item

Run 2026-11-07 on spike sites (CMS 17.8.x and 18.3.x, `@umbraco-cms/backoffice` 17.8.1 and 18.3.1), throwaway manifests only.

| Fact | 17.8 | 18.3 |
|---|---|---|
| User workspace alias (`UMB_USER_WORKSPACE_ALIAS`) | `Umb.Workspace.User` | `Umb.Workspace.User` |
| User workspace context token | `UMB_USER_WORKSPACE_CONTEXT` from `@umbraco-cms/backoffice/user` (`UmbWorkspaceContext`, entity type `user`) | same |
| Users section alias (client) | `Umb.Section.Users` | `Umb.Section.Users` |
| Users section menu alias | `Umb.Menu.UserManagement` | `Umb.Menu.UserManagement` |
| Core menu items | `Umb.MenuItem.Users` (weight 200), `Umb.MenuItem.UserGroups` (weight 100) | same |
| `workspaceView` on the user workspace (condition `Umb.Condition.WorkspaceAlias` = `Umb.Workspace.User`) shows as a tab | yes | yes |
| Workspace `unique` is a GUID equal to the user key in the URL (`.../workspace/user/edit/<key>`) | yes | yes |
| Follows a switch to another user | yes | yes |
| Shows on the admin's own user page | yes | yes |
| `menuItem` in `Umb.Menu.UserManagement` (weight 50, `entityType` of our own root) shows after "User Groups" | yes | yes |
| Menu item routes to a root workspace (`kind: default`, `meta.entityType`) at `.../workspace/<entityType>` | yes | yes |
| `?source=core&page=2` survives direct load and reload | yes | yes |
| `history.pushState` of a new query is not rewritten by the router; back and forward restore each query | yes | yes |
| Survives navigating away (Users) and back | yes | not re-run on 17 (same router) |

Notes:
- The tab route is `.../workspace/user/edit/<key>/view/<pathname>`.
- Result: everything works on both lines, with identical aliases, so one client bundle serves both.

Result: tab, menu item and query-string state all work on 17.8 and 18.3.

GO/NO-GO: **GO** on both lines (gate row 1). Filter state stays in query strings (no path-segment fallback needed).

## S6: authorisation

Run 2026-11-07 on both spike sites with a throwaway Management API controller (`ManagementApiControllerBase`, `[VersionedApiBackOfficeRoute]`) and a throwaway `AuthorizationHandler<T>`.

| Fact | 17.8 | 18.3 |
|---|---|---|
| Core policy for Users section access | `AuthorizationPolicies.SectionAccessUsers` | same |
| Server-side Users section alias in `IUser.AllowedSections` | `users` (`Constants.Applications.Users`), **not** the client alias `Umb.Section.Users` | same |
| In the handler: `IBackOfficeSecurityAccessor.BackOfficeSecurity.CurrentUser` set | yes | yes |
| In the handler: user key readable from claims (`context.User.Identity.GetUserKey()`) and `IUserService.GetAsync(key)` finds the user | yes | yes |
| Group aliases readable (`IUser.Groups[].Alias`) | yes | yes |
| A custom policy needs no explicit authentication scheme on `ManagementApiControllerBase` (the base controller's policy authenticates) | yes | yes |
| Admin (Sam) on the custom policy and on `SectionAccessUsers` | 200 / 200 | 200 / 200 |
| Editor (Ed) | 403 / 403 | 403 / 403 |
| Anonymous (`curl -sk`) | 401 / 401 | 401 / 401 |

Findings for Task 12:
- **Register the authorization handler as Transient or Singleton, never Scoped.** A scoped `IAuthorizationHandler` crashed start-up under scope validation on 18.3 (Development): core's singleton ServerEvents authorizers consume `IAuthorizationService`, which then depends on a scoped service.
- Compare sections with `Constants.Applications.Users`, and group aliases case-insensitively.
- A plain cookie `fetch` to the Management API from the page gives 401; the backoffice client adds its own `Authorization` header. Our client uses the generated API client, so this is only a note for e2e and manual checks.

Result: groups, section access and the user are readable in a handler on both lines; admin 200, editor 403, anonymous 401.

GO/NO-GO: **GO** on both lines.

## S2: Forms history rows

Run 2026-11-07 on both spike sites (Umbraco Forms 17.6.2 and 18.2.2, unlicensed on localhost), through the Forms Management API as Sam (admin group, a real user id, not `-1`; writers have no Forms section by default). Actions: create, save twice, trash, restore, roll back, trash, permanent delete.

| Fact | 17.8 + Forms 17.6.2 | 18.3 + Forms 18.2.2 |
|---|---|---|
| Rows written to `umbracoLog` | yes, one per action | yes, identical |
| `entityType` | `Form` | `Form` |
| `userId` | the acting user's real id | same |
| `NodeId` | Forms' own int id (`UFForms.NodeId`), **not** in `umbracoNode` | same |
| `logHeader` per action | create `New`; save `Save`; trash `Move`; restore `Move`; roll back `RollBack` then `Save`; permanent delete `Delete` | same |
| `logComment` / `parameters` shapes | `New`, `Save`, `Delete`: both null. Trash: fixed text "Moved to recycle bin" / `trash`. Restore: "Restored from recycle bin" / `restore`. Roll back: "Rolled back to version <guid>" in both | same |
| `umbracoAudit` rows from Forms | none | none |
| Forms' own history | `UFFormVersions` (`Id`, `Key`, `FormKey`, `Name`, `Definition`, `Workflows`, `CreatedBy` int, `CreateDate`, `PreventCleanup`); emptied by a permanent delete | same |
| Forms' audit read | `GET /umbraco/forms/management/api/v1/form/{id}/audit-log` returns core's audit shape (`AuditTypeModel`), i.e. the same `umbracoLog` rows | same |
| `umbracoLog` rows after a permanent delete | kept (form row and versions are gone, so only the int id remains) | same |

Consequences for Task 21:
- `FormsSource` claims `umbracoLog` `entityType` `Form`; the actor is the int `userId`; no Forms reference is needed.
- Names: join `UFForms` on `NodeId` for live forms (read-only, listed columns); a deleted form shows as "Deleted form (id N)". Reading `UFForms` is a dependency on a Forms table name; it fails soft if the table is absent.
- Trash and restore both log `Move`; map by `parameters` (`trash` → `trashed`, `restore` → `restored`), otherwise `moved`.
- Not exercised: form folder actions (check in Task 21 whether they log under another `entityType`).

Result: Forms writes one `umbracoLog` row per form action with the real user id, on both lines.

GO/NO-GO: **GO** on both lines (gate row 1): `FormsSource` claims `entityType` `Form`; no Forms reference; available when `Umbraco.Forms.Core` is loaded (by name).

## S3: Automate audit rows

Run 2026-11-07 on both spike sites (Umbraco.Automate 17.5.1 and 18.5.1), through the Automate Management API as Sam (admin; writers have no Automation section). Setup: an API-kind service user, one workspace using it, automations with a manual trigger. Automate needs `ConnectionStrings:umbracoAutomateDbDSN` or `Umbraco:Automate:UseNamedConnectionString` (here `umbracoDbDSN`), or its migrations fail and nothing runs.

| Fact | 17.5.1 | 18.5.1 |
|---|---|---|
| `AuditTrailMiddleware` present and writing `umbracoAudit` | yes | yes |
| Which steps write a row | only actions that change CMS data (seen: `createContent`); `logMessage` runs wrote **no** row | same |
| `eventType` shape | `umbraco/automate/<action alias with '.' as '/'>`, e.g. `umbraco/automate/umbracoAutomate/createContent` | same |
| `performingUserKey` / `performingUserId` | the workspace's **service account** | same |
| `performingDetails`, `performingIp` | set (a string, and an IP literal) | same |
| `affectedUserKey` / `affectedUserId` | null / 0 | same |
| `eventDetails` JSON shape | `{"automationId":"<guid>","runId":"<guid>","stepId":"<guid>","workspace":"<str>","initiatedBy":"<kind>:<id>"}` | same |
| `initiatedBy` for a manual run | `user:<human user key>` (the key matched the person who ran it) | same |
| `initiatedBy` for a replay | `replay:<original run id>`: the id is a **run**, not a user; the replaying person is not recorded | same |
| `CorrelationId` in `eventDetails` | no; `runId` is, and `umbracoAutomateRun` has `CorrelationId` and `InitiatedBy` | same |
| The step's own CMS change | a core `umbracoLog` row (`Document` / `Save`) with `userId` = the service account | same |

Not observed:
- **Scheduled run:** a `* * * * *` scheduled automation (Flexible, then Precise) never fired in about 25 minutes on the spike site, so `scheduled:` was not seen. Not investigated further; the design's research lists `scheduled`, `webhook`, `system` and `ai-agent`.
- **Webhook run:** the `plain-secret` authenticator had no secret configured and the spike didn't find where it is set, so the webhook call returned 401.

Consequences for Task 20:
- Parse `initiatedBy` as `<kind>:<id>`. Only `user:<guid>` names a human (actor = that user, `Via` = service account). Every other kind (`replay`, `scheduled`, `webhook`, `system`, `ai-agent`, unknown) is `Automation` with the service account as `Via`; never treat the id after `replay:` as a user.
- Rows exist only for data-changing actions, so the Automate source shows those steps, not every run. The README says so.
- The step's CMS change also appears as a core row by the service account; the design keeps both (3.6), linked by time and `runId`, not merged.

Result: on both lines the human key is readable from `eventDetails.initiatedBy` for user-started runs; the service account is the performer.

GO/NO-GO: **GO on both lines** (gate per plan review: `initiatedBy` present on each line).

## S4: handler-time user (feeds M5)

Run 2026-11-07 on both spike sites with one throwaway `INotificationAsyncHandler<T>` per notification. Each logged only the type name, `HasUser` (`IBackOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser` set), `UserKeyMatchesActor` (that user is the acting test user) and `InAmbientScope` (`IScopeAccessor.AmbientScope` set). Actions were done as Sam (admin) through each package's backoffice API or UI. Versions: CSP Manager 17.0.5 / 18.1.0, Skybrud Redirects 17.0.5 / 18.0.0, UrlTracker 17.1.0 / **18.0.0-beta.3** (no stable 18 release yet), Workflow 17.5.4 / 18.2.5 (free licence, no setup).

| Package | Notification (action) | 17 | 18 |
|---|---|---|---|
| CSP Manager | `CspSavedNotification` (save definition) | user yes, matches, no ambient scope | same |
| UrlTracker | `RedirectCreatedNotification`, `RedirectUpdatedNotification`, `RedirectDeletedNotification` (`UrlTracker.Core.Notifications`) | user yes, matches, no ambient scope | same |
| Skybrud Redirects | add (`PUT /umbraco/skybrud/redirects`) | **no** `RedirectSavedNotification` raised | not run |
| Skybrud Redirects | `RedirectDeletedNotification` (delete) | user yes, matches, **inside an ambient scope** | not run |
| Workflow | `WorkflowSettingsSavedNotification`, `WorkflowGroupDeletedNotification` | user yes, matches, no ambient scope | same |
| Workflow | `WorkflowInstanceCreatedNotification` (submit for approval) | not raised (17's node-config endpoint differs, so no instance was started) | user yes, matches, no ambient scope |
| Workflow | approve | n/a | the instance reached `Approved`, but `WorkflowInstanceApprovedNotification` was **not** raised for a one-step approval (probably `WorkflowInstanceCompletedNotification`, not hooked) |

Other facts:
- CSP Manager's notification carries only the definition (design section 0); the user comes from the accessor, which works.
- Skybrud's add path raised nothing the spike hooked; M5 must find its add/edit notification (or none exists).
- Skybrud's delete handler runs inside Skybrud's scope: design 4.1's rollback residual applies to it.
- UrlTracker has no stable 18.x release; 18.0.0-beta.3 behaves like 17.1.0.

Result: every notification that fired had the backoffice user, and it was the acting user, on both lines.

GO/NO-GO, per package:
- **CSP Manager: GO** (both lines).
- **UrlTracker: GO on 17. NO-GO on 18 until a stable 18.x ships** (the 18.0.0-beta.3 behaved like 17.1.0; M5 re-checks).
- **Workflow: GO** (both lines) for settings and approval groups; the approve/complete notification is an open item for M5.
- **Skybrud Redirects: NO-GO pending M5.** The gate needs the user on a backoffice save, and no save notification was found on 17 (add raised none). Delete has the user and runs inside an ambient scope. 18 not run.

## S5: user matching and core table facts

Run 2026-11-07: SQLite on both spike sites, metadata of `Umbraco.Cms.Core` / `Umbraco.Infrastructure` 17.8.0 and 18.3.0 (probe, never loaded), and CMS source at tags `release-17.8.0` and `release-18.3.0`. SQL Server was not run (Docker was down): its schema comes from the same DTO attributes, and Task 11's local SQL Server perf run confirms it.

**Tables (identical on 17.8 and 18.3):**

| Table | Columns | Indexes | FKs |
|---|---|---|---|
| `umbracoLog` | `id` int, `userId` int null, `NodeId` int, `entityType` text null, `Datestamp` datetime, `logHeader` text, `logComment` text null, `parameters` text null | PK; `IX_umbracoLog_datestamp` (`Datestamp`, `userId`, `NodeId`); `IX_umbracoLog_datestamp_logheader` (`Datestamp`, `logHeader`); `IX_umbracoLog` (`NodeId`) | `userId` → `umbracoUser.id` (no cascade) |
| `umbracoAudit` | `id`, `performingUserId` int, `performingUserKey` guid null, `performingDetails`, `performingIp`, `eventDateUtc`, `affectedUserId` int, `affectedUserKey` guid null, `affectedDetails`, `eventType`, `eventDetails` | **PK only** (no index on date or user) | none |

- **UTC:** both date columns default to `CurrentUTCDateTime`, and a site running as `TZ=Asia/Tokyo` (UTC+9) stored UTC times in both tables.
- **DTOs:** `LogDto` and `AuditEntryDto` are **internal** on both lines, as are `AuditEntryRepository` and `LogScrubberJob`. We use `SqlSyntax`-quoted table and column constants.
- **Scrubber (design 4.2 first row):** `LogScrubberJob` runs every 4 hours and deletes `umbracoLog` rows whose `logHeader` is `open` or `system` and that are older than `Umbraco:CMS:Logging:MaxLogAge` (default 24 hours). Every other `umbracoLog` row is kept. No job cleans `umbracoAudit`. Same code on both lines.
- `IUserIdKeyResolver.GetAsync(Guid)`, `GetAsync(int)` and the `TryGetAsync` overloads exist on 17.8 and 18.3, as do `IUserService.GetAsync(Guid)`, `GetAsync(IEnumerable<Guid>)` and `GetUsersById(int[])`.

**Who the rows name (18.3; 17.8 matched where checked):**

| Case | What is stored |
|---|---|
| Unattended / default admin | id `-1`, key = `Constants.Security.SuperUserKey` |
| Service calls with no user (our seed, `IContentService.Save` default) | `umbracoLog.userId` `-1` |
| System user saves (user create, failed-attempt counter) | `umbracoAudit` performer id `0`, key **null** |
| **Sign-in** (`umbraco/user/sign-in/login`) | performer = **default admin (`-1`)**; the person who signed in is the **affected** user |
| Sign-out (`umbraco/user/sign-in/logout`) | performer = affected = the user |
| Failed sign-in, known user | `umbraco/user/save` ("updating FailedPasswordAttempts", affected = that user) **and** `umbraco/user/sign-in/failed` (performer `-1`, affected `0`, no username stored) |
| Failed sign-in, unknown username (`nobody@example.test`) | **no row at all** |
| Admin sets another user's password | `umbraco/user/password/reset` (performer = admin, affected = user) |
| User save by an admin | `umbraco/user/save` (performer = admin, affected = user); `affectedDetails` holds the user's name and email |
| User group save | `umbraco/user-group/save` (performer = admin, affected `0`) |
| Deleted user | `umbracoLog.userId` has an FK without cascade; core only deletes users who never signed in, so most authored rows keep a live user |

Event types seen: `umbraco/user/save`, `umbraco/user/password/reset`, `umbraco/user/sign-in/login`, `umbraco/user/sign-in/logout`, `umbraco/user/sign-in/failed`, `umbraco/user-group/save`. A self-service password change was not exercised.

Consequences for Tasks 8 and 10: the actor of a sign-in row is the **affected** user, not the performer (map by `eventType`); performer `0`/null key is `System`; performer `-1` on sign-in/failed rows is not a real action by the default admin. The "failed sign-in" detail never contains the typed username, but a nearby `user/save` row's `affectedDetails` names the user.

**Our packages (read only):**
- **Archived Users (01):** writes its own `archivedUsersAudit` table (`userKey`, `action`, `performedByUserKey`, `performedUtc`, `source`, `reason`, `userNameSnapshot`, `reEnabled`, `context`) **and** a core `umbracoLog` row per action through `IAuditService` (`AuditType.Custom` header, `entityType` `User`, `NodeId` = target user id, `userId` = performer, comment `ArchivedUsers: <action> (<source>)`). Public `IArchivedUserService` has no history read method.
- **Forms Audit Trail (02):** its own EF tables `formsAuditTrailEntries` / `formsAuditTrailChanges`; each entry has `UserKey`, `UserName`, `FormId`, `FormName`, `Timestamp`, `EventType`, snapshots. Public services are only `IFormSnapshotService` and `IFormDiffService`; **no public read service**.

**Gate (plan review S5 gate and Step 4):**
- DTOs not public → `SqlSyntax`-quoted constants. Applied.
- Dates are UTC. No conversion needed.
- No usable index on `umbracoAudit.performingUserKey` (or its date) → **Pascal decided (2026-11-07): bound by id range** (find the id range for the date window through the PK, then filter inside it); Task 11's perf test confirms or reopens.
- `umbracoLog`: the only index with `userId` is `IX_umbracoLog_datestamp` (`Datestamp`, `userId`, `NodeId`). Ruling (implementer): usable, because every query has a required date window, so the engine range-seeks on `Datestamp` and filters `userId` inside the index without touching the table. Task 11's 1M-row perf test confirms it; over 2 s goes to Pascal there (plan Task 11 Step 4).
- 01 writes core rows → `archivedUsers` is a **core-table source** over `umbracoLog` (`entityType` `User`, header `Custom`, comment prefix `ArchivedUsers:`), not an adapter (design 3.2 exception). Task 24's claim must cover entity type plus comment prefix, because `User` alone is too broad.
- 02 has no read service → **Pascal decided (2026-11-07): Forms Audit Trail is not a source in 0.1**; the alias stays reserved.

Result: core facts recorded on both lines (SQLite plus source); two Pascal decisions taken.

GO/NO-GO: **GO** on both lines, with the two decisions above.

## S7: v17 vs v18 and adapter brittleness

Run 2026-11-07, metadata only: each nupkg was downloaded into its own empty folder outside the repo, and the probe (`System.Reflection.Metadata`) read the target DLL. No package code was loaded or run. Real binding is proved later by Task 22/23's `Installed_Oldest/Latest` host tests.

**Line facts:**

| Source | 17 | 18 |
|---|---|---|
| Automate `AuditTrailMiddleware` (internal) writes `umbracoAudit` | yes (17.5.1; same rows as 18, see S3) | yes (18.5.1) |
| Umbraco.AI `IAIAuditLogService.GetAuditLogsPagedAsync(AIAuditLogFilter, int, int, CancellationToken)` | yes, every release 17.0.0-17.7.0 | yes, every release 18.0.0-18.7.0 |
| uSync `ISyncHistoryService` (`GetHistoryAsync()`, `IsEnabled()`) in `uSync.History` | from **17.3.0** (absent in 17.0.0-17.0.4) | every release 18.0.0-18.3.0 |
| `IDistributedBackgroundJob` (public) | yes (17.8.0) | yes (18.3.0) |

**Members the adapters use** (all present unless noted):
- AI: `IAIAuditLogService.GetAuditLogsPagedAsync` returning `Task<(IEnumerable<AIAuditLog>, int)>`; `AIAuditLogFilter.UserId` (string), `FromDate`, `ToDate`; `AIAuditLog.Id`, `StartTime`, `UserId`, `UserName`, `EntityId`, `EntityType`, `FeatureType`, `Status`; `AIAuditLogOptions.RetentionDays`. The filter also has `SearchText`.
- uSync: `ISyncHistoryService.GetHistoryAsync()` (no paging), `IsEnabled()`; `HistoryInfo.Date`, `Username`, `Method`, `Changes`, `Total`, `Actions`.

**Version × member table:**

| Package | Line | Versions checked | Umbraco range admits the floor | Missing members |
|---|---|---|---|---|
| `Umbraco.AI.Core` | 17 | 17.0.0, 17.1.0, 17.1.1, 17.2.0, 17.3.0, 17.3.1, 17.3.3, 17.3.4, 17.3.5, 17.4.0, 17.5.0, 17.5.1, 17.5.2, 17.6.0, 17.6.1, 17.7.0 | yes (`[17.4.0, 17.999.999)` then `[17.5.0, ...)`) | none |
| `Umbraco.AI.Core` | 18 | 18.0.0 to 18.7.0 (16 releases) | yes (`[18.0.0, 18.999.999)`) | none |
| `uSync.History` | 17 | 17.0.0-17.0.4 | yes (open-ended `>=`) | `ISyncHistoryService` |
| `uSync.History` | 17 | 17.3.0 to 17.6.1 (14 releases) | yes | none |
| `uSync.History` | 18 | 18.0.0 to 18.3.0 (13 releases) | yes | none |

**Gate:**
- Umbraco.AI: every member present on every version on both lines → **GO**, one compile version per line: `AICompileVersion` 17.0.0 / 18.0.0.
- uSync: one change with a clear boundary (the service arrives in 17.3.0) → **GO** by gate row 2's **"the floor moves above the boundary"** option: compile against 17.3.0 on 17 (`USyncCompileVersion` 17.3.0 / 18.0.0); on 17.0.x the guard's probe finds no `ISyncHistoryService` and the source is hidden. No reflection path.
- No add-on package is needed.

Result: no brittleness found in any released version of either integration on either line.

GO/NO-GO: **GO** on both lines (gate rows 1 and 2).

## M1 results (Task 10 and Task 11)

Run 2026-11-09 on the 18.3 line, local Docker SQL Server 2022 (`linux/amd64`) and SQLite.

**Core audit event types (Task 10):** read from the `Umbraco.Core`, `Umbraco.Infrastructure` and `Umbraco.Cms.Api.Management` assemblies at 17.8.0 and 18.3.0 (identical): `umbraco/user/sign-in/login`, `.../logout`, `.../failed`, `umbraco/user/save`, `umbraco/user/delete`, `umbraco/user/password/reset`, `umbraco/user/password/change` (the self-service change, open since S5), `umbraco/user/password/forgot/request`, `umbraco/user/password/forgot/change`, `umbraco/user-group/save`, `umbraco/user-group/permissions-change`, `umbraco/member/save`, `umbraco/member/delete`, `umbraco/member/exported`, `umbraco/member/roles/assigned`, `umbraco/member/roles/removed`.

**SQL Server (closes the S5 SQL Server item):** the whole integration suite (core `umbracoLog` and `umbracoAudit` reads: UTC windows, `LIKE ... ESCAPE` with `%`, `_`, `[` and `\`, the claims, the id-range bound) passes on SQL Server as on SQLite.

**Performance (Task 11 Step 4):** 1,000,000 `umbracoLog` rows and 1,000,000 `umbracoAudit` rows over 90 days, 20 users, ids growing with time. The `core` source, 30-day window, warm plan cache:

| Query | Time | Rows returned | Total |
|---|---|---|---|
| One user, page 1 (top 50) | 68 ms | 50 | 33,332 |
| One user, page 100 (top 5,000) | 88 ms | 5,000 | 33,332 |
| All users, page 1 | 96 ms | 50 | 666,668 |
| All users, page 100 | 127 ms | 5,000 | 666,668 |

| All users, 366-day search matching nothing (full `LIKE` scan, no count), page 1 | 167 ms | 0 | not counted |
| Same, page 100 | 169 ms | 0 | not counted |

The last two rows were added after the M1 review: searches are no longer counted (design 3.7, "count with indexed filters only"), and core's database work is bounded by `SourceTimeoutSeconds` as a command timeout.

**Cross-site requests (M1 review):** on both lines a request carrying only the access-token cookie gets 401 (the Management API needs the bearer header, which a cross-site link can't add), and the `__Host-umbAccessToken` cookie is `SameSite=Strict`. `CrossSiteRequestTests` keeps checking it.

**Core's `performingDetails`:** a real sign-in row stores the default admin as `User "<name>"` in `performingDetails` (seen in the M1 integration run), which is why event details go to readers only.

Result: well under the 2 s gate. The `umbracoLog` user filter inside `IX_umbracoLog_datestamp` and the `umbracoAudit` id-range bound (Pascal, 2026-11-07) both hold; no change to `MaxDepth` or paging.

GO/NO-GO: **GO** (no Pascal decision needed).

## M4 acceptance (Task 30), 2026-11-10

Run on both lines over HTTPS on throwaway exercise sites (outside git): the demo site plus Umbraco Forms (17.6.2 / 18.2.2), Umbraco Automate (17.5.1 / 18.5.1), uSync (17.6.1 / 18.3.0) and Umbraco.AI (17.7.0 / 18.7.0), on SQL Server in a container. Synthetic users only (Sam admin, Wren writer, Ed editor, `example.test` addresses). The AI action is one audit row written through Umbraco.AI's own audit service (no provider call, Pascal 2026-11-10).

| Check | 17 | 18 |
|---|---|---|
| Wren: failed sign-in, sign-in, page save, sign-out; each shows once with the right actor and source | yes | yes |
| Failed sign-in names nobody (S5) | yes | yes |
| Sam: form create shows under Umbraco Forms with the form's name | yes | yes |
| Sam: uSync export shows under uSync (uSync writes no history entry for an import with no changes) | yes | yes |
| Umbraco.AI row shows under Umbraco.AI with Wren as actor | yes | yes |
| No IP column or value with the defaults | yes | yes |
| Global view: 4 sources and 5 event types from a pasted URL restore the same view | - | yes |
| `ShowIpAddresses: true`: the reader's detail shows the sign-in IP (the loopback address) | - | yes |
| `Export:Enabled: true`: CSV has `'=HYPERLINK(...)` prefixed and `<img ...>` as text | - | yes |
| `Sources:uSync:Enabled: false`: uSync gone from the source filter | - | yes |
| Umbraco.AI removed: site boots with no error, `ai` gone | - | yes |
| `AllowSelfView: true`: a writer (no Users section) opens **Your activity** from the profile and sees only their own events; the page URL is unchanged | yes | yes |
| Same session, someone else's per-user endpoint and the global endpoint | - | 403 and 403 |
| `AllowSelfView: false`: no **Your activity** box for a writer | - | yes |
| Anonymous requests to the four endpoints | - | 401 |
| Our log categories: lines containing a name, email, item name or IP | 0 | 0 |
| Playwright spec on the demo (fresh SQLite) | 7 passed, 1 skipped (screenshots) | 7 passed, 1 skipped |

Not exercised: a real Automate run (needs a service user, workspace and automation set up by hand; the S3 row shapes are covered by the integration tests).

Found on the way:
- **SQLite first-boot stall (core, not this package).** On a fresh SQLite database the demo seeder hung creating its second user while core rebuilt the database cache ("Database cache was serialized using None ... Rebuilding database cache"). The stack showed only the seeder, busy-retrying an `INSERT` into `umbracoUser`, with no frame of this package. A restart (no rebuild) seeded normally. Same family as CI rule 12; the CI e2e runs on SQL Server.
- **Self-view had no route for users without the Users section** (final review QA-F3). Fixed in 0.1 with the profile route (design 6.2).

## M5 handler probe (2026-11-10)

Re-check of S4 and an S7-style metadata probe for the recorder (M5 Tasks 32-33). Each nupkg was downloaded into its own empty folder outside the repo and its DLL read with `System.Reflection.Metadata`; no package code was loaded or run. Shapes only. The probe lists every stable release (prereleases excluded) on NuGet that day; "admits the floor" means the nuspec's Umbraco range includes 17.8.0 or 18.3.0.

| Package (assembly) | Line | Stable releases probed | Admit the floor | Notifications and members the handlers use | Missing in any release |
|---|---|---|---|---|---|
| `Umbraco.Community.CSPManager` | 17 | 17.0.0, 17.0.1, 17.0.2, 17.0.4, 17.0.5 | all | `CspSavedNotification.CspDefinition`; `CspDefinition` `Id` (Guid), `Enabled`, `ReportOnly`, `IsBackOffice` (bool), `Sources` (list) | none; identical metadata in all 8 releases |
| `Umbraco.Community.CSPManager` | 18 | 18.0.0, 18.0.1, 18.1.0 | all | same | none |
| `UrlTracker.Core` | 17 | 17.0.0, 17.1.0 | both (`Umbraco.Cms.Infrastructure >= 17.1.0`) | `RedirectCreatedNotification`, `RedirectUpdatedNotification`, `RedirectDeletedNotification` over `RedirectNotificationBase.Redirects` (collection of `IRedirect : IEntity`: `Key`, `SourceValue`, `Permanent`, `Force`, `RetainQuery`, `Advanced`) | none; identical in both |
| `UrlTracker.Core` | 18 | none (18.0.0-beta.1 and 18.0.0-beta.3 only) | n/a | n/a | **no stable 18.x release** |
| `Skybrud.Umbraco.Redirects` | 17 | 17.0.0 to 17.0.5 (six) | all (`[17.0.0, 17.999.0)`) | `RedirectAddedNotification`, `RedirectSavedNotification`, `RedirectDeletedNotification`, each with `Redirect` (`IRedirect`: `Key`, `Path`, `Type`, `IsPermanent`) | all three notifications are **absent in 17.0.0-17.0.4** (lookup notifications only); present, identical, from **17.0.5** |
| `Skybrud.Umbraco.Redirects` | 18 | 18.0.0 | yes (`[18.0.0, 18.999.0)`) | same as 17.0.5 | none |
| `Umbraco.Workflow.Core` | 17 | 17.0.0-17.0.2, 17.1.0-17.1.1, 17.2.0, 17.3.0-17.3.6, 17.4.0-17.4.2, 17.5.0-17.5.4 (21) | all (ranges start at 17.0.0 up to 17.7.0, all open to 17.999.999) | `WorkflowSettingsSavedNotification`, `WorkflowGroupSavedNotification`, `WorkflowGroupDeletedNotification` (`SavedEntities` / `DeletedEntities` of `IWorkflowGroup : IWorkflowGroupBase` with `Name`, `Unique`), `WorkflowInstanceCreatedNotification` (`CreatedEntity`), `WorkflowInstanceApprovedNotification` and `WorkflowInstanceRejectedNotification` (`UpdatedEntity`), `WorkflowInstanceCompletedNotification` (`WorkflowType`); `IWorkflowInstance.WorkflowType` | none |
| `Umbraco.Workflow.Core` | 18 | 18.0.0-18.0.3, 18.1.0-18.1.2, 18.2.0-18.2.5 (13) | all (from `[18.0.0, 19.0.0)`) | same notifications, plus `WorkflowConfigSavedNotification` (only on 18) | none; but `IWorkflowInstance`, `ISettings`, `IWorkflowConfig` and the `WorkflowType` enum **moved namespace** between the lines (`Umbraco.Workflow.Core.Interfaces` / `.Models.Enums` on 17, `.ContentApprovals.*` / `.Settings` / `.Common.Interfaces` on 18), so the handlers never name them |

**Floors (compile references, `Directory.Build.props`):** CSP Manager 17.0.0 / 18.0.0; Workflow 17.0.0 / 18.0.0; UrlTracker 17.0.0 / none; Skybrud 17.0.5 / 18.0.0. Skybrud's 17.0.5 is above the boundary at which its notifications arrive (the same treatment as uSync in S7, gate row 2): on 17.0.0-17.0.4 the probe fails and the recorder for Skybrud stays off, with one start-up Warning.

**S4 re-check:**
- CSP Manager: only `CspSavedNotification` exists, there is no delete notification. Saves only.
- UrlTracker: GO on 17. **NO-GO on 18**: still beta only, so line 18 compiles no UrlTracker guard (`#if UMBRACO_17`); a site running the beta is simply not recorded.
- Skybrud Redirects: S4's "no save notification" was wrong. The S4 spike hooked only `RedirectSavedNotification`; the package also has `RedirectAddedNotification` (raised from `AddRedirect` after the insert and scope complete) and `RedirectSavedNotification` (raised from `SaveRedirect`, the edit path). **GO** for add, edit and delete on both lines. Delete runs inside Skybrud's ambient scope (S4), which the writer joins safely.
- Workflow: `WorkflowInstanceApprovedNotification` exists, derives from `WorkflowInstanceUpdatedNotification` and is raised per step. `WorkflowInstanceCompletedNotification` (also with `WorkflowType` and `CompletedInstance`) exists in every release and is the likely one S4's one-step approval raised. Hooked: settings saved, group saved and deleted, instance created, approved, rejected and completed, and config saved on 18.

**Not verifiable from metadata (recorded, not assumed):**
- Skybrud add and edit: the backoffice user at handler time was shown only for delete in S4. Add and edit run in the same authenticated backoffice request, so the user is expected; if it is missing the row stores a null user.
- Workflow: which of Approved, Rejected and Completed fires for each approval shape; whether Group created raises Saved as well as Created (only Saved and Deleted are hooked); `WorkflowInstanceCreatedNotification` on 17 (S4 started no instance there); `WorkflowConfigSavedNotification` on 18. A final approval may store a step row and a completed row, each with its own raw type.
- Workflow's `IWorkflowInstance` declares only `Type` and `WorkflowType` in the metadata, so the instance rows carry the workflow type and nothing else (no page, no comment).

Result: no package is too brittle. Every member used is identical across all stable releases within a line; the only boundaries are Skybrud's 17.0.5 and the missing stable UrlTracker 18.

GO/NO-GO: **GO** CSP Manager (both lines), Workflow (both lines), Skybrud Redirects (both lines, floor 17.0.5), UrlTracker (17 only).
