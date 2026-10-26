#!/usr/bin/env bash
# Development-suite parity gate: keep the Bash gate set, the PowerShell gate
# set, the shared self-tests, and the script map from drifting silently.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

failures=0
FAIL() {
    echo "ERROR: $*" >&2
    failures=$((failures + 1))
}

require_file() {
    local path="$1"
    [[ -f "$path" ]] || FAIL "required development-suite file is missing: $path"
}

gate_names=(lint-scripts format api-inventory test benchmark godot-test package-consumer-smoke)
for gate in "${gate_names[@]}"; do
    require_file "scripts/bash/$gate.sh"
    require_file "scripts/powershell/$gate.ps1"
done
require_file scripts/bash/check.sh
require_file scripts/powershell/check.ps1
require_file scripts/bash/lint-commits.sh
require_file scripts/powershell/lint-commits.ps1
require_file scripts/bash/ci.sh
require_file scripts/powershell/ci.ps1
require_file scripts/bash/test-api-inventory-parent.sh
require_file scripts/powershell/test-api-inventory-parent.ps1
require_file scripts/powershell/common.ps1

# The two full-gate entry points must run the same ordered gate sequence.
mapfile -t bash_steps < <(
    sed -nE 's|^bash scripts/bash/([a-z-]+)\.sh$|\1|p' scripts/bash/ci.sh
)
mapfile -t powershell_steps < <(
    sed -nE "s|^[[:space:]]*'([a-z-]+)\\.ps1',?$|\\1|p" scripts/powershell/ci.ps1
)
expected_steps="lint-scripts format api-inventory test benchmark godot-test package-consumer-smoke"
if [[ "${bash_steps[*]}" != "$expected_steps" ]]; then
    FAIL "Bash CI gate sequence drifted: ${bash_steps[*]}"
fi
if [[ "${powershell_steps[*]}" != "$expected_steps" ]]; then
    FAIL "PowerShell CI gate sequence drifted: ${powershell_steps[*]}"
fi

# Every Bash self-test must be wired into the Bash lint gate (the parity gate
# itself is the exception because it is the gate doing this check).
for self_test in scripts/bash/test-*.sh; do
    name="$(basename "$self_test")"
    [[ "$name" == "test-dev-suite.sh" ]] && continue
    grep -Fq "bash scripts/bash/$name" scripts/bash/lint-scripts.sh \
        || FAIL "Bash self-test is not wired into lint-scripts.sh: $name"
done

# Bash-free shared Python gates that the PowerShell lint gate relies on.
for marker in \
    "scripts/python/check_test.py" \
    "scripts/python/validate-agent-docs.py" \
    "scripts/python/validate-release-workflow.py" \
    "scripts/python/test-validate-release-workflow.py" \
    "scripts/python/work_identity.py"; do
    grep -Fq "$marker" scripts/powershell/lint-scripts.ps1 \
        || FAIL "PowerShell lint gate is missing shared check: $marker"
done
# Keep the native-Windows suite free of Bash-backed Python self-tests.
if grep -Eq 'work_identity_test\.py|test-validate-release-packages\.py' scripts/powershell/lint-scripts.ps1; then
    FAIL "PowerShell lint gate must not invoke Bash-backed Python self-tests."
fi

# Every Bash/PowerShell script must appear in the script map.
for script in scripts/bash/*.sh scripts/powershell/*.ps1; do
    name="$(basename "$script")"
    grep -Fq "\`$name\`" scripts/README.md \
        || FAIL "script is missing from scripts/README.md: $name"
done

if ((failures > 0)); then
    echo "Development-suite parity: FAILED ($failures issue(s))." >&2
    exit 1
fi

echo "Development-suite parity: OK (Bash/PowerShell gates, self-tests, script map)."
