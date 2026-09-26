#!/usr/bin/env bash
# Regression tests for scripts/verify-release.sh.
#
# Build self-contained release fixtures and exercise the happy path, the
# version-stamp checks, every formal metadata failure, and the snapshot skip
# path. Runs from scripts/lint-scripts.sh so the pre-tag verifier cannot
# regress silently.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-verify-release.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

mkdir -p "$FIXTURE/scripts" "$FIXTURE/docs" "$FIXTURE/Origo.SourceGeneration"
cp "$ROOT/scripts/verify-release.sh" "$FIXTURE/scripts/verify-release.sh"

write_props() {
    local version="$1"
    local assembly_version="$2"
    cat > "$FIXTURE/Directory.Build.props" <<PROPS_EOF
<Project>
    <PropertyGroup>
        <Version>${version}</Version>
        <AssemblyVersion>${assembly_version}</AssemblyVersion>
        <FileVersion>${assembly_version}</FileVersion>
    </PropertyGroup>
</Project>
PROPS_EOF
}

write_changelog() {
    local unreleased_body="${1:-}"
    local version_body="${2:-}"
    cat > "$FIXTURE/CHANGELOG.md" <<CHANGELOG_EOF
# Changelog

## [Unreleased]

${unreleased_body}
${version_body}
CHANGELOG_EOF
}

write_valid_fixture() {
    write_props "0.1.0" "0.1.0.0"
    write_changelog "" "## [0.1.0] - 2026-09-24

### Added

- initial release"
    cat > "$FIXTURE/Origo.SourceGeneration/AnalyzerReleases.Shipped.md" <<'SHIPPED_EOF'
## Release 0.1.0
SHIPPED_EOF
    cat > "$FIXTURE/Origo.SourceGeneration/AnalyzerReleases.Unshipped.md" <<'UNSHIPPED_EOF'
; no unshipped rules
UNSHIPPED_EOF
    printf 'version 0.1.0\n' > "$FIXTURE/docs/README.zh.md"
    printf 'version 0.1.0\n' > "$FIXTURE/docs/README.en.md"
}

expect_failure() {
    local label="$1"
    local expected_message="$2"
    set +e
    local output
    output="$(TAG_VERSION=0.1.0 bash "$FIXTURE/scripts/verify-release.sh" 2>&1)"
    local exit_code=$?
    set -e
    [[ $exit_code -eq 1 ]] \
        || FAIL "$label: expected exit 1, got $exit_code:
$output"
    grep -qF "$expected_message" <<<"$output" \
        || FAIL "$label: missing diagnostic '$expected_message':
$output"
}

write_valid_fixture
POSITIVE_OUTPUT="$(TAG_VERSION=0.1.0 bash "$FIXTURE/scripts/verify-release.sh")"
grep -q "Release metadata verification PASSED for 0.1.0." <<<"$POSITIVE_OUTPUT" \
    || FAIL "formal release fixture did not pass:
$POSITIVE_OUTPUT"

write_props "0.1.1" "0.1.1.0"
expect_failure "stale <Version>" "Directory.Build.props <Version> is '0.1.1', expected '0.1.0'"
expect_failure "stale <AssemblyVersion>" "Directory.Build.props <AssemblyVersion> is '0.1.1.0', expected '0.1.0.0'"
expect_failure "stale <FileVersion>" "Directory.Build.props <FileVersion> is '0.1.1.0', expected '0.1.0.0'"

write_valid_fixture
write_changelog "- pending release note" "## [0.1.0] - 2026-09-24"
expect_failure "non-empty [Unreleased]" "[Unreleased] section is not empty"

write_valid_fixture
write_changelog "" ""
expect_failure "missing CHANGELOG version block" "CHANGELOG.md has no version block heading '## [0.1.0] -'"

write_valid_fixture
printf '; no release block\n' > "$FIXTURE/Origo.SourceGeneration/AnalyzerReleases.Shipped.md"
expect_failure "missing analyzer shipped block" "AnalyzerReleases.Shipped.md has no '## Release 0.1.0' block"

write_valid_fixture
printf '| ORIGOSG999 | Test | Error | fixture |\n' >> "$FIXTURE/Origo.SourceGeneration/AnalyzerReleases.Unshipped.md"
expect_failure "unshipped analyzer rule" "AnalyzerReleases.Unshipped.md still contains unshipped rules"

write_valid_fixture
printf 'version 9.9.9\n' > "$FIXTURE/docs/README.zh.md"
expect_failure "docs version stamp" "docs/README.zh.md does not mention version 0.1.0"

write_valid_fixture
SNAPSHOT_OUTPUT="$(TAG_VERSION=0.1.0-nightly.20260924 bash "$FIXTURE/scripts/verify-release.sh")"
grep -q "Release metadata verification SKIPPED for snapshot version 0.1.0-nightly.20260924." <<<"$SNAPSHOT_OUTPUT" \
    || FAIL "snapshot fixture did not skip formal metadata checks:
$SNAPSHOT_OUTPUT"

echo "Release-metadata verifier: OK"
