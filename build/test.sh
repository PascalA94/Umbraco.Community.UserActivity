#!/usr/bin/env bash
# Runs the unit and integration suites once per Umbraco line in eng/lines.txt (SQLite).
# Usage: bash build/test.sh [line ...]   (default: every line). Quiet output; failures in full.
set -euo pipefail
cd "$(dirname "$0")/.."
lines=("$@")
if [ ${#lines[@]} -eq 0 ]; then while read -r l; do [ -n "$l" ] && lines+=("$l"); done < eng/lines.txt; fi
for line in "${lines[@]}"; do
  echo "== Umbraco $line"
  dotnet restore src/Umbraco.Community.UserActivity.slnx "-p:UmbracoMajor=$line" -v q
  dotnet build src/Umbraco.Community.UserActivity.slnx -c Release --no-restore "-p:UmbracoMajor=$line" -v q
  dotnet test src/Umbraco.Community.UserActivity.slnx -c Release --no-build "-p:UmbracoMajor=$line" --logger "console;verbosity=minimal"
done
