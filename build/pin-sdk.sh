#!/usr/bin/env bash
# CI only. Hosted images ship a newer SDK than a line needs (ubuntu-24.04 has 11.0.100), and with no global.json
# `dotnet` picks the newest installed SDK, so the 17 and 18 legs would silently build on .NET 11. This writes an
# untracked global.json in the current directory that pins the newest installed SDK of the given major, then fails
# unless that SDK is the one in use. Run it from the repository root, after setup-dotnet. Never commit the global.json.
# Usage: build/pin-sdk.sh <sdk major>, for example build/pin-sdk.sh 10.
set -euo pipefail
major="${1:?usage: pin-sdk.sh <sdk major>}"
case "$major" in 10|11) ;; *) echo "pin-sdk: unsupported SDK major '$major'" >&2; exit 1;; esac

version="$(dotnet --list-sdks | awk '{print $1}' | grep -E "^${major}\.0\.[0-9]+$" | sort -V | tail -n 1 || true)"
if [ -z "$version" ]; then
  echo "pin-sdk: no .NET $major SDK installed" >&2
  exit 1
fi
printf '{\n  "sdk": {\n    "version": "%s",\n    "rollForward": "disable"\n  }\n}\n' "$version" > global.json

got="$(dotnet --version)"
case "$got" in
  "$major".*) echo "pin-sdk: using SDK $got" ;;
  *) echo "pin-sdk: expected a .NET $major SDK, got $got" >&2; exit 1 ;;
esac
