#!/usr/bin/env bash
# CI step: shell and embedded-tooling lint for repository scripts.
# Mirrors the "bash-lint" job of the GitHub Actions workflow.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

SHELL_FILES=(scripts/bash/*.sh dotnet .githooks/*)

for script in "${SHELL_FILES[@]}"; do
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
    shellcheck --severity=warning "${SHELL_FILES[@]}"
fi

# benchmark.sh previously embedded Python in heredocs. Any extracted or new
# Python helper must be syntax-checked alongside the shell scripts. Compile in
# memory: py_compile would leave a scripts/__pycache__ artifact in the worktree.
if compgen -G "scripts/python/*.py" >/dev/null 2>&1; then
  if command -v python3 >/dev/null 2>&1; then
    python3 - <<'PYEOF'
import pathlib
import sys

failed = False
for path in sorted(pathlib.Path("scripts/python").glob("*.py")):
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
    if [[ -n "${ORIGO_BRANCH_NAME:-}" ]]; then
        bash scripts/bash/work-identity.sh validate --branch "$ORIGO_BRANCH_NAME"
    else
        bash scripts/bash/work-identity.sh validate
    fi
    if python3 -c "import yaml" >/dev/null 2>&1; then
        python3 scripts/python/validate-release-workflow.py
        PYTHONDONTWRITEBYTECODE=1 python3 scripts/python/test-validate-release-workflow.py
    else
        echo "WARNING: PyYAML is not installed; skipping release-workflow guard."
    fi
    PYTHONDONTWRITEBYTECODE=1 python3 scripts/python/work_identity_test.py
    PYTHONDONTWRITEBYTECODE=1 python3 scripts/python/check_test.py
    python3 scripts/python/validate-agent-docs.py
    python3 scripts/python/test-validate-release-packages.py
    bash scripts/bash/test-write-version-stamp.sh
    bash scripts/bash/test-validate-release-source.sh
    bash scripts/bash/test-release-check.sh
    bash scripts/bash/test-lint-pr-body.sh
elif [[ -n "${CI:-}" || -n "${GITHUB_ACTIONS:-}" ]]; then
    echo "ERROR: python3 is required to run the release-workflow, release-package, and agent-doc guards." >&2
    exit 1
else
    echo "WARNING: python3 is not installed; skipping workflow and agent-doc guards (bash -n still runs)."
fi

bash scripts/bash/test-verify-release.sh
bash scripts/bash/test-docs.sh
bash scripts/bash/test-api-inventory-parent.sh
bash scripts/bash/test-find-previous-api-baseline.sh
bash scripts/bash/test-check-weekly-base.sh
bash scripts/bash/test-verify-release-provenance.sh
bash scripts/bash/test-package-consumer-smoke-lib.sh
bash scripts/bash/test-download-godot-deps.sh
bash scripts/bash/test-pre-push-hook.sh
bash scripts/bash/test-dotnet-env.sh
bash scripts/bash/test-dev-suite.sh

echo "Script lint: OK"
