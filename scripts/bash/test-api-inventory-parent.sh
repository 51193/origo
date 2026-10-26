#!/usr/bin/env bash
# Regression tests for the Bash counterpart of the PowerShell API-inventory
# parent/metadata-commit check. The helper must resolve a release commit's
# parent and accept only a metadata-only `chore(release)` commit as a release
# tag when the tag is a sibling of the current HEAD.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-api-parent.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

repo="$FIXTURE/repo"
mkdir -p "$repo/tools/ApiInventoryTool"
git -C "$repo" init -q
git -C "$repo" config user.name "Origo Test"
git -C "$repo" config user.email "origo-test@example.invalid"

# v0.1.0: the older fallback baseline.
printf '%s\n' '<Version>0.1.0</Version>' > "$repo/Directory.Build.props"
printf '%s\n' 'first-baseline' > "$repo/tools/ApiInventoryTool/shell-api-baseline.json"
git -C "$repo" add -A
git -C "$repo" commit -qm "release 0.1.0"
git -C "$repo" tag v0.1.0

# Main advances to a development version with a distinct baseline. The
# release tags below are detached children of this commit, so they are
# siblings of HEAD rather than ancestors.
printf '%s\n' '<Version>0.1.4-dev</Version>' > "$repo/Directory.Build.props"
printf '%s\n' 'second-baseline' > "$repo/tools/ApiInventoryTool/shell-api-baseline.json"
git -C "$repo" add -A
git -C "$repo" commit -qm "develop 0.1.4"
main_commit="$(git -C "$repo" rev-parse HEAD)"

# v0.1.3: a valid metadata-only release commit; its baseline is the one the
# helper must select.
git -C "$repo" checkout -q --detach "$main_commit"
printf '%s\n' '<Version>0.1.3</Version>' > "$repo/Directory.Build.props"
git -C "$repo" commit -qam "chore(release): sync version stamps to v0.1.3"
git -C "$repo" tag v0.1.3

# v0.1.4: metadata-looking but with an extra changed file. It must be rejected,
# and its intentionally wrong baseline must not be selected.
git -C "$repo" checkout -q --detach "$main_commit"
printf '%s\n' '<Version>0.1.4</Version>' > "$repo/Directory.Build.props"
printf '%s\n' 'wrong-baseline' > "$repo/tools/ApiInventoryTool/shell-api-baseline.json"
printf '%s\n' 'not metadata' > "$repo/README.md"
git -C "$repo" add -A
git -C "$repo" commit -qm "chore(release): sync version stamps to v0.1.4"
git -C "$repo" tag v0.1.4
git -C "$repo" checkout -q "$main_commit"

output="$(cd "$repo" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -n "$output" ]] || FAIL "sibling metadata tags did not yield a previous baseline."
actual="$(cat "$output")"
rm -f "$output"
[[ "$actual" == "second-baseline" ]] ||
    FAIL "expected the valid sibling metadata baseline; got '$actual'."

echo "Bash API-inventory parent/metadata regression: OK"
