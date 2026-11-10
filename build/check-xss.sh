#!/usr/bin/env bash
# XSS guard for the backoffice client. Fails on any HTML sink or banned API in the client source, and on any
# localize.term call whose first argument is not a string literal. Run from the repository root.
# Rationale: package UI renders user and Umbraco data inside the backoffice, where a script sink runs with the
# signed-in user's authority. Use Lit text bindings, never raw HTML.
set -euo pipefail

# XSS_SRC overrides the folder so the guard itself can be tested against fixtures.
SRC="${XSS_SRC:-src/Umbraco.Community.UserActivity/Client/src}"
status=0

if grep -rnE 'unsafeHTML|unsafeSVG|unsafeStatic|innerHTML|outerHTML|insertAdjacentHTML|DOMParser|createContextualFragment|document\.write|setHTMLUnsafe|srcdoc|\beval\(|new Function\(|UMB_CONFIRM_MODAL|htmlString|htmlMessage' "$SRC"; then
  echo "check-xss: banned HTML sink or API found" >&2
  status=1
fi

# Whole-file match, so a key on the next line or a literal joined with + is caught too. A `.term` that is not called
# at all (a cast, an alias or a bound reference) is caught as well. components/localize.ts is the one audited file
# that may pass a dynamic key: it only resolves keys starting with userActivity_.
if find "$SRC" -type f \( -name '*.ts' -o -name '*.js' \) ! -path '*/components/localize.ts' -print0 \
  | xargs -0 perl -0777 -ne 'while (/\.term(?!\()|\.term\(\s*(?:[^\x27"\s]|([\x27"])[^\x27"]*\1\s*\+)/g) { print "$ARGV: $&\n"; $f = 1 } END { exit($f ? 0 : 1) }'; then
  echo "check-xss: localize.term must take a string literal key" >&2
  status=1
fi

exit $status
