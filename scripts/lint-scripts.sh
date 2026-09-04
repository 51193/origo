#!/usr/bin/env bash
# CI step: shell and embedded-tooling lint for repository scripts.
# Mirrors the "lint-scripts" job of the GitHub Actions workflow.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

for script in scripts/*.sh dotnet; do
    bash -n "$script"
done

if ! command -v shellcheck >/dev/null 2>&1; then
    if [[ -n "${CI:-}" || -n "${GITHUB_ACTIONS:-}" ]]; then
        echo "ERROR: shellcheck is not installed." >&2
        echo "Install shellcheck (e.g. 'sudo apt-get install shellcheck') and re-run." >&2
        exit 1
    fi
    echo "WARNING: shellcheck is not installed; skipping shellcheck (bash -n still runs)."
else
    shellcheck --severity=warning scripts/*.sh dotnet
fi

# benchmark.sh previously embedded Python in heredocs. Any extracted or new
# Python helper must be syntax-checked alongside the shell scripts.
if compgen -G "scripts/*.py" >/dev/null 2>&1; then
    python3 -m py_compile scripts/*.py
fi

if python3 -c "import yaml" >/dev/null 2>&1; then
    python3 - <<'PYEOF'
import glob
import sys
import yaml

failures = []
for path in glob.glob(".github/workflows/*.yml") + [".github/dependabot.yml"]:
    try:
        with open(path, encoding="utf-8") as handle:
            yaml.safe_load(handle)
    except Exception as exc:
        failures.append(f"{path}: {exc}")

if failures:
    print("Workflow YAML syntax FAILED:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    sys.exit(1)

print("Workflow YAML syntax OK")
PYEOF
elif [[ -n "${CI:-}" || -n "${GITHUB_ACTIONS:-}" ]]; then
    echo "ERROR: PyYAML is not installed." >&2
    echo "Install PyYAML (e.g. 'sudo apt-get install python3-yaml') and re-run." >&2
    exit 1
else
    echo "WARNING: PyYAML is not installed; skipping workflow YAML syntax check."
fi

echo "Script lint: OK"
