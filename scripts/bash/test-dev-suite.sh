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
require_file .githooks/pre-commit
grep -Fxq 'bash scripts/bash/docs.sh check-staged' .githooks/pre-commit \
    || FAIL "pre-commit hook must check the staged generated documentation"
grep -Fxq 'bash scripts/bash/docs.sh check' .githooks/pre-commit \
    || FAIL "pre-commit hook must check the worktree documentation"

# The two full-gate entry points must run the same ordered gate sequence.
bash_steps=()
while IFS= read -r step; do
    bash_steps+=("$step")
done < <(sed -nE 's|^bash scripts/bash/([a-z-]+)\.sh$|\1|p' scripts/bash/ci.sh)
powershell_steps=()
while IFS= read -r step; do
    powershell_steps+=("$step")
done < <(sed -nE "s|^[[:space:]]*'([a-z-]+)\\.ps1',?$|\\1|p" scripts/powershell/ci.ps1)
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
    grep -Eq "^[[:space:]]*bash scripts/bash/${name//./\\.}([[:space:]]|$)" scripts/bash/lint-scripts.sh \
        || FAIL "Bash self-test is not wired into lint-scripts.sh: $name"
done

# Bash-free shared Python gates that the PowerShell lint gate relies on.
powershell_lint_active="$(grep -v '^[[:space:]]*#' scripts/powershell/lint-scripts.ps1)"
for marker in \
    "scripts/python/check_test.py" \
    "scripts/python/validate-agent-docs.py" \
    "scripts/python/validate-release-workflow.py" \
    "scripts/python/test-validate-release-workflow.py" \
    "scripts/python/work_identity.py" \
    "scripts/python/docs_generate_test.py" \
    "docs.ps1"; do
    grep -Fq "$marker" <<<"$powershell_lint_active" \
        || FAIL "PowerShell lint gate is missing shared check: $marker"
done
grep -Fq "test-api-inventory-parent.ps1" <<<"$powershell_lint_active" \
    || FAIL "PowerShell lint gate is missing its API-inventory self-test."
# Keep the native-Windows suite free of Bash-backed Python self-tests.
if grep -Eq 'work_identity_test\.py|test-validate-release-packages\.py' <<<"$powershell_lint_active"; then
    FAIL "PowerShell lint gate must not invoke Bash-backed Python self-tests."
fi
if grep -Eq '^[[:space:]]*(bash|sh)[[:space:]]' <<<"$powershell_lint_active"; then
    FAIL "PowerShell lint gate must not invoke a shell command."
fi

# The GitHub CI must expose the dedicated Bash parity check alongside the
# native PowerShell job; otherwise a local-only gate could silently disappear.
for job in bash-lint bash-checks powershell-lint powershell-checks; do
    grep -Eq "^[[:space:]]*${job}:" .github/workflows/ci.yml \
        || FAIL "GitHub CI is missing required job: $job"
done

# Every Bash, PowerShell, and Python script must appear as a table row in
# the script map.
for script in scripts/bash/*.sh scripts/powershell/*.ps1 scripts/python/*.py; do
    name="$(basename "$script")"
    grep -Eq "^\|[[:space:]]*\`${name//./\\.}\`" scripts/README.md \
        || FAIL "script is missing from the scripts/README.md table: $name"
done

# Documentation scripts must be in their platform-specific map section.
bash_map="$(sed -n '/^## Bash (`scripts\/bash`)/,/^## PowerShell (`scripts\/powershell`)/p' scripts/README.md)"
powershell_map="$(sed -n '/^## PowerShell (`scripts\/powershell`)/,/^## Adding or Moving Scripts/p' scripts/README.md)"
grep -Fq '`docs.sh`' <<<"$bash_map" \
    || FAIL "docs.sh is missing from the Bash script-map section"
grep -Fq '`docs.ps1`' <<<"$powershell_map" \
    || FAIL "docs.ps1 is missing from the PowerShell script-map section"
if grep -Fq '`docs.ps1`' <<<"$bash_map"; then
    FAIL "docs.ps1 must not be listed in the Bash script-map section"
fi

if ((failures > 0)); then
    echo "Development-suite parity: FAILED ($failures issue(s))." >&2
    exit 1
fi

echo "Development-suite parity: OK (Bash/PowerShell gates, self-tests, script map)."
