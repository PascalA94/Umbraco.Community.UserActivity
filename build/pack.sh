#!/usr/bin/env bash
# Packs one nupkg per Umbraco line into <outdir> (default ./artifacts). The lines come from LINES (space separated, for
# example LINES=19) or, when unset, from eng/lines.txt. PACK_VERSION, when set, overrides the version (the 19 tags use
# it: PACK_VERSION=19.0.0-rc.1), so the nupkg and the stamped manifest carry it.
# The client version files stay 0.0.0 in git; this stamps the line's package version into wwwroot/umbraco-package.json
# for the build and pack, then restores it, so the committed tree never changes.
# Usage: [LINES="17 18"] [PACK_VERSION=19.0.0-rc.1] bash build/pack.sh [outdir]. Run from the repository root, after the client build and the tests.
set -euo pipefail
cd "$(dirname "$0")/.."
out="${1:-./artifacts}"
proj=src/Umbraco.Community.UserActivity/Umbraco.Community.UserActivity.csproj
manifest=src/Umbraco.Community.UserActivity/wwwroot/umbraco-package.json
backup="$(mktemp)"
cp "$manifest" "$backup"
trap 'cp "$backup" "$manifest"; rm -f "$backup"' EXIT
mkdir -p "$out"
lines="${LINES:-$(cat eng/lines.txt)}"
vprop=()
[ -z "${PACK_VERSION:-}" ] || vprop=("-p:Version=$PACK_VERSION")

for line in $lines; do
  version="$(dotnet msbuild "$proj" "-p:UmbracoMajor=$line" ${vprop[@]+"${vprop[@]}"} -getProperty:Version | tr -d '[:space:]')"
  case "$version" in ''|*-) echo "invalid package version '$version' for line $line" >&2; exit 1;; esac
  node -e '
    const fs = require("fs"); const [f, v] = process.argv.slice(1);
    const j = JSON.parse(fs.readFileSync(f, "utf8")); j.version = v;
    fs.writeFileSync(f, JSON.stringify(j, null, 2) + "\n");' "$manifest" "$version"
  dotnet restore "$proj" "-p:UmbracoMajor=$line" --locked-mode -v q
  dotnet build "$proj" -c Release --no-restore "-p:UmbracoMajor=$line" ${vprop[@]+"${vprop[@]}"} -v q
  dotnet pack "$proj" -c Release --no-build "-p:UmbracoMajor=$line" ${vprop[@]+"${vprop[@]}"} -o "$out" -v q
  cp "$backup" "$manifest"
  echo "packed $line: $version"
done
