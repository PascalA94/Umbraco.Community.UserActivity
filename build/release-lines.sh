#!/usr/bin/env bash
# Prints the Umbraco lines a release tag packs, or exits 1 for any other tag shape.
#   release/<minor>.<patch>[-suffix]      packs 17 and 18 (every line in eng/lines.txt except 19)
#   release/19.<minor>.<patch>[-suffix]   packs 19 only, e.g. release/19.0.0-rc.1
# The shapes never clash: a legacy tag has two numeric parts, a 19 tag has three and starts with 19.
# Usage: bash build/release-lines.sh release/19.0.0-rc.1. Run from the repository root.
set -euo pipefail
cd "$(dirname "$0")/.."

tag="${1:?usage: release-lines.sh <tag>}"
sfx='(-[0-9A-Za-z][0-9A-Za-z.-]*)?'
n='(0|[1-9][0-9]*)'   # no leading zeros: NuGet would normalise 19.01.0 to 19.1.0
if   [[ "$tag" =~ ^release/19\.${n}\.${n}${sfx}$ ]]; then echo "19"
elif [[ "$tag" == release/19.* ]]; then echo "release-lines: '$tag' is not release/19.<minor>.<patch>[-suffix]" >&2; exit 1
elif [[ "$tag" =~ ^release/${n}\.${n}${sfx}$ ]];       then grep -E '^[0-9]+$' eng/lines.txt | grep -vx 19 | tr '\n' ' ' | sed 's/ $//'
else echo "release-lines: '$tag' is not a release tag" >&2; exit 1; fi
