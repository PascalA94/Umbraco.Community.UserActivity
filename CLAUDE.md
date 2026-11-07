# CLAUDE.md

`Umbraco.Community.UserActivity`: A per-user Activity tab and a global Activity view under Users, reading Umbraco's audit logs and other packages' history.
Razor class library in `src/Umbraco.Community.UserActivity/` (`Core/` is line-agnostic, `Web/` is per Umbraco line).
Design `docs/design.md`, plan `docs/plan.md`, spikes `docs/spikes.md`, where work stopped: `COMMITS-TODO.md`, `HANDOVER.md`.

**Never commit, push or publish.** Pascal commits, pushes, tags and releases.

Token rules and the 14 CI rules: the portfolio README in Pascal's private workspace, sections "Token rules" and "Build approach > CI and GitHub rules". The build session prompt gives its location.

## Lines and versions

- **One branch, two lines.** `UmbracoMajor` (`17` or `18`, default `18`, set as a property or an environment variable) picks the CMS range (`[17.8.0, 18.0.0)` or `[18.3.0, 19.0.0)`), the package version (`17.x.y` or `18.x.y`), the `UMBRACO_17` / `UMBRACO_18` constants and its own `packages.<line>.lock.json`, `obj/<line>` and `bin/<line>`. `eng/lines.txt` lists the lines; `bash build/test.sh` runs the suites once per line.
- **One client bundle serves both lines.** `@umbraco-cms/backoffice` is a Vite external, the committed `wwwroot/` is built against `^17.8.0`, and CI type-checks the source against `^18.3.0`. Split into two bundles only if a line ever needs different code. If the two lines' APIs ever force different source, move to one branch per line instead.
- The NuGet ID, App_Plugins folder, aliases, and any migration plan, table or route names are permanent once released.

## Commands

```bash
bash build/test.sh       # restore, build and test every line in eng/lines.txt (SQLite); or bash build/test.sh 17
dotnet restore src/Umbraco.Community.UserActivity.slnx -p:UmbracoMajor=17 --locked-mode   # one line, locked
# SQL Server integration run: set Tests__Database__DatabaseType=SqlServer and Tests__Database__SQLServerMasterConnectionString
# Client (output is committed in wwwroot/; CI fails on drift)
cd src/Umbraco.Community.UserActivity/Client && npm ci --ignore-scripts && npm run build && npm test
bash build/check-xss.sh   # from the repo root
# Demo site: HTTPS only (OpenIddict refuses HTTP)
dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<pw>" --project demo/Umbraco.Community.UserActivity.DemoSite
dotnet dev-certs https && dotnet run --project demo/Umbraco.Community.UserActivity.DemoSite -p:UmbracoMajor=17   # https://localhost:44331/umbraco; use a separate database per line
# e2e (demo running; env vars as in tests/e2e/.env.example)
cd tests/e2e && npm ci --ignore-scripts && npx playwright install chromium && npx playwright test --reporter=line
bash build/check-vulnerable.sh
```

## Releasing

- Bump `PackageMinor`, `PackagePatch` and `PackageVersionSuffix` in `Directory.Build.props` (both lines move together), and rebuild the client if its source changed.
- `bash build/check-vulnerable.sh` and `bash build/check-release-version.sh release/<minor>.<patch>[-suffix]` both exit 0.
- Run the **Release checks** workflow by hand on the release branch (SQL Server integration, and e2e if present). Never on PRs. Re-run after a new Umbraco minor.
- Squash-merge, tag `release/<minor>.<patch>[-suffix]` on the merge commit; `publish.yml` (tag `release/*`, from `main` only) restores, builds, tests and audits both lines, packs them with `bash build/pack.sh` (one nupkg per line) and publishes with Trusted Publishing. Then `gh release create` per line, and mark the newest Umbraco line's release as Latest.
- Stable from the first real public release unless the API is still moving (`.github/REPO-SETUP.md` step 10); after that, no breaking changes within a major.
- "Done" means green on GitHub. The first CI run is its own step with Pascal.

## Keep

- Floors never below 17.8.0 / 18.3.0. The repo is public: no reviews, plans of record or local paths in it.
- `docs/` holds only `design.md`, `plan.md`, `spikes.md`. Fake test secrets carry `// gitleaks:allow` from their first commit.
