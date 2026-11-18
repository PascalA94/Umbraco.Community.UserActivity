#!/usr/bin/env bash
# Release gate. A legacy tag packs lines 17 and 18: release/<PackageMinor>.<PackagePatch>[-suffix] (suffix optional),
# for example release/0.0-alpha.1 gives 17.0.0-alpha.1 and 18.0.0-alpha.1, and release/1.2 gives 17.1.2 and 18.1.2.
# A 19 tag, release/19.<minor>.<patch>[-suffix], packs line 19 only and carries its own version (passed to pack as
# -p:Version=), so it is only checked for shape and not compared with Directory.Build.props.
# Fails if Directory.Build.props disagrees with a legacy tag, and (when the package has a client) if a client version file is
# not the 0.0.0 placeholder: the pack step stamps the real version. Run from the repository root.
# Usage: build/check-release-version.sh release/0.0-alpha.1   or   release/19.0.0-rc.1
set -euo pipefail

tag="${1:?usage: check-release-version.sh <release/minor.patch[-suffix] | release/19.minor.patch[-suffix]>}"
want="${tag#release/}"
status=0

prop() { sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" Directory.Build.props | head -1; }
if [[ "$tag" == release/19.* ]]; then
  if ! [[ "$want" =~ ^19\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z][0-9A-Za-z.-]*)?$ ]]; then
    echo "check-release-version: '$want' is not 19.<minor>.<patch>[-suffix]" >&2
    status=1
  fi
else
  minor="$(prop PackageMinor)"; patch="$(prop PackagePatch)"; suffix="$(prop PackageVersionSuffix)"
  have="$minor.$patch${suffix:+-$suffix}"
  if [ "$have" != "$want" ]; then
    echo "check-release-version: Directory.Build.props has '$have', tag wants '$want'" >&2
    status=1
  fi
fi

pkg="src/Umbraco.Community.UserActivity"
for f in "$pkg/Client/package.json" "$pkg/Client/public/umbraco-package.json" "$pkg/wwwroot/umbraco-package.json" "$pkg/Client/package-lock.json"; do
  got="$(node -p "require('./$f').version")"
  if [ "$got" != "0.0.0" ]; then
    echo "check-release-version: $f has '$got', expected the 0.0.0 placeholder" >&2
    status=1
  fi
done

exit $status
