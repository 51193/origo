# shellcheck shell=bash
# Source-only acceptance-marker gate for the POSIX Evolution Demo smoke.
#
# The headless demo can exit zero without running its acceptance runner (for
# example a startup error that the engine reports as a normal quit). The gate
# therefore requires the runner's completion markers in addition to the exit
# code; the PowerShell gate has the same requirements.

# require_demo_acceptance_markers <exit-status> <output> [expected-checkpoints]
require_demo_acceptance_markers() {
    local status="$1"
    local output="$2"
    local expected_checkpoints="${3:-2}"

    if [[ "$status" -ne 0 ]]; then
        echo "ERROR: Evolution Demo acceptance smoke failed with exit code $status." >&2
        return 1
    fi
    if [[ "$output" != *"DEMO_BENCHMARK_OK"* ]]; then
        echo "ERROR: Evolution Demo acceptance smoke did not report DEMO_BENCHMARK_OK." >&2
        return 1
    fi
    if [[ "$output" != *"DEMO_CAMERA_OK"* ]]; then
        echo "ERROR: Evolution Demo acceptance smoke did not report DEMO_CAMERA_OK." >&2
        return 1
    fi
    if ! grep -Eq "DEMO_ITERATE_CHECKPOINTS count=${expected_checkpoints}([[:space:]]|$)" <<<"$output"; then
        echo "ERROR: Evolution Demo acceptance smoke did not produce $expected_checkpoints checkpoint(s)." >&2
        return 1
    fi
}
