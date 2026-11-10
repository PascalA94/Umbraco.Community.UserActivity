# Contributing

Issues and pull requests are welcome. For anything bigger than a small fix, open an issue first so we can agree the approach.

## Branches

- `main` builds both Umbraco lines (package 17.x for Umbraco 17, 18.x for Umbraco 18); `-p:UmbracoMajor=17` or `18` picks the line.
- Open pull requests against `main`. A change must build and pass the tests on both lines.

## Build and test


```bash
dotnet restore src/Umbraco.Community.UserActivity.slnx --locked-mode
dotnet build src/Umbraco.Community.UserActivity.slnx -c Release --no-restore
dotnet test src/Umbraco.Community.UserActivity.slnx -c Release --no-build
```

That builds the Umbraco 18 line. `bash build/test.sh` restores, builds and tests every line (`bash build/test.sh 17` for one); the
`-p:UmbracoMajor=17` property picks the Umbraco 17 line in the commands above.

The backoffice client is built into `wwwroot/`, which is committed. Rebuild it when you change the client; CI fails if it drifts:

```bash
cd src/Umbraco.Community.UserActivity/Client
npm ci --ignore-scripts && npm run build && npm test
```

## Try it on the demo site

`demo/` is an Umbraco site for trying the package. It runs on HTTPS only:

```bash
dotnet user-secrets set "Umbraco:CMS:Unattended:UnattendedUserPassword" "<pw>" --project demo/Umbraco.Community.UserActivity.DemoSite
dotnet dev-certs https
dotnet run --project demo/Umbraco.Community.UserActivity.DemoSite   # https://localhost:44331/umbraco
```

## Try it in your own site

Pack with a unique version each time, because NuGet's global cache keeps serving the first build of any version number:

```bash
dotnet pack src/Umbraco.Community.UserActivity -c Release -o "$HOME/local-nuget" -p:Version=18.0.0-local.$(date +%s)
dotnet nuget add source "$HOME/local-nuget" --name local   # once
dotnet add package Umbraco.Community.UserActivity --version 18.0.0-local.<timestamp>
```

For an Umbraco 17 site, add `-p:UmbracoMajor=17` to the pack command and use a `17.0.0-local.<timestamp>` version.

## Before you open a pull request

- Add or update tests for what you changed, and run them.
- Keep personal data out of logs.
- Security issues go through private reporting (the Security tab), never a public issue or PR.
