#!/usr/bin/env bash
# Regression tests for scripts/verify-release-provenance.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-release-provenance.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

git init -q --bare "$FIXTURE/origin.git"
git init -q -b main "$FIXTURE/repo"
REPO="$FIXTURE/repo"
git -C "$REPO" config user.name "Origo Test"
git -C "$REPO" config user.email "origo-test@example.invalid"
write_props() {
    local version="$1"
    cat > "$REPO/Directory.Build.props" <<PROPS_EOF
<Project>
    <PropertyGroup>
        <Version>${version}</Version>
        <AssemblyVersion>0.1.1.0</AssemblyVersion>
        <FileVersion>0.1.1.0</FileVersion>
    </PropertyGroup>
</Project>
PROPS_EOF
}

write_props "0.1.1-dev"
git -C "$REPO" add -A
git -C "$REPO" commit -qm "main baseline"
git -C "$REPO" remote add origin "$FIXTURE/origin.git"
git -C "$REPO" push -q origin main
MAIN_COMMIT="$(git -C "$REPO" rev-parse HEAD)"

expect_pass() {
    local label="$1" tag="$2" commit="$3"
    if ! (cd "$REPO" && bash "$ROOT/scripts/verify-release-provenance.sh" "$tag" "$commit"); then
        FAIL "$label: expected pass"
    fi
}

expect_fail() {
    local label="$1" tag="$2" commit="$3" message="$4"
    set +e
    local output
    output="$(cd "$REPO" && bash "$ROOT/scripts/verify-release-provenance.sh" "$tag" "$commit" 2>&1)"
    local code=$?
    set -e
    [[ $code -eq 1 ]] || FAIL "$label: expected exit 1, got $code:
$output"
    grep -qF "$message" <<<"$output" || FAIL "$label: missing diagnostic '$message':
$output"
}

expect_pass "formal tag on main" "v0.1.0" "$MAIN_COMMIT"

git -C "$REPO" checkout -q -b feature
printf 'feature\n' > "$REPO/feature.txt"
git -C "$REPO" add -A
git -C "$REPO" commit -qm "unmerged feature"
FEATURE_COMMIT="$(git -C "$REPO" rev-parse HEAD)"
git -C "$REPO" tag v0.1.1
expect_fail "formal tag on unmerged feature" "v0.1.1" "$FEATURE_COMMIT" \
    "does not point at main history"

git -C "$REPO" checkout -q main
git -C "$REPO" checkout -q --detach "$MAIN_COMMIT"
write_props "0.1.2"
git -C "$REPO" add -A
git -C "$REPO" commit -qm "chore(release): sync version stamps to v0.1.2"
FORMAL_RELEASE_COMMIT="$(git -C "$REPO" rev-parse HEAD)"
git -C "$REPO" tag v0.1.2
expect_pass "formal metadata release commit" "v0.1.2" "$FORMAL_RELEASE_COMMIT"
git -C "$REPO" tag v0.1.3 "$FORMAL_RELEASE_COMMIT"
expect_fail "formal tag with a mismatched version subject" "v0.1.3" "$FORMAL_RELEASE_COMMIT" \
    "points at a version-stamp commit for a different version"

git -C "$REPO" checkout -q main
git -C "$REPO" checkout -q --detach "$MAIN_COMMIT"
write_props "0.1.1-nightly.20261021"
git -C "$REPO" add -A
git -C "$REPO" commit -qm "chore: bump version to 0.1.1-nightly.20261021 for weekly build"
SNAPSHOT_COMMIT="$(git -C "$REPO" rev-parse HEAD)"
git -C "$REPO" tag v0.1.1-nightly.20261021
expect_pass "weekly snapshot commit" "v0.1.1-nightly.20261021" "$SNAPSHOT_COMMIT"
git -C "$REPO" tag v0.1.3-nightly.20261021 "$SNAPSHOT_COMMIT"
expect_fail "snapshot tag with a mismatched version subject" "v0.1.3-nightly.20261021" "$SNAPSHOT_COMMIT" \
    "points at a version-stamp commit for a different version"
expect_pass "historical snapshot tag on main" "v0.1.0-nightly.20261020" "$MAIN_COMMIT"

git -C "$REPO" checkout -q main
git -C "$REPO" checkout -q --detach "$MAIN_COMMIT"
write_props "0.1.2-nightly.20261022"
printf 'extra\n' > "$REPO/README.md"
git -C "$REPO" add -A
git -C "$REPO" commit -qm "chore: bump version to 0.1.2-nightly.20261022 for weekly build"
SNAPSHOT_EXTRA_COMMIT="$(git -C "$REPO" rev-parse HEAD)"
git -C "$REPO" tag v0.1.2-nightly.20261022
expect_fail "snapshot with extra file" "v0.1.2-nightly.20261022" "$SNAPSHOT_EXTRA_COMMIT" \
    "does not point at main history or a weekly/release version-stamp commit"

git -C "$REPO" checkout -q main
git -C "$REPO" checkout -q --detach "$MAIN_COMMIT"
write_props "0.1.3-nightly.20261023"
git -C "$REPO" add -A
git -C "$REPO" commit -qm "feature: unrelated work"
SNAPSHOT_FEATURE_COMMIT="$(git -C "$REPO" rev-parse HEAD)"
git -C "$REPO" tag v0.1.3-nightly.20261023
expect_fail "snapshot with feature subject" "v0.1.3-nightly.20261023" "$SNAPSHOT_FEATURE_COMMIT" \
    "does not point at main history or a weekly/release version-stamp commit"


git -C "$REPO" checkout -q main
git -C "$REPO" checkout -q --detach "$MAIN_COMMIT"
write_props "0.1.4-nightly.20261024"
git -C "$REPO" add -A
git -C "$REPO" commit -qm "chore(release): sync version stamps to v0.1.4-nightly.20261024"
SNAPSHOT_RELEASE_COMMIT="$(git -C "$REPO" rev-parse HEAD)"
git -C "$REPO" tag v0.1.4-nightly.20261024
expect_pass "snapshot release-sync commit" "v0.1.4-nightly.20261024" "$SNAPSHOT_RELEASE_COMMIT"

git -C "$REPO" checkout -q main
git -C "$REPO" checkout -q --detach "$MAIN_COMMIT"
write_props "0.1.5"
python3 - "$REPO/Directory.Build.props" <<'PYEOF'
import sys
from pathlib import Path
path = Path(sys.argv[1])
text = path.read_text().replace('</Project>', '    <Target Name="Injected" BeforeTargets="Build">\n        <Exec Command="echo injected" />\n    </Target>\n</Project>')
path.write_text(text)
PYEOF
git -C "$REPO" add -A
git -C "$REPO" commit -qm "chore(release): sync version stamps to v0.1.5"
EXTRA_PROPS_COMMIT="$(git -C "$REPO" rev-parse HEAD)"
git -C "$REPO" tag v0.1.5
expect_fail "formal metadata with extra MSBuild content" "v0.1.5" "$EXTRA_PROPS_COMMIT" \
    "does not point at main history or the metadata-only release commit"

set +e
(cd "$REPO" && bash "$ROOT/scripts/verify-release-provenance.sh" "v0.1.0" >/dev/null 2>&1)
CODE=$?
set -e
[[ $CODE -eq 2 ]] || FAIL "missing commit expected exit 2, got $CODE"

echo "Release provenance: OK"
