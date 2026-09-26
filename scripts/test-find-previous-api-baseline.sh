#!/usr/bin/env bash
# Regression tests for scripts/find-previous-api-baseline.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-previous-baseline.XXXXXX")"
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
    mkdir -p "$dir"
    git -C "$dir" init -q
    git -C "$dir" config user.name "Origo Test"
    git -C "$dir" config user.email "origo-test@example.invalid"
}

commit_all() {
    local dir="$1"
    local message="$2"
    git -C "$dir" add -A
    git -C "$dir" commit -qm "$message"
}

# Case 1: previous formal tag contains a baseline.
repo_one="$FIXTURE/with-baseline"
new_repo "$repo_one"
mkdir -p "$repo_one/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_one/tools/ApiInventoryTool/shell-api-baseline.json"
commit_all "$repo_one" "release 0.1.0"
git -C "$repo_one" tag v0.1.0
printf 'second-baseline\n' > "$repo_one/tools/ApiInventoryTool/shell-api-baseline.json"
commit_all "$repo_one" "release 0.2.0"
git -C "$repo_one" tag v0.2.0

output="$(cd "$repo_one" && bash "$ROOT/scripts/find-previous-api-baseline.sh")"
[[ -n "$output" ]] || FAIL "expected a previous baseline path when an earlier tagged baseline exists."
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "extracted baseline does not match the previous formal tag."
rm -f "$output"

# Case 2: no previous formal tag contains a baseline.
repo_two="$FIXTURE/without-baseline"
new_repo "$repo_two"
printf 'release notes\n' > "$repo_two/README.md"
commit_all "$repo_two" "release 0.1.0"
git -C "$repo_two" tag v0.1.0
printf 'more release notes\n' > "$repo_two/README.md"
commit_all "$repo_two" "release 0.2.0"
git -C "$repo_two" tag v0.2.0

output="$(cd "$repo_two" && bash "$ROOT/scripts/find-previous-api-baseline.sh")"
[[ -z "$output" ]] || FAIL "expected no output when no previous formal tag contains a baseline."

# Case 3: a newer formal tag on an unrelated branch is not eligible.
repo_three="$FIXTURE/unrelated-tag"
new_repo "$repo_three"
printf 'baseline\n' > "$repo_three/baseline.txt"
commit_all "$repo_three" "release 0.1.0"
git -C "$repo_three" tag v0.1.0
git -C "$repo_three" checkout -qb side
printf 'side\n' > "$repo_three/side.txt"
commit_all "$repo_three" "side branch"
git -C "$repo_three" tag v0.9.9
git -C "$repo_three" checkout -q main 2>/dev/null || git -C "$repo_three" checkout -q master
printf 'main\n' > "$repo_three/main.txt"
commit_all "$repo_three" "main advances"

output="$(cd "$repo_three" && bash "$ROOT/scripts/find-previous-api-baseline.sh")"
[[ -z "$output" ]] || FAIL "expected unrelated or baseline-free tags to be ignored."

echo "Previous shell API baseline helper: OK"
