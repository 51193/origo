#!/usr/bin/env bash
# Regression tests for scripts/bash/check-weekly-base.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$ROOT/scripts/bash/check-weekly-base.sh"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

expect_pass() {
    local label="$1" base="$2" latest="$3"
    if ! bash "$SCRIPT" "$base" "$latest"; then
        FAIL "$label: expected pass for base=$base latest=$latest"
    fi
}

expect_fail() {
    local label="$1" base="$2" latest="$3" expected="$4"
    set +e
    local output
    output="$(bash "$SCRIPT" "$base" "$latest" 2>&1)"
    local code=$?
    set -e
    [[ $code -eq 1 ]] || FAIL "$label: expected exit 1, got $code:
$output"
    grep -qF "$expected" <<<"$output" || FAIL "$label: missing diagnostic '$expected':
$output"
}

expect_usage() {
    local label="$1"
    shift
    set +e
    bash "$SCRIPT" "$@" >/dev/null 2>&1
    local code=$?
    set -e
    [[ $code -eq 2 ]] || FAIL "$label: expected exit 2, got $code"
}

expect_pass "patch ahead" "0.1.10" "0.1.9"
expect_pass "minor ahead" "0.10.0" "0.2.0"
expect_pass "no formal tag" "0.1.1" ""
expect_fail "equal" "0.1.1" "0.1.1" "is not newer"
expect_fail "older" "0.1.9" "0.1.10" "is not newer"
expect_fail "older minor" "0.2.0" "0.10.0" "is not newer"
expect_usage "missing base"
expect_usage "invalid base" "not-a-version" "0.1.0"
expect_usage "invalid base with no formal tag" "not-a-version" ""
expect_usage "invalid latest" "0.2.0" "v0.1.0"

echo "Weekly base check: OK"
