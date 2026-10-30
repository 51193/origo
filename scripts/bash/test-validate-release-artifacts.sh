#!/usr/bin/env bash
# Regression tests for scripts/bash/validate-release-artifacts.sh.
#
# The validator guards the exact nine-name release asset set; these fixtures
# pin the complete set, every missing name, unexpected extras, nested entries,
# wrong-version names, and the usage error paths.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-validate-artifacts.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

VALIDATOR="$ROOT/scripts/bash/validate-release-artifacts.sh"
VERSION="0.1.0"
ARTIFACTS="$FIXTURE/artifacts"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

expected_assets() {
    local version="$1"
    printf '%s\n' \
        "Origo.ConsoleBridge.${version}.nupkg" \
        "Origo.Core.${version}.nupkg" \
        "Origo.Core.Contracts.${version}.nupkg" \
        "Origo.Core.Kernel.${version}.nupkg" \
        "Origo.GodotAdapter.${version}.nupkg" \
        "origo-docs-${version}.tar.gz" \
        "origo-docs-${version}.zip" \
        "origo-source-${version}.tar.gz" \
        "nuget.config"
}

reset_artifacts() {
    local version="$1"
    local name
    rm -rf "$ARTIFACTS"
    mkdir -p "$ARTIFACTS"
    while IFS= read -r name; do
        printf 'fixture\n' > "$ARTIFACTS/$name"
    done < <(expected_assets "$version")
}

expect_pass() {
    local label="$1"
    local version="$2"
    local output
    if ! output="$(bash "$VALIDATOR" "$ARTIFACTS" "$version" 2>&1)"; then
        FAIL "$label: expected the artifact set to pass: $output"
    fi
    grep -qF "Release artifact set OK" <<<"$output" \
        || FAIL "$label: missing success line: $output"
}

expect_failure() {
    local label="$1"
    local diagnostic="$2"
    local version="$3"
    local output exit_code
    set +e
    output="$(bash "$VALIDATOR" "$ARTIFACTS" "$version" 2>&1)"
    exit_code=$?
    set -e
    [[ $exit_code -eq 1 ]] \
        || FAIL "$label: expected exit 1, got $exit_code: $output"
    grep -qF "$diagnostic" <<<"$output" \
        || FAIL "$label: missing diagnostic '$diagnostic': $output"
}

expect_usage_error() {
    local label="$1"
    shift
    local output exit_code
    set +e
    output="$(bash "$VALIDATOR" "$@" 2>&1)"
    exit_code=$?
    set -e
    [[ $exit_code -eq 2 ]] \
        || FAIL "$label: expected exit 2, got $exit_code: $output"
}

reset_artifacts "$VERSION"
expect_pass "complete set" "$VERSION"

while IFS= read -r name; do
    reset_artifacts "$VERSION"
    rm -f "$ARTIFACTS/$name"
    expect_failure "missing $name" "missing artifact: $name" "$VERSION"
done < <(expected_assets "$VERSION")

reset_artifacts "$VERSION"
printf 'extra\n' > "$ARTIFACTS/origo-extra.txt"
expect_failure "unexpected extra file" "unexpected artifact: origo-extra.txt" "$VERSION"

reset_artifacts "$VERSION"
mkdir "$ARTIFACTS/nested"
expect_failure "unexpected nested entry" \
    "unexpected non-file artifact entry: nested" "$VERSION"

reset_artifacts "$VERSION"
rm -f "$ARTIFACTS/Origo.Core.${VERSION}.nupkg"
printf 'wrong version\n' > "$ARTIFACTS/Origo.Core.0.1.1.nupkg"
expect_failure "wrong package version name" \
    "missing artifact: Origo.Core.${VERSION}.nupkg" "$VERSION"
expect_failure "wrong package version extra" \
    "unexpected artifact: Origo.Core.0.1.1.nupkg" "$VERSION"

reset_artifacts "$VERSION"
rm -f "$ARTIFACTS/origo-source-${VERSION}.tar.gz"
printf 'wrong version\n' > "$ARTIFACTS/origo-source-0.1.1.tar.gz"
expect_failure "wrong source archive version" \
    "missing artifact: origo-source-${VERSION}.tar.gz" "$VERSION"

SNAPSHOT_VERSION="0.1.1-nightly.20261026"
reset_artifacts "$SNAPSHOT_VERSION"
expect_pass "suffixed version" "$SNAPSHOT_VERSION"

EMPTY_DIR="$FIXTURE/empty"
mkdir -p "$EMPTY_DIR"
ARTIFACTS="$EMPTY_DIR"
expect_failure "empty directory" "missing artifact: nuget.config" "$VERSION"
ARTIFACTS="$FIXTURE/artifacts"

expect_usage_error "missing arguments"
expect_usage_error "missing version" "$ARTIFACTS"
expect_usage_error "missing directory" "$FIXTURE/does-not-exist" "$VERSION"
reset_artifacts "$VERSION"
expect_usage_error "invalid version" "$ARTIFACTS" "not-a-version"

echo "Release-artifact validator: OK"
