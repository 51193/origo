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
# Python helper must be syntax-checked alongside the shell scripts. Compile in
# memory: py_compile would leave a scripts/__pycache__ artifact in the worktree.
if compgen -G "scripts/*.py" >/dev/null 2>&1; then
  if command -v python3 >/dev/null 2>&1; then
    python3 - <<'PYEOF'
import pathlib
import sys

failed = False
for path in sorted(pathlib.Path("scripts").glob("*.py")):
    try:
        compile(path.read_text(encoding="utf-8"), str(path), "exec")
    except SyntaxError as exc:
        failed = True
        print(f"Python syntax error in {path}: {exc}", file=sys.stderr)
if failed:
    sys.exit(1)
print("Script Python syntax OK")
PYEOF
  else
    echo "WARNING: python3 is not installed; skipping Python syntax check."
  fi
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

if command -v python3 >/dev/null 2>&1; then
    if python3 -c "import yaml" >/dev/null 2>&1; then
        python3 scripts/validate-release-workflow.py
    else
        echo "WARNING: PyYAML is not installed; skipping release-workflow guard."
    fi
    python3 scripts/validate-agent-docs.py
elif [[ -n "${CI:-}" || -n "${GITHUB_ACTIONS:-}" ]]; then
    echo "ERROR: python3 is required to run the release-workflow and agent-doc guards." >&2
    exit 1
else
    echo "WARNING: python3 is not installed; skipping workflow and agent-doc guards (bash -n still runs)."
fi

bash scripts/test-verify-release.sh

echo "Script lint: OK"
