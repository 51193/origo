#!/usr/bin/env bash
# Regression tests for scripts/validate-release-source.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-release-source.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

write_tree() {
    local version="$1"
    local assembly_version="$2"
    local formal="$3"
    local prefix="${4:-$version}"
    local tree="$FIXTURE/tree"
    local root="$tree/origo-${prefix}"
    rm -rf "$tree"
    mkdir -p "$root/scripts" "$root/Origo.SourceGeneration"
    cp "$ROOT/scripts/verify-release.sh" "$root/scripts/verify-release.sh"
    cat > "$root/Directory.Build.props" <<PROPS_EOF
<Project>
    <PropertyGroup>
        <Version>${version}</Version>
        <AssemblyVersion>${assembly_version}</AssemblyVersion>
        <FileVersion>${assembly_version}</FileVersion>
    </PropertyGroup>
</Project>
PROPS_EOF
    if [[ "$formal" == "true" ]]; then
        cat > "$root/CHANGELOG.md" <<CHANGELOG_EOF
# Changelog

## [Unreleased]

## [${version}] - 2026-10-21

### Added

- release source archive fixture
CHANGELOG_EOF
        printf '## Release %s\n' "$version" \
            > "$root/Origo.SourceGeneration/AnalyzerReleases.Shipped.md"
    else
        printf '# Changelog\n\n## [Unreleased]\n' > "$root/CHANGELOG.md"
        printf '; no release block for a snapshot\n' \
            > "$root/Origo.SourceGeneration/AnalyzerReleases.Shipped.md"
    fi
    printf '; no unshipped rules\n' \
        > "$root/Origo.SourceGeneration/AnalyzerReleases.Unshipped.md"
    repack "$prefix"
}

repack() {
    local prefix="$1"
    tar -czf "$FIXTURE/source.tar.gz" -C "$FIXTURE/tree" "origo-${prefix}"
}

expect_pass() {
    local label="$1" version="$2" trusted="${3:-}"
    local output
    if [[ -n "$trusted" ]]; then
        output="$(bash "$ROOT/scripts/validate-release-source.sh" "$FIXTURE/source.tar.gz" "$version" "$trusted")" \
            || FAIL "$label: expected pass:
$output"
    else
        output="$(bash "$ROOT/scripts/validate-release-source.sh" "$FIXTURE/source.tar.gz" "$version")" \
            || FAIL "$label: expected pass:
$output"
    fi
    grep -q "Release source archive validation passed for ${version}." <<<"$output" \
        || FAIL "$label: missing pass message:
$output"
}

expect_failure() {
    local label="$1" version="$2" message="$3"
    set +e
    local output
    output="$(bash "$ROOT/scripts/validate-release-source.sh" "$FIXTURE/source.tar.gz" "$version" 2>&1)"
    local code=$?
    set -e
    [[ $code -eq 1 ]] || FAIL "$label: expected exit 1, got $code:
$output"
    grep -qF "$message" <<<"$output" || FAIL "$label: missing diagnostic '$message':
$output"
}

write_tree "0.1.1" "0.1.1.0" "true"
expect_pass "formal archive" "0.1.1"

write_tree "0.1.0" "0.1.0.0" "true" "0.1.1"
expect_failure "stale Version" "0.1.1" "source archive <Version> is '0.1.0', expected '0.1.1'"

write_tree "0.1.1" "9.9.9.0" "true"
expect_failure "stale AssemblyVersion" "0.1.1" "source archive <AssemblyVersion> is '9.9.9.0', expected '0.1.1.0'"

write_tree "0.1.1" "0.1.1.0" "true"
python3 - "$FIXTURE/tree/origo-0.1.1/Directory.Build.props" <<'PY'
import sys
from pathlib import Path
path = Path(sys.argv[1])
text = path.read_text()
path.write_text(text.replace('<FileVersion>0.1.1.0</FileVersion>', '<FileVersion>9.9.9.0</FileVersion>'))
PY
repack "0.1.1"
expect_failure "stale FileVersion" "0.1.1" "source archive <FileVersion> is '9.9.9.0', expected '0.1.1.0'"

write_tree "0.1.1" "0.1.1.0" "true"
mkdir -p "$FIXTURE/tree/origo-0.1.1/nested"
cp "$FIXTURE/tree/origo-0.1.1/Directory.Build.props" "$FIXTURE/tree/origo-0.1.1/nested/Directory.Build.props"
repack "0.1.1"
expect_failure "multiple props" "0.1.1" "must contain exactly one Directory.Build.props"

write_tree "0.1.1" "0.1.1.0" "true" "0.2.0"
expect_failure "wrong archive prefix" "0.1.1" "source archive root must be 'origo-0.1.1/'"

write_tree "0.1.1" "0.1.1.0" "true"
printf '# Changelog\n\n## [Unreleased]\n' > "$FIXTURE/tree/origo-0.1.1/CHANGELOG.md"
repack "0.1.1"
expect_failure "missing formal metadata" "0.1.1" "CHANGELOG.md has no version block heading '## [0.1.1] -'"


write_tree "0.1.1" "0.1.1.0" "true"
printf '#!/usr/bin/env bash\nexit 1\n' > "$FIXTURE/tree/origo-0.1.1/scripts/verify-release.sh"
repack "0.1.1"
set +e
bash "$ROOT/scripts/validate-release-source.sh" "$FIXTURE/source.tar.gz" "0.1.1" >/dev/null 2>&1
EXIT_CODE=$?
set -e
[[ $EXIT_CODE -eq 1 ]] || FAIL "broken archive verifier expected exit 1, got $EXIT_CODE"
expect_pass "trusted verifier overrides archive verifier" "0.1.1" "$ROOT/scripts/verify-release.sh"

write_tree "0.1.1-nightly.20261021" "0.1.1.0" "false"
expect_pass "snapshot archive" "0.1.1-nightly.20261021"

set +e
bash "$ROOT/scripts/validate-release-source.sh" "$FIXTURE/missing.tar.gz" "0.1.1" >/dev/null 2>&1
EXIT_CODE=$?
set -e
[[ $EXIT_CODE -eq 2 ]] || FAIL "missing archive expected exit 2, got $EXIT_CODE"

echo "Release source archive validator: OK"
