#!/usr/bin/env bash
# Regression tests for the Bash counterpart of the PowerShell API-inventory
# parent/metadata-commit check. The helper must resolve a release commit's
# parent and accept only a metadata-only `chore(release)` commit as a release
# tag when the tag is not an ancestor of the current HEAD.
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

new_repo() {
    local dir="$1"
    mkdir -p "$dir/tools/ApiInventoryTool"
    git -C "$dir" init -q
    git -C "$dir" config user.name "Origo Test"
    git -C "$dir" config user.email "origo-test@example.invalid"
    printf '%s\n' '<Version>0.1.0</Version>' > "$dir/Directory.Build.props"
    printf '%s\n' 'first-baseline' > "$dir/tools/ApiInventoryTool/shell-api-baseline.json"
    git -C "$dir" add -A
    git -C "$dir" commit -qm "release 0.1.0"
    git -C "$dir" tag v0.1.0
}

# A metadata-only release commit is a child of the previous main commit, not an
# ancestor of HEAD's branch when the tag is a sibling. The helper must select
# the previous release baseline through the metadata tag.
repo_metadata="$FIXTURE/metadata-only"
new_repo "$repo_metadata"
printf '%s\n' '<Version>0.1.1</Version>' > "$repo_metadata/Directory.Build.props"
git -C "$repo_metadata" commit -qam "chore(release): sync version stamps to v0.1.1"
git -C "$repo_metadata" tag v0.1.1

output="$(cd "$repo_metadata" && ORIGO_CURRENT_RELEASE_TAG=v0.1.1 bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -n "$output" ]] || FAIL "metadata-only tag did not yield a previous baseline."
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "metadata-only tag selected the wrong baseline."
rm -f "$output"

# A metadata-looking commit with another changed file must not be treated as a
# release commit. The older tagged baseline must still be selected.
repo_extra="$FIXTURE/extra-file"
new_repo "$repo_extra"
printf '%s\n' '<Version>0.2.0</Version>' > "$repo_extra/Directory.Build.props"
printf '%s\n' 'not metadata' > "$repo_extra/README.md"
git -C "$repo_extra" add -A
git -C "$repo_extra" commit -qm "chore(release): sync version stamps to v0.2.0"
git -C "$repo_extra" tag v0.2.0

output="$(cd "$repo_extra" && ORIGO_CURRENT_RELEASE_TAG=v0.2.0 bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -n "$output" ]] || FAIL "extra-file metadata tag did not yield the older baseline."
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "a metadata-looking commit with an extra file was accepted."
rm -f "$output"

echo "Bash API-inventory parent/metadata regression: OK"
