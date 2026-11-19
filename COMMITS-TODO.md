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

## M2 checkpoints

- [x] Task 15: `feat: shared activity components and URL filter state`
- [x] Task 16: `feat: global Activity view under the Users section`
- [x] Task 17: `feat: per-user Activity tab`
- [x] Task 18: `feat: event detail sidebar, empty, loading and partial states`

- [x] Task 19: M2 stop. qa-reviewer: pass with changes; every Important fixed under tests (HANDOVER.md has the list and what was left)

## M3 checkpoints

- [x] Task 20: `feat: Automate source`
- [x] Task 21: `feat: Forms source`
- [x] Task 22: `feat: runtime adapter pattern and Umbraco.AI source`
- [x] Task 23: `feat: uSync source`
- [x] Task 24: `feat: Archived Users source` (Forms Audit Trail out of 0.1, Pascal 2026-11-07)

- [x] Task 25: M3 stop. Sample source and README "Add your own source"; qa-reviewer: pass with changes; both Importants and two re-graded Minors fixed under tests (HANDOVER.md has the list)

## M4 checkpoints

- [x] Task 26: `docs: privacy, sources and limits`
- [x] Task 27: `docs: presentation, Marketplace and publish workflow` (Step 6, reserve the NuGet ID and add Trusted Publishing, is Pascal's)
- [x] Task 28 Step 1: pre-push checks clean (push and PR #3 done; CI already green on PRs #1 and #2; release-checks still to run)
- [x] Task 29: `fix: final review findings` (qa-reviewer pass with changes, security-analyst yes with conditions; no Critical)
- [x] Task 30 Steps 1-6: `docs: acceptance results for 0.1` (both lines; Step 7, the release, is Pascal's)

## M5 checkpoints

- [x] Task 31: `feat: recorder table, writer and retention job`
- [x] Task 32: `feat: recorder handlers for CSP Manager and redirects` (CSP Manager, Skybrud Redirects, UrlTracker on 17)
- [x] Task 33: `feat: recorder handlers for Workflow`
- [x] Task 34: `docs: recorder and retention` (README "Recorder", "Set a retention limit", erasure bullet)
- [x] Task 35: M5 stop. qa-reviewer: pass with changes; security-analyst: yes with conditions, no Critical. Importants and the two security conditions fixed (HANDOVER.md has the list)

## Paused here (2026-11-10, after M5)

Milestone: **M5 done** (Tasks 31-35): the recorder is built and reviewed on both lines. Unit 307 / 303, integration 136, adapter 72 / 62 (17 / 18); Vitest 54; `check-xss.sh` clean.
Reviews: qa-reviewer pass with changes, security-analyst yes with conditions, no Critical. Fixed under tests, plus README and design. Your choice recorded: the recorder keeps names after a user is deleted, and the README gives the erasure SQL.
Next task: **Task 36** (release the recorder minor). Step 1 is the demo-site exercise (an agent can do it). Step 2 is yours: bump `PackageMinor` to 1, run release checks, tag `release/1.0`.
Open for Task 36 Step 1: the Skybrud add and edit user at handler time, which Workflow notifications fire per approval shape, a SQL Server run of the recorder tests, and whether UrlTracker or Skybrud raise notifications outside a backoffice user.

## Earlier: Paused here (2026-11-10, after M4)

Milestone: **M4 done** (Tasks 26-30): 0.1 is built, reviewed and exercised on both lines. Unit 247, integration 122, adapter 32 on 17 and on 18; Vitest 46; Playwright 7 passed per line.
Reviews: qa-reviewer pass with changes, security-analyst yes with conditions, no Critical. QA-F1, QA-F2, QA-F3 (profile route, Pascal's choice), QA-F4, SEC-F1 and SEC-F5 fixed; the Minors deferred are in HANDOVER.md.
**0.1 released 2026-11-10:** 17.0.0 and 18.0.0 on nuget.org from tag `release/0.0` (`28dec72`), GitHub release marked Latest. M4 follow-ups merged as PRs #3 to #5.
Next task: **Task 31** (M5, the recorder), **on Sonnet** (`--model sonnet`). 0.1 is released, so M5 can start.

## Earlier milestone commit (M4, already done)

```bash
# from the repository root
git status --short          # check: only README.md, CLAUDE.md, COMMITS-TODO.md, HANDOVER.md, umbraco-marketplace.json, docs/, assets/screenshots/, .github/, src/ changed or added (two old screenshots deleted); no secrets, .env, appsettings.*.local, bin/obj, spikes/, test-results or review docs
git add -A
git commit -m "feat: 0.1 release readiness with privacy docs, profile route and final review fixes"
git log -1 --oneline
```

## Milestone commit

```bash
# from the repository root
git status --short          # check: only README.md, COMMITS-TODO.md, HANDOVER.md, Directory.Build.props, docs/spikes.md, docs/design.md and src/ changed or added (new Recorder/ folders, regenerated wwwroot chunks); no secrets, .env, appsettings.*.local, bin/obj, spikes/, test-results or review docs
git add -A
git commit -m "feat: recorder for CSP Manager, Workflow and redirects"
git log -1 --oneline
```

## Paused here (2026-11-11, recorder minor released)

SQL Server run passed on both lines (integration 135 passed, 1 skipped). The demo exercise with third-party packages was not done (Pascal chose the partial option). Task 36 Step 2 done: PR #9 merged, release-checks green, tag `release/1.0`, publish succeeded, GitHub release created. Nothing is left in the plan. Optional next: a patch milestone to exercise the recorder against the real third-party packages in the demo (see HANDOVER.md).

## Milestone commit (Task 36 notes)

```bash
git status --short          # check: only HANDOVER.md and COMMITS-TODO.md changed
git add -A
git commit -m "docs: record recorder acceptance results"
git log -1 --oneline
```


## Paused here (2026-11-11, 1.1 patch released)

The real-package exercise and its patch are done and released: PR #11 merged (`28bf54c`), Release checks green, tag `release/1.1`, publish succeeded and the GitHub release "User Activity 18.1.1 / 17.1.1" is marked Latest. Nothing is left in the plan. Still to do by hand: confirm 17.1.1 and 18.1.1 on nuget.org. The local real-package helpers are deleted; HANDOVER.md "Repeating the exercise" says how to rebuild them.
Optional next: Workflow cancel and resubmit recording, an item name on Workflow instance rows, a live check of the config-delete handlers (see HANDOVER.md "Not verified / gaps left for you").

## Earlier milestone commit (real-package exercise, already done)

```bash
git status --short          # check: only README.md, HANDOVER.md, COMMITS-TODO.md, Directory.Build.props, umbraco-marketplace.json, assets/screenshots/screenshot-05-recorder.png and src/ changed or added (Workflow handler, ActorRef, en.ts, tests, regenerated wwwroot chunks); no demo/RecorderLocal, tests/e2e/local, secrets, bin/obj
git add -A
git commit -m "fix: record Workflow group creation and config changes, label recorder item types, link uSync users"
git log -1 --oneline
```

## Milestone commit (1.1 release notes)

```bash
git status --short          # check: only HANDOVER.md and COMMITS-TODO.md changed
git add -A
git commit -m "docs: record the 1.1 patch release"
git log -1 --oneline
```

## Milestone commit (handover recipe)

```bash
git status --short          # check: only HANDOVER.md and COMMITS-TODO.md changed
git add -A
git commit -m "docs: add the recipe for repeating the real-package exercise"
git log -1 --oneline
```

## Umbraco 19 line (branch feat/umbraco-19)

Tasks (guide `useractivity.md`):

- [x] Task 1: branch and baseline. 17: 310 / 75 / 136, 18: 306 / 64 / 136, as expected. **picomatch ignore entry NOT added** (see below).
- [x] Task 2: build switch, version, floors, description, no Marketplace tag, lock files out of the nupkg
- [x] Task 3: TFM via `$(LineTfm)` and HintPaths via `$(TargetFramework)`
- [x] Task 4: CSP Manager and Skybrud Redirects fenced out of 19 (`#if !UMBRACO_19`)
- [x] Task 5: ScaffoldTests 19 arm, CsvHelper 34 in AdapterTests, `Split('-')[0]`, `AddAsync`, demo `Program.cs`
- [x] Task 6: 19 cookie-only login in `ApiTestBase`
- [x] Task 7: `FetchMetadataFilter` on `UserActivityControllerBase`, 17 unit tests and 8 integration tests (security-analyst review done)
- [x] Task 8: `eng/lines.txt`, six `packages.19.lock.json`, locked restore OK, no ICU4C in the demo lock, `check-vulnerable.sh` clean on 19
- [x] Task 9: client 59/59 on `@umbraco-cms/backoffice@19.0.0-rc1` (temp copy), no `wwwroot` drift, `check-xss.sh` 0
- [x] Task 10: `build.yml`
- [x] Task 11: `release-checks.yml`
- [x] Task 12: `publish.yml`, `release-lines.sh`, `check-release-version.sh`, `pack.sh`
- [x] Task 13: README, CLAUDE.md, CONTRIBUTING, REPO-SETUP (no CHANGELOG exists)
- [x] Task 14: final runs (below)

Results (this session):

- 19: 0 warnings; unit 313, adapter 47, integration 145 (guide expected 146, see drift)
- 18: unit 306, adapter 64, integration 136. 17: unit 310, adapter 75, integration 136. Unchanged.
- `dotnet msbuild -getProperty:Version`: 17.1.1, 18.1.1, 19.1.1; 19.0.0-rc.1 with `-p:Version=`. `-p:UmbracoMajor=20` fails with "must be 17, 18 or 19".
- 19 pack: `19.0.0-rc.1` nupkg, 0 lock files, `staticwebassets/index.js` present, tags have no `umbraco-marketplace`, RC description present, no `wwwroot` change left behind.
- Tag dry runs: `release/1.2` gives `17 18`; `release/19.0.0-rc.1` and `release/19.0.0` give `19`; `release/19/19.0.0-rc.1`, `release/foo`, `release/19.0`, `release/19.01.0` exit 1. `check-release-version.sh`: `release/1.1`, `release/19.0.0-rc.1`, `release/19.0.0` exit 0; `release/19.x`, `release/19.01.0` exit 1.
- YAML of the three workflows parses (ruby psych; no python yaml here).

Drift from the guide (repo wins):

- Task 3 says `$(UaTfm)` but Task 2 defines `LineTfm`; used `$(LineTfm)`.
- Integration total is 145, not 146: the empty `Sec-Fetch-Site` case was dropped, as the guide allows. The test host delivers an empty header as absent (200). The policy still refuses `""` (unit test), and the README says an empty value may be treated as absent.
- `BackOfficeHostCrossSiteTests` works (setting `BackOfficeHost` does not break login), so it stays.
- The em dash in `demo/.../DemoSite.csproj` is pre-existing, not in the diff.

NOT DONE, needs Pascal:

- **picomatch ignore** (`GHSA-fc5q-39xc-vjpf`, Task 1): adding a vulnerability ignore entry to `osv-scanner.toml` was blocked by the permission layer, so it is not in the tree. Add it by hand (text in the guide, Task 1) or tell me to retry. The osv job will probably go red without it.
- 17/18 pack check was blocked too. Run it yourself: `env LINES="17 18" bash build/pack.sh /tmp/ua-pack` (expect 17.1.1 and 18.1.1, each with 0 lock files). In zsh `LINES` is a special variable, so keep the `env`.

Review outcomes:

- security-analyst (opus, fresh): no Critical or High. Medium F1: a navigation with `Sec-Fetch-Site: none` (a link opened from an email or document) passes the filter and can write a forged `viewed` or `exported` row, if the browser sends the Strict cookie. Medium F2: same-origin embeds (an `<img src>` pointing at the API from any same-origin HTML sink) pass. Both are outside the guide's rule. Suggested fix: also require `Sec-Fetch-Mode` `cors` or `same-origin` and `Sec-Fetch-Dest: empty` when the headers are present (the real client only uses `fetch`), and optionally a custom request header. **Pascal decides** whether to widen the filter before the RC. Low: absent header allowed on old Safari and Firefox (accepted residual), no integration test for the detail endpoint, a one-line README warning about a leftover dev `BackOfficeHost`, an architecture test that every controller derives from the base.
- qa-reviewer (sonnet, fresh): no Critical or High; 3 Medium, 8 Low. Fixed: tag shape now rejects leading zeros (`19.01.0`) and `release-lines.sh` rejects any non-three-part `release/19.*`; filter comment corrected about empty headers. Open: F3 `setup-dotnet` `11.x` may need `dotnet-quality` (first CI run decides); F5 `--skip-duplicate` also applies to 17/18 pushes; F6 and F7 test gaps (positive control for global events, unauthenticated 401 on 19); F8 the AI resolver on 19 takes the newest prerelease forever (revisit at stable); F9 the 19 type-check step is pinned to rc1 (move at stable); F10 `failure()` skips the summary on a cancelled e2e; F11 `pack.sh` passes `-p:Version` to build and pack but not restore (the 19 pack ran fine).

## Paused here (2026-11-18)

Work stopped after Task 14. Nothing is committed. The 19 line is green locally; CI and everything below has not run.

### Not verified (Pascal runs these)

- SQL Server integration on 19: `gh workflow run release-checks.yml --ref feat/umbraco-19`, then `gh run watch`. Required leg: `integration-sqlserver (19)`. Local: `Tests__Database__DatabaseType=SqlServer Tests__Database__SQLServerMasterConnectionString=<your string> PATH=/usr/local/share/dotnet:$PATH dotnet test src/Umbraco.Community.UserActivity.IntegrationTests -c Release -p:UmbracoMajor=19`
- e2e on 19 (advisory): the `e2e (19 floor)` and `e2e (19 latest)` legs of the same run. Local: `PATH=/usr/local/share/dotnet:$PATH dotnet run --project demo/Umbraco.Community.UserActivity.DemoSite -p:UmbracoMajor=19` (HTTPS), then `cd tests/e2e && npm ci --ignore-scripts && npx playwright install chromium && npx playwright test --reporter=line`. Check the e2e console guard for the pre-sign-in 401 before reading a 19 failure as a product bug.
- Live-site smoke of the client bundle on a real 19 site (tab, global view, export): manual.
- Live provider behaviour on 19 (Forms and Automate row shapes): unchanged per research, no code change.
- First CI run: `setup-dotnet` `11.x` (fallback `dotnet-quality: ga`), `19.*-*` in the demo restore, osv-scanner on the new lock files. Publish path: a real `release/19.0.0-rc.1` tag run.
- 17/18 pack check, see above.

### Pascal's release steps

1. Decide the two security Mediums (filter widening) and add the picomatch ignore, then review the diff.
2. Run the Milestone commit block below.
3. `git push -u origin feat/umbraco-19`, then `gh pr create --base main --title "feat: add the Umbraco 19 line" --body-file <your notes>` (what changed, three lines, the security gate, the not-verified list, the RC label; no attribution line).
4. After the first PR run, add `build (19 floor)`, `build (19 latest)` and `deps (19)` to the `main` ruleset's required checks.
5. No tag ruleset, environment or Trusted Publishing change is needed (`release/19.0.0-rc.1` matches `release/*`).
6. Run Release checks on the branch. Needs green: `integration-sqlserver (17)`, `(18)`, `(19)`, e2e 17 and 18. A 19 e2e failure goes in the release notes.
7. Squash-merge. `git tag release/19.0.0-rc.1 <merge-sha> && git push origin release/19.0.0-rc.1`, approve the `nuget` environment run.
8. `gh release create release/19.0.0-rc.1 --prerelease --title "19.0.0-rc.1" --notes "Built against Umbraco 19.0.0-rc1, not yet verified on 19 stable. <19 e2e result if it failed>"`. Leave 18 as Latest.
9. Check nuget.org lists `19.0.0-rc.1` (no Marketplace tag). The next legacy 17/18 release drops the stray lock files from those nupkgs.

### Release checks result (2026-11-19, run 44523985103 on be170cc, after one re-run of the apt flakes)

- Green: `integration-sqlserver` 17, 18 and 19; e2e 17 and 18 (floor and latest); e2e 19 floor.
- Red, advisory: e2e 19 latest. `activity.spec.ts:194` ("your activity opens from the profile") times out opening the profile panel; 7 of 8 specs pass. Same result on both runs. Cause unconfirmed (probably a profile panel change in a later 19 prerelease; the same test passes on the 19 floor).
- Pre-sign-in 401 from core on `user/current/configuration` on 19: allowed in `tests/e2e/support/console-guard.ts` (be170cc).
- Release notes line: "e2e on the latest 19 prerelease fails opening the profile panel; checked by hand on a live 19 site." Do the hand check first: sign in, click the avatar, open Your activity.

## Milestone commit (Umbraco 19 line)

```bash
git status --short          # check: no secrets, local settings, review docs, obj/, bin/, TEMP/ or appsettings-schema*.json; osv-scanner.toml appears only if you added the picomatch entry
git add -A
git commit -m "feat: add the Umbraco 19 line with a cross-site gate on 19"
git log -1 --oneline
```
