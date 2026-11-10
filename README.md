# User Activity

A per-user Activity tab and a global Activity view under Users, reading Umbraco's audit logs and other packages' history.

[![Build](https://github.com/PascalA94/Umbraco.Community.UserActivity/actions/workflows/build.yml/badge.svg)](https://github.com/PascalA94/Umbraco.Community.UserActivity/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Umbraco.Community.UserActivity)](https://www.nuget.org/packages/Umbraco.Community.UserActivity)
[![NuGet downloads](https://img.shields.io/nuget/dt/Umbraco.Community.UserActivity)](https://www.nuget.org/packages/Umbraco.Community.UserActivity)
[![Licence: MIT](https://img.shields.io/badge/licence-MIT-blue)](https://github.com/PascalA94/Umbraco.Community.UserActivity/blob/main/LICENSE)

A community package, not made or supported by Umbraco HQ. Requires Umbraco 17 (17.8.0 or later) or Umbraco 18 (18.3.0 or later), on .NET 10.

<!-- TODO before the first release: add the best screenshot from assets/screenshots/ with an absolute URL, so it also shows on NuGet:
![What it looks like](https://raw.githubusercontent.com/PascalA94/Umbraco.Community.UserActivity/main/assets/screenshots/screenshot-01-<what>.png) -->

## Compatibility

| Package version | Umbraco |
|---|---|
| 17.x | 17 (17.8.0 or later) |
| 18.x | 18 (18.3.0 or later) |

## Install

```bash
dotnet add package Umbraco.Community.UserActivity
```

## Use

TODO: what the editor sees, and the settings (`appsettings.json` section, defaults).

## Add your own source

Another package can put its own history on the Activity tab and in the global view. The contract is in this package (namespace `Umbraco.Community.UserActivity.Sources`), so a source only shows up where User Activity is installed. It's small, public and versioned with the package: no breaking changes within a major (17.x, 18.x).

1. Implement `IUserActivitySource`:
   - `Alias`: permanent, `^[a-z][a-zA-Z0-9]{1,31}$`, unique across sources. `core`, `automate`, `forms`, `ai`, `usync`, `archivedUsers`, `formsAuditTrail` and `recorder` are taken.
   - `DisplayNameKey`: a localisation key for the source's name. Without a translation the alias is shown.
   - `IsAvailable`: `false` hides the source everywhere (for example, your feature is switched off).
   - `Info`: the entity types and categories you can return, whether you can count (`CanCount`) and search (`CanSearch`), and how long your store keeps rows (`RetentionHint`). A source that can't search is skipped while a search is active, rather than showing rows that don't match.
   - `QueryAsync`: the top `Limit` events in the date window, newest first (oldest first when `Ascending`), sorted with `ActivityEventOrder`. Apply the user, category, entity type and search filters yourself. Never throw for bad stored data: skip the row.
   - `GetAsync`: one event by your opaque id, with `Details`.
2. Register it from a composer: `builder.UserActivitySources().Append<MySource>();`
3. Your source gets the setting `UserActivity:Sources:<Alias>:Enabled` (on by default) for free.

`samples/SampleSource` is a working source over an imaginary newsletter log, in about 50 lines. Register your own `INewsletterLog` (or whatever your store is) before you try it on a site.

### The event shape

`ActivityEvent` holds the source alias, opaque id (unique within your source), time (UTC), actor (`ActivityActor`: a kind, the user's key or id, the name you stored, and `Via` for an account that acted for someone), category, your raw type, entity, a plain-text summary of up to 500 characters, an IP address (leave it null; it is shown only when `ShowIpAddresses` is on), and `Details` (up to 50 entries of 2,000 characters, shown to readers only).
- Every string is untrusted and shown as plain text, never as HTML.
- A user's tab shows the events where they are the actor or the `Via`. An actor with no user key or id is matched by its stored name against the username (that's how uSync's rows work), so only put a username there, never a display name.
- Never put an IP address in `Details`.

### Claims

If your package writes rows to Umbraco's `umbracoAudit` or `umbracoLog` tables and your source reads them, also implement `IClaimsCoreAuditRows`, so the built-in `core` source skips those rows and each row shows once. You can claim `umbracoAudit` event type prefixes, whole `umbracoLog` entity types, or `umbracoLog` rows by entity type, header and comment prefix. Two sources can't claim the same rows (start-up stops with an error naming both). No source can claim core's own `umbracoAudit` user, user group, member and sign-in rows, or every `umbracoLog` row for users, user groups or members; claim a header and comment prefix instead, as the Archived Users source does.

### What is versioned

- Versioned (stable within a major): the interfaces, `ActivityEvent` and its fields, the category strings, the source aliases and the `Sources:<Alias>:Enabled` settings.
- Not versioned: the `Details` dictionary and `RawType` values. They are free-form, for people to read, and can change in a minor.

## Develop

See `CLAUDE.md` for the commands. In short: `bash build/test.sh` restores, builds and tests both Umbraco lines; run the demo site under `demo/`.

## Licence

MIT. See `LICENSE`. Security issues: see `SECURITY.md`.
