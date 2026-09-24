#!/usr/bin/env bash
# Regression tests for scripts/verify-release.sh.
#
# Build a self-contained release fixture, verify the happy path, prove a stale
# Directory.Build.props version stamp fails with the expected diagnostics, and
# confirm snapshot versions keep skipping formal metadata checks. Runs from
# scripts/lint-scripts.sh so the pre-tag verifier cannot regress silently.
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

write_props "0.1.0" "0.1.0.0"
cat > "$FIXTURE/CHANGELOG.md" <<'CHANGELOG_EOF'
# Changelog

## [Unreleased]

## [0.1.0] - 2026-09-24

### Added

- initial release
CHANGELOG_EOF
cat > "$FIXTURE/Origo.SourceGeneration/AnalyzerReleases.Shipped.md" <<'SHIPPED_EOF'
## Release 0.1.0
SHIPPED_EOF
cat > "$FIXTURE/Origo.SourceGeneration/AnalyzerReleases.Unshipped.md" <<'UNSHIPPED_EOF'
; no unshipped rules
UNSHIPPED_EOF
printf 'version 0.1.0\n' > "$FIXTURE/docs/README.zh.md"
printf 'version 0.1.0\n' > "$FIXTURE/docs/README.en.md"

POSITIVE_OUTPUT="$(TAG_VERSION=0.1.0 bash "$FIXTURE/scripts/verify-release.sh")"
grep -q "Release metadata verification PASSED for 0.1.0." <<<"$POSITIVE_OUTPUT" \
    || FAIL "formal release fixture did not pass:
$POSITIVE_OUTPUT"

write_props "0.1.1" "0.1.1.0"
set +e
NEGATIVE_OUTPUT="$(TAG_VERSION=0.1.0 bash "$FIXTURE/scripts/verify-release.sh" 2>&1)"
NEGATIVE_EXIT=$?
set -e
[[ $NEGATIVE_EXIT -eq 1 ]] \
    || FAIL "stale version stamps must fail verification, got exit $NEGATIVE_EXIT:
$NEGATIVE_OUTPUT"
grep -q "Directory.Build.props <Version> is '0.1.1', expected '0.1.0'" <<<"$NEGATIVE_OUTPUT" \
    || FAIL "missing <Version> mismatch diagnostic:
$NEGATIVE_OUTPUT"
grep -q "Directory.Build.props <AssemblyVersion> is '0.1.1.0', expected '0.1.0.0'" <<<"$NEGATIVE_OUTPUT" \
    || FAIL "missing <AssemblyVersion> mismatch diagnostic:
$NEGATIVE_OUTPUT"
grep -q "Directory.Build.props <FileVersion> is '0.1.1.0', expected '0.1.0.0'" <<<"$NEGATIVE_OUTPUT" \
    || FAIL "missing <FileVersion> mismatch diagnostic:
$NEGATIVE_OUTPUT"

SNAPSHOT_OUTPUT="$(TAG_VERSION=0.1.0-nightly.20260924 bash "$FIXTURE/scripts/verify-release.sh")"
grep -q "Release metadata verification SKIPPED for snapshot version 0.1.0-nightly.20260924." <<<"$SNAPSHOT_OUTPUT" \
    || FAIL "snapshot fixture did not skip formal metadata checks:
$SNAPSHOT_OUTPUT"

echo "Release-metadata verifier: OK"
