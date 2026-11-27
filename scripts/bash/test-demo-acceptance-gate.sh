#!/usr/bin/env bash
# Regression tests for the POSIX Evolution Demo acceptance-marker gate.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
source "$ROOT/scripts/bash/demo-acceptance-gate-lib.sh"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

good='DEMO_CAMERA_OK
DEMO_BENCHMARK_OK seed=1 population=10 time=150.0
DEMO_ITERATE_CHECKPOINTS count=2 first=60 second=120'

require_demo_acceptance_markers 0 "$good" 2 >/dev/null 2>&1 \
    || FAIL "the complete acceptance output was rejected"

for bad in \
    "DEMO_CAMERA_OK" \
    "DEMO_BENCHMARK_OK" \
    "DEMO_CAMERA_OK
DEMO_ITERATE_CHECKPOINTS count=2" \
    "DEMO_CAMERA_OK
DEMO_BENCHMARK_OK" \
    "DEMO_CAMERA_OK
DEMO_BENCHMARK_OK
DEMO_ITERATE_CHECKPOINTS count=1" \
    "DEMO_CAMERA_OK
DEMO_BENCHMARK_OK
DEMO_ITERATE_CHECKPOINTS count=20" \
    "DEMO_CAMERA_OK
DEMO_BENCHMARK_OK
DEMO_ITERATE_CHECKPOINTS count=2x"; do
    if require_demo_acceptance_markers 0 "$bad" 2 >/dev/null 2>&1; then
        FAIL "incomplete acceptance output was accepted: $bad"
    fi
done

if require_demo_acceptance_markers 1 "$good" 2 >/dev/null 2>&1; then
    FAIL "a non-zero engine exit was accepted"
fi

# The gate must actually capture the demo output and apply the requirements;
# a helper that is never called would leave the original false-green hole.
gate="$ROOT/scripts/bash/godot-test.sh"
grep -Fq 'DEMO_OUTPUT=$(' "$gate" \
    || FAIL "godot-test.sh does not capture the Evolution Demo smoke output"
grep -Fq 'require_demo_acceptance_markers "$DEMO_EXIT" "$DEMO_OUTPUT" 2 || exit 1' "$gate" \
    || FAIL "godot-test.sh does not apply the Evolution Demo acceptance markers or does not propagate their failure"

echo "Demo acceptance gate tests: OK"
