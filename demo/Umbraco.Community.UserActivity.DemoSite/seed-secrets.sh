#!/usr/bin/env bash
# Demo only. Stores random passwords for the unattended admin and the three seeded users, plus an imaging HMAC key, in
# this project's user secrets. Nothing is printed. Keys that are already set are kept: the seed never changes a password
# once a user exists, so rotating here would leave the stored passwords out of step. To start again, delete the demo
# databases (umbraco/Data/Umbraco17.sqlite.db, Umbraco18.sqlite.db) and run `dotnet user-secrets clear` first.
set -euo pipefail
cd "$(dirname "$0")"
existing="$(dotnet user-secrets list 2>/dev/null | sed 's/ = .*//')"
set_once() {
  if ! grep -qxF "$1" <<<"$existing"; then dotnet user-secrets set "$1" "$2" >/dev/null; fi
}
set_once "Umbraco:CMS:Unattended:UnattendedUserPassword" "$(openssl rand -base64 24)A1!"
# Umbraco writes a random HMAC key into appsettings.json on first install unless one is configured; keep it out of git.
set_once "Umbraco:CMS:Imaging:HMACSecretKey" "$(openssl rand -hex 32)"
for u in Sam Wren Ed; do set_once "DemoSeed:Passwords:$u" "$(openssl rand -base64 24)A1!"; done
echo "Demo secrets present in user secrets (values not shown)."
