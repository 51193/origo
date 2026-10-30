#!/usr/bin/env bash
# Regression tests for scripts/bash/find-previous-api-baseline.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
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

output="$(cd "$repo_one" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
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

output="$(cd "$repo_two" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -z "$output" ]] || FAIL "expected no output when no previous formal tag contains a baseline."

# Case 3: a newer formal tag with a baseline on an unrelated branch is not
# eligible, even though it contains the tracked baseline file.
repo_three="$FIXTURE/unrelated-tag"
new_repo "$repo_three"
mkdir -p "$repo_three/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_three/tools/ApiInventoryTool/shell-api-baseline.json"
commit_all "$repo_three" "release 0.1.0"
git -C "$repo_three" tag v0.1.0
git -C "$repo_three" checkout -qb side
printf 'side-baseline\n' > "$repo_three/tools/ApiInventoryTool/shell-api-baseline.json"
commit_all "$repo_three" "side branch"
git -C "$repo_three" tag v0.9.9
git -C "$repo_three" checkout -q main 2>/dev/null || git -C "$repo_three" checkout -q master
printf 'main\n' > "$repo_three/main.txt"
commit_all "$repo_three" "main advances"

output="$(cd "$repo_three" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -n "$output" ]] || FAIL "expected the older main baseline when the unrelated tag is ignored."
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "an unrelated branch tag with a baseline was selected."
rm -f "$output"

# Case 4: excluding the current release tag still works when a release
# workflow creates a version-sync commit after the tag.
repo_four="$FIXTURE/version-sync"
new_repo "$repo_four"
mkdir -p "$repo_four/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_four/tools/ApiInventoryTool/shell-api-baseline.json"
commit_all "$repo_four" "release 0.1.0"
git -C "$repo_four" tag v0.1.0
printf 'second-baseline\n' > "$repo_four/tools/ApiInventoryTool/shell-api-baseline.json"
commit_all "$repo_four" "release 0.2.0"
git -C "$repo_four" tag v0.2.0
printf 'version sync\n' > "$repo_four/Directory.Build.props"
commit_all "$repo_four" "sync version stamps"

output="$(cd "$repo_four" && ORIGO_CURRENT_RELEASE_TAG=v0.2.0 bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -n "$output" ]] || FAIL "expected a previous baseline when the current release tag is explicitly excluded."
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "version-sync commit fallback compared against the current release tag."
rm -f "$output"

# Case 5: consecutive tag-only release commits are siblings on main. The
# immediate previous release must still win over the older ancestor tag.
repo_five="$FIXTURE/synthetic-release-commits"
new_repo "$repo_five"
MAIN_BRANCH="$(git -C "$repo_five" branch --show-current)"
mkdir -p "$repo_five/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_five/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.0</Version>\n' > "$repo_five/Directory.Build.props"
commit_all "$repo_five" "release 0.1.0"
git -C "$repo_five" tag -a v0.1.0 -m "Release v0.1.0"

printf 'second-baseline\n' > "$repo_five/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.2.0-dev</Version>\n' > "$repo_five/Directory.Build.props"
commit_all "$repo_five" "develop 0.2.0"
main_two="$(git -C "$repo_five" rev-parse HEAD)"

git -C "$repo_five" checkout -q --detach "$main_two"
printf '<Version>0.2.0</Version>\n' > "$repo_five/Directory.Build.props"
commit_all "$repo_five" "chore(release): sync version stamps to v0.2.0"
git -C "$repo_five" tag -a v0.2.0 -m "Release v0.2.0"
git -C "$repo_five" checkout -q "$MAIN_BRANCH"

printf 'third-baseline\n' > "$repo_five/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.3.0-dev</Version>\n' > "$repo_five/Directory.Build.props"
commit_all "$repo_five" "develop 0.3.0"
main_three="$(git -C "$repo_five" rev-parse HEAD)"

git -C "$repo_five" checkout -q --detach "$main_three"
printf '<Version>0.3.0</Version>\n' > "$repo_five/Directory.Build.props"
commit_all "$repo_five" "chore(release): sync version stamps to v0.3.0"
git -C "$repo_five" tag -a v0.3.0 -m "Release v0.3.0"

output="$(cd "$repo_five" && ORIGO_CURRENT_RELEASE_TAG=v0.3.0 bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -n "$output" ]] || FAIL "expected the previous synthetic release baseline when consecutive tag-only releases exist."
[[ "$(cat "$output")" == "second-baseline" ]] || FAIL "tag-only release ancestry skipped the immediate previous release."
rm -f "$output"


# Case 6: a tag newer than the props-derived source version is skipped so an
# older usable baseline is selected; the helper succeeds (the hard-error path is
# only when no older usable baseline remains, Case 10).
repo_six="$FIXTURE/version-ceiling"
new_repo "$repo_six"
MAIN_BRANCH="$(git -C "$repo_six" branch --show-current)"
mkdir -p "$repo_six/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_six/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.0</Version>\n' > "$repo_six/Directory.Build.props"
commit_all "$repo_six" "release 0.1.0"
git -C "$repo_six" tag v0.1.0
printf 'newer-baseline\n' > "$repo_six/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.1-dev</Version>\n' > "$repo_six/Directory.Build.props"
commit_all "$repo_six" "develop 0.1.1"
main_six="$(git -C "$repo_six" rev-parse HEAD)"
git -C "$repo_six" checkout -q --detach "$main_six"
printf '<Version>0.1.2</Version>\n' > "$repo_six/Directory.Build.props"
commit_all "$repo_six" "chore(release): sync version stamps to v0.1.2"
git -C "$repo_six" tag v0.1.2
git -C "$repo_six" checkout -q "$MAIN_BRANCH"

output="$(cd "$repo_six" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "a tag newer than the source version was selected."
rm -f "$output"

# Case 7: a non-ancestor feature commit with a baseline is not a release tag.
repo_seven="$FIXTURE/reject-non-metadata"
new_repo "$repo_seven"
MAIN_BRANCH="$(git -C "$repo_seven" branch --show-current)"
mkdir -p "$repo_seven/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_seven/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.0</Version>\n' > "$repo_seven/Directory.Build.props"
commit_all "$repo_seven" "release 0.1.0"
git -C "$repo_seven" tag v0.1.0
printf 'second-baseline\n' > "$repo_seven/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.1-dev</Version>\n' > "$repo_seven/Directory.Build.props"
commit_all "$repo_seven" "develop 0.1.1"
main_seven="$(git -C "$repo_seven" rev-parse HEAD)"
git -C "$repo_seven" checkout -q --detach "$main_seven"
printf 'side-baseline\n' > "$repo_seven/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.1</Version>\n' > "$repo_seven/Directory.Build.props"
commit_all "$repo_seven" "feature: side work"
git -C "$repo_seven" tag v0.1.1
git -C "$repo_seven" checkout -q "$MAIN_BRANCH"

output="$(cd "$repo_seven" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "a non-ancestor feature commit was accepted as a release tag."
rm -f "$output"

# Case 8: a metadata-looking commit that also changes another file is rejected.
repo_eight="$FIXTURE/reject-extra-file"
new_repo "$repo_eight"
MAIN_BRANCH="$(git -C "$repo_eight" branch --show-current)"
mkdir -p "$repo_eight/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_eight/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.0</Version>\n' > "$repo_eight/Directory.Build.props"
commit_all "$repo_eight" "release 0.1.0"
git -C "$repo_eight" tag v0.1.0
printf 'second-baseline\n' > "$repo_eight/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.1-dev</Version>\n' > "$repo_eight/Directory.Build.props"
commit_all "$repo_eight" "develop 0.1.1"
main_eight="$(git -C "$repo_eight" rev-parse HEAD)"
git -C "$repo_eight" checkout -q --detach "$main_eight"
printf 'side-baseline\n' > "$repo_eight/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.1</Version>\n' > "$repo_eight/Directory.Build.props"
printf 'extra\n' > "$repo_eight/README.md"
commit_all "$repo_eight" "chore(release): sync version stamps to v0.1.1"
git -C "$repo_eight" tag v0.1.1
git -C "$repo_eight" checkout -q "$MAIN_BRANCH"

output="$(cd "$repo_eight" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "a metadata commit with an extra file was accepted."
rm -f "$output"

# Case 9: an equal-version metadata release commit is selected via props.
repo_nine="$FIXTURE/equal-version"
new_repo "$repo_nine"
MAIN_BRANCH="$(git -C "$repo_nine" branch --show-current)"
mkdir -p "$repo_nine/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_nine/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.0</Version>\n' > "$repo_nine/Directory.Build.props"
commit_all "$repo_nine" "release 0.1.0"
git -C "$repo_nine" tag v0.1.0
printf 'equal-baseline\n' > "$repo_nine/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.1-dev</Version>\n' > "$repo_nine/Directory.Build.props"
commit_all "$repo_nine" "develop 0.1.1"
main_nine="$(git -C "$repo_nine" rev-parse HEAD)"
git -C "$repo_nine" checkout -q --detach "$main_nine"
printf '<Version>0.1.1</Version>\n' > "$repo_nine/Directory.Build.props"
commit_all "$repo_nine" "chore(release): sync version stamps to v0.1.1"
git -C "$repo_nine" tag v0.1.1
git -C "$repo_nine" checkout -q "$MAIN_BRANCH"

output="$(cd "$repo_nine" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ "$(cat "$output")" == "equal-baseline" ]] || FAIL "an equal-version metadata release commit was not selected via props."
rm -f "$output"

# Case 10: fail closed when the only formal release tag is newer than the
# source version. Silently comparing against nothing would hide the release.
repo_ten="$FIXTURE/fail-closed-newer-tag"
new_repo "$repo_ten"
mkdir -p "$repo_ten/tools/ApiInventoryTool"
printf '<Version>0.1.0</Version>\n' > "$repo_ten/Directory.Build.props"
printf 'first-baseline\n' > "$repo_ten/tools/ApiInventoryTool/shell-api-baseline.json"
commit_all "$repo_ten" "develop 0.1.0"
git -C "$repo_ten" tag v0.1.2

if (cd "$repo_ten" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh" >/dev/null 2>"$FIXTURE/case10.err"); then
    FAIL "a formal tag newer than the source version must fail closed, not skip silently."
fi
grep -q "newer than the current source version" "$FIXTURE/case10.err" \
    || FAIL "the newer-tag fail-closed error must name the newer formal tag."

# Case 11: fail closed when a baseline-bearing formal tag is neither an ancestor
# nor the recognized metadata-only release commit, and no older baseline exists.
repo_eleven="$FIXTURE/fail-closed-unresolvable-tag"
new_repo "$repo_eleven"
MAIN_BRANCH="$(git -C "$repo_eleven" branch --show-current)"
mkdir -p "$repo_eleven/tools/ApiInventoryTool"
printf '<Version>0.1.2-dev</Version>\n' > "$repo_eleven/Directory.Build.props"
printf 'main-baseline\n' > "$repo_eleven/tools/ApiInventoryTool/shell-api-baseline.json"
commit_all "$repo_eleven" "develop 0.1.2"
main_eleven="$(git -C "$repo_eleven" rev-parse HEAD)"
git -C "$repo_eleven" checkout -q --detach "$main_eleven"
printf 'side-baseline\n' > "$repo_eleven/tools/ApiInventoryTool/shell-api-baseline.json"
printf 'extra\n' > "$repo_eleven/README.md"
commit_all "$repo_eleven" "feature: side work"
git -C "$repo_eleven" tag v0.1.1
git -C "$repo_eleven" checkout -q "$MAIN_BRANCH"

if (cd "$repo_eleven" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh" >/dev/null 2>&1); then
    FAIL "an unresolvable baseline-bearing release tag must fail closed, not skip silently."
fi

# Case 12: the documented first-release exception is preserved: eligible formal
# releases that predate the baseline tool skip the comparison with exit 0.
repo_twelve="$FIXTURE/first-release-exception"
new_repo "$repo_twelve"
printf '<Version>0.1.0</Version>\n' > "$repo_twelve/Directory.Build.props"
printf 'release notes\n' > "$repo_twelve/README.md"
commit_all "$repo_twelve" "release 0.0.9"
git -C "$repo_twelve" tag v0.0.9
printf 'main advances\n' > "$repo_twelve/main.txt"
commit_all "$repo_twelve" "main advances"

output="$(cd "$repo_twelve" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -z "$output" ]] || FAIL "a pre-baseline formal release must skip the comparison with no output."

# Case 13: several newer formal tags are skipped when an older usable baseline
# exists, so a stale development baseline does not break comparison against the
# last release the source actually builds on.
repo_thirteen="$FIXTURE/multiple-newer-tags"
new_repo "$repo_thirteen"
MAIN_BRANCH="$(git -C "$repo_thirteen" branch --show-current)"
mkdir -p "$repo_thirteen/tools/ApiInventoryTool"
printf 'first-baseline\n' > "$repo_thirteen/tools/ApiInventoryTool/shell-api-baseline.json"
printf '<Version>0.1.0</Version>\n' > "$repo_thirteen/Directory.Build.props"
commit_all "$repo_thirteen" "release 0.1.0"
git -C "$repo_thirteen" tag v0.1.0
printf '<Version>0.1.1-dev</Version>\n' > "$repo_thirteen/Directory.Build.props"
commit_all "$repo_thirteen" "develop 0.1.1"
git -C "$repo_thirteen" tag v0.2.0
git -C "$repo_thirteen" tag v0.3.0

output="$(cd "$repo_thirteen" && bash "$ROOT/scripts/bash/find-previous-api-baseline.sh")"
[[ -n "$output" ]] || FAIL "newer formal tags must be skipped when an older usable baseline exists."
[[ "$(cat "$output")" == "first-baseline" ]] || FAIL "a newer formal tag was selected despite the version ceiling."
rm -f "$output"

echo "Previous shell API baseline helper: OK"
