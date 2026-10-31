#!/usr/bin/env python3
"""Pin the structural verification gates in ``.github/workflows/ci.yml``.

The local gates only protect CI while the required jobs and steps still run the
same commands with the same inputs. This guard parses the workflow and pins its
triggers, required jobs, matrices, environment, action inputs, step order,
shells, and exact commands, so deleting a job/step, adding ``if:``,
``continue-on-error:``, a step ``env:``, a ``working-directory:``, a job/workflow
``defaults:``, a wrong shell, or masking a command with ``echo``/``|| true`` fails
locally before CI ever runs.

An action's version tag is not pinned: Dependabot updates it as reviewed
configuration, and the guard only reviews the action identity and its inputs.
Duplicate YAML mapping keys are rejected explicitly because PyYAML otherwise
keeps the last value silently.
"""

from __future__ import annotations

import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github" / "workflows" / "ci.yml"

EXPECTED_TRIGGERS = {
    "push": {"branches": ["main"]},
    "pull_request": {"branches": ["main"]},
}
EXPECTED_CONCURRENCY = {
    "group": "${{ github.workflow }}-${{ github.ref }}",
    "cancel-in-progress": True,
}
JOB_ENV = {
    "ORIGO_BRANCH_NAME": "${{ github.head_ref || github.ref_name }}",
    "ORIGO_FORK_PR": (
        "${{ github.event_name == 'pull_request' && "
        "github.event.pull_request.head.repo.full_name != github.repository }}"
    ),
}
NUGET_CACHE = {
    "path": "~/.nuget/packages",
    "key": (
        "nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', "
        "'**/Directory.Build.props', 'Directory.Packages.props', 'global.json') }}"
    ),
    "restore-keys": "nuget-${{ runner.os }}-",
}
GODOT_CACHE = {
    "path": ".godot_binary",
    "key": (
        "godot-${{ runner.os }}-${{ "
        "hashFiles('Origo.GodotAdapter/Origo.GodotAdapter.csproj') }}"
    ),
}
APT_UPDATE = "timeout 300 sudo apt-get -o Acquire::Retries=3 update -qq"
APT_INSTALL_LINT = (
    "timeout 300 sudo apt-get -o Acquire::Retries=3 install -y -qq "
    "shellcheck python3-yaml"
)
APT_INSTALL_GODOT = (
    "timeout 300 sudo apt-get -o Acquire::Retries=3 install -y -qq "
    "libxcursor1 libxinerama1 libxrandr2 libasound2t64 libpulse0 libgl1 "
    "libfontconfig1"
)
VERIFY_RELEASE_TAGS = (
    "set -euo pipefail",
    "if ! git tag --list 'v*' | grep -Eq '^v[0-9]+\\.[0-9]+\\.[0-9]+$'; then",
    'echo "ERROR: no formal release tag is available in this checkout." >&2',
    'echo "Set fetch-depth: 0 so previous-package API validation can run." >&2',
    "exit 1",
    "fi",
)


def _action(
    name: str | None,
    *,
    action: str,
    with_inputs: dict | None = None,
) -> dict:
    return {
        "kind": "action",
        "name": name,
        "action": action,
        "with": with_inputs or {},
    }


def _run(
    name: str,
    lines: tuple[str, ...],
    *,
    shell: str | None = None,
) -> dict:
    return {"kind": "run", "name": name, "lines": lines, "shell": shell}


CHECKOUT = _action(None, action="actions/checkout")
SETUP_DOTNET = _action(
    "Setup .NET", action="actions/setup-dotnet",
    with_inputs={"global-json-file": "global.json"},
)
SETUP_PYTHON = _action(
    "Setup Python", action="actions/setup-python",
    with_inputs={"python-version": "3.12"},
)
CACHE_NUGET = _action(None, action="actions/cache", with_inputs=NUGET_CACHE)
CACHE_GODOT = _action(
    "Cache Godot binary", action="actions/cache", with_inputs=GODOT_CACHE
)
INSTALL_PYTHON_DEPS = _run(
    "Install Python validation dependencies", ("python -m pip install pyyaml",)
)

JOBS = {
    "bash-lint": {
        "runs-on": "ubuntu-latest",
        "timeout-minutes": 15,
        "env": JOB_ENV,
        "steps": (
            CHECKOUT,
            _run("Install shellcheck", (APT_UPDATE, APT_INSTALL_LINT)),
            _run("Lint repository scripts", ("bash scripts/bash/lint-scripts.sh",)),
        ),
    },
    "bash-build-and-test": {
        "runs-on": "${{ matrix.os }}",
        "timeout-minutes": 90,
        "strategy": {
            "matrix": {
                "os": ["ubuntu-latest", "windows-latest", "macos-latest"],
            },
            "fail-fast": False,
        },
        "steps": (
            _action(
                None,
                action="actions/checkout",
                with_inputs={"fetch-depth": 0},
            ),
            SETUP_DOTNET,
            CACHE_NUGET,
            _run("Format check", ("bash scripts/bash/format.sh",)),
            _run(
                "Verify release tags for the API baseline",
                VERIFY_RELEASE_TAGS,
                shell="bash",
            ),
            _run(
                "Shell API baseline gate",
                ("bash scripts/bash/api-inventory.sh",),
            ),
            _run(
                "CI \u2014 build, tests, line coverage gates (>= 90%)",
                ("bash scripts/bash/test.sh",),
            ),
            _run(
                "Benchmarks \u2014 TypedData + subsystem baselines",
                ("bash scripts/bash/benchmark.sh",),
            ),
        ),
    },
    "bash-checks": {
        "runs-on": "ubuntu-latest",
        "timeout-minutes": 15,
        "steps": (
            CHECKOUT,
            _run(
                "Bash/PowerShell development-suite parity",
                ("bash scripts/bash/test-dev-suite.sh",),
            ),
            _run(
                "Bash API-inventory parent regression",
                ("bash scripts/bash/test-api-inventory-parent.sh",),
            ),
        ),
    },
    "powershell-lint": {
        "runs-on": "windows-latest",
        "timeout-minutes": 15,
        "env": JOB_ENV,
        "steps": (
            CHECKOUT,
            SETUP_PYTHON,
            INSTALL_PYTHON_DEPS,
            _run(
                "Lint PowerShell scripts",
                (
                    "powershell -NoProfile -ExecutionPolicy Bypass -File "
                    "scripts\\powershell\\lint-scripts.ps1",
                ),
            ),
        ),
    },
    "powershell-checks": {
        "runs-on": "windows-latest",
        "timeout-minutes": 90,
        "env": JOB_ENV,
        "steps": (
            _action(
                None,
                action="actions/checkout",
                with_inputs={"fetch-depth": 0},
            ),
            SETUP_DOTNET,
            SETUP_PYTHON,
            INSTALL_PYTHON_DEPS,
            CACHE_NUGET,
            CACHE_GODOT,
            _run(
                "Windows PowerShell gates",
                (
                    "powershell -NoProfile -ExecutionPolicy Bypass -File "
                    "scripts\\powershell\\ci.ps1",
                ),
                shell="cmd",
            ),
        ),
    },
    "godot-integration-tests": {
        "runs-on": "${{ matrix.os }}",
        "timeout-minutes": 60,
        "needs": "bash-build-and-test",
        "strategy": {
            "matrix": {"os": ["ubuntu-latest"]},
            "fail-fast": False,
        },
        "steps": (
            CHECKOUT,
            SETUP_DOTNET,
            CACHE_NUGET,
            CACHE_GODOT,
            _run(
                "Install Godot headless dependencies",
                (APT_UPDATE, APT_INSTALL_GODOT),
            ),
            _run(
                "Godot headless integration tests",
                ("bash scripts/bash/godot-test.sh",),
            ),
            _run(
                "Shell-only package consumer smoke",
                ("bash scripts/bash/package-consumer-smoke.sh",),
            ),
        ),
    },
}


class _StrictSafeLoader(yaml.SafeLoader):
    """SafeLoader that rejects duplicate mapping keys instead of dropping them."""

    def construct_mapping(self, node, deep: bool = False):
        if isinstance(node, yaml.MappingNode):
            self.flatten_mapping(node)
        mapping = {}
        for key_node, value_node in node.value:
            key = self.construct_object(key_node, deep=deep)
            try:
                duplicate = key in mapping
            except TypeError as exc:
                raise yaml.constructor.ConstructorError(
                    "while constructing a mapping",
                    node.start_mark,
                    f"found an unhashable key: {exc}",
                    key_node.start_mark,
                ) from exc
            if duplicate:
                raise yaml.constructor.ConstructorError(
                    "while constructing a mapping",
                    node.start_mark,
                    f"found duplicate key {key!r}",
                    key_node.start_mark,
                )
            mapping[key] = self.construct_object(value_node, deep=deep)
        return mapping


def _display_key(key: object) -> str:
    return "on" if key is True else str(key)


def _strip_shell_comment(line: str) -> str:
    """Remove a shell comment from one line, respecting simple quoting."""
    quote = ""
    for index, char in enumerate(line):
        if quote:
            if char == quote:
                quote = ""
            continue
        if char in ('"', "'"):
            quote = char
            continue
        if char == "#" and (index == 0 or line[index - 1].isspace()):
            return line[:index].rstrip()
    return line


def _active_lines(text: str) -> list[str]:
    """Return executable, comment-free shell lines with continuations joined."""
    joined = text.replace("\\\n", " ").replace("\\\r", " ")
    lines: list[str] = []
    for raw_line in joined.splitlines():
        line = _strip_shell_comment(raw_line.strip())
        if line:
            lines.append(line)
    return lines


def event_mapping(workflow: dict) -> dict | None:
    """Return the workflow's ``on:`` mapping despite YAML 1.1 parsing it as True."""
    for key in ("on", True):
        value = workflow.get(key)
        if isinstance(value, dict):
            return value
    return None


def _validate_step(
    step: object, expected: dict, label: str, errors: list[str]
) -> None:
    if not isinstance(step, dict):
        errors.append(f"{label} must be a mapping.")
        return

    reported: set[str] = set()
    for conditional in ("if", "continue-on-error"):
        if conditional in step:
            reported.add(conditional)
            errors.append(
                f"{label} must not set '{conditional}:' because the guard "
                "requires it to run."
            )
    if "working-directory" in step:
        reported.add("working-directory")
        errors.append(f"{label} must not set 'working-directory:'.")
    if "env" in step:
        reported.add("env")
        errors.append(f"{label} must not set step 'env:'.")

    kind = expected["kind"]
    allowed = (
        {"name", "uses", "with"}
        if kind == "action"
        else {"name", "run", "shell"}
    )
    if kind == "action" and "shell" in step:
        reported.add("shell")
        errors.append(f"{label} must not set 'shell:'.")
    if kind == "run":
        if expected["shell"] is None:
            if "shell" in step:
                reported.add("shell")
                errors.append(
                    f"{label} must use the default shell, not "
                    f"shell: {step.get('shell')!r}."
                )
        elif step.get("shell") != expected["shell"]:
            reported.add("shell")
            errors.append(
                f"{label} must use shell: {expected['shell']!r}; found "
                f"{step.get('shell')!r}."
            )

    for key in sorted(set(step) - allowed - reported, key=str):
        errors.append(f"{label} must not define '{key}:'.")

    if kind == "action":
        uses = step.get("uses")
        if not isinstance(uses, str) or not uses:
            errors.append(f"{label} must set a pinned 'uses:' action.")
        elif "${{" in uses:
            errors.append(
                f"{label} must not use a dynamic action reference ({uses!r})."
            )
        else:
            action_name, separator, version = uses.partition("@")
            if not separator or not version:
                errors.append(
                    f"{label} action reference {uses!r} must include an "
                    "@version tag."
                )
            elif action_name != expected["action"]:
                errors.append(
                    f"{label} must use {expected['action']!r}; found {uses!r}."
                )
        if expected["name"] is not None and step.get("name") != expected["name"]:
            errors.append(
                f"{label} must keep its reviewed step name "
                f"{expected['name']!r}."
            )
        with_value = step.get("with", {})
        if expected["with"]:
            if with_value != expected["with"]:
                errors.append(
                    f"{label} must use exactly the reviewed action inputs "
                    f"{expected['with']!r}; found {with_value!r}."
                )
        elif with_value:
            errors.append(
                f"{label} must not add action inputs; none are reviewed."
            )
        return

    if step.get("name") != expected["name"]:
        errors.append(
            f"{label} must keep its reviewed step name {expected['name']!r}."
        )
    actual_lines = _active_lines(str(step.get("run") or ""))
    if actual_lines != list(expected["lines"]):
        errors.append(
            f"{label} must run exactly the reviewed commands "
            f"{list(expected['lines'])!r}; found {actual_lines!r}."
        )


def _validate_job(
    job_name: str, job: object, expected: dict, errors: list[str]
) -> None:
    label = f"ci job '{job_name}'"
    if not isinstance(job, dict):
        errors.append(f"{label} must be a mapping.")
        return

    reported: set[str] = set()
    for conditional in ("if", "continue-on-error"):
        if conditional in job:
            reported.add(conditional)
            errors.append(
                f"{label} must not set '{conditional}:' because the guard "
                "requires its steps to run."
            )
    if "defaults" in job:
        reported.add("defaults")
        errors.append(f"{label} must not set job 'defaults:'.")

    allowed = {"runs-on", "timeout-minutes", "strategy", "env", "needs", "steps"}
    for key in sorted(set(job) - allowed - reported, key=str):
        errors.append(f"{label} must not define '{key}:'.")

    if job.get("runs-on") != expected["runs-on"]:
        errors.append(
            f"{label} must run on {expected['runs-on']!r}; found "
            f"{job.get('runs-on')!r}."
        )
    if job.get("timeout-minutes") != expected["timeout-minutes"]:
        errors.append(
            f"{label} must set timeout-minutes: {expected['timeout-minutes']}; "
            f"found {job.get('timeout-minutes')!r}."
        )

    if "strategy" in expected:
        if job.get("strategy") != expected["strategy"]:
            errors.append(
                f"{label} must use exactly the reviewed matrix "
                f"{expected['strategy']!r}; found {job.get('strategy')!r}."
            )
    elif "strategy" in job:
        errors.append(f"{label} must not define 'strategy:'.")
    if "needs" in expected:
        if job.get("needs") != expected["needs"]:
            errors.append(
                f"{label} must declare needs: {expected['needs']}; found "
                f"{job.get('needs')!r}."
            )
    elif "needs" in job:
        errors.append(f"{label} must not define 'needs:'.")
    if "env" in expected:
        if job.get("env") != expected["env"]:
            errors.append(
                f"{label} must use exactly the reviewed job environment "
                f"{expected['env']!r}; found {job.get('env')!r}."
            )
    elif "env" in job:
        errors.append(f"{label} must not define job 'env:'.")

    steps = job.get("steps")
    if not isinstance(steps, list):
        errors.append(f"{label} must define a 'steps:' list.")
        return
    expected_steps = expected["steps"]
    if len(steps) != len(expected_steps):
        errors.append(
            f"{label} must define exactly {len(expected_steps)} reviewed steps "
            f"in order; found {len(steps)}."
        )
    for index, expected_step in enumerate(expected_steps):
        step_label = (
            f"{label} step {index} "
            f"({expected_step['name'] or expected_step['action']!r})"
        )
        if index >= len(steps):
            errors.append(f"{step_label} is missing.")
            continue
        _validate_step(steps[index], expected_step, step_label, errors)


def _validate_workflow(workflow: dict, errors: list[str]) -> None:
    allowed_top_level = {"name", "on", True, "concurrency", "permissions", "jobs"}
    reported: set[object] = set()
    if "defaults" in workflow:
        reported.add("defaults")
        errors.append("ci workflow must not set top-level 'defaults:'.")
    if "env" in workflow:
        reported.add("env")
        errors.append("ci workflow must not set top-level 'env:'.")
    for key in workflow:
        if key not in allowed_top_level and key not in reported:
            errors.append(
                f"ci workflow must not define top-level "
                f"'{_display_key(key)}:'."
            )

    if workflow.get("permissions") != {"contents": "read"}:
        errors.append(
            "ci workflow must set exactly top-level permissions "
            "{'contents': 'read'}; found "
            f"{workflow.get('permissions')!r}."
        )

    if workflow.get("name") != "CI":
        errors.append(
            f"ci workflow must be named 'CI'; found {workflow.get('name')!r}."
        )

    on_keys = [key for key in ("on", True) if key in workflow]
    if len(on_keys) != 1:
        errors.append(
            "ci workflow must define exactly one 'on:' trigger block."
        )
    triggers = event_mapping(workflow)
    if triggers != EXPECTED_TRIGGERS:
        errors.append(
            "ci workflow must use exactly the reviewed triggers "
            f"{EXPECTED_TRIGGERS!r}; found {triggers!r}."
        )
    if workflow.get("concurrency") != EXPECTED_CONCURRENCY:
        errors.append(
            "ci workflow must use exactly the reviewed concurrency group "
            f"{EXPECTED_CONCURRENCY!r}; found "
            f"{workflow.get('concurrency')!r}."
        )

    jobs = workflow.get("jobs")
    if not isinstance(jobs, dict):
        errors.append("ci workflow must define a 'jobs:' mapping.")
        return
    for job_name in JOBS:
        if job_name not in jobs:
            errors.append(
                f"ci workflow is missing required job '{job_name}'."
            )
    for job_name in jobs:
        if job_name not in JOBS:
            errors.append(
                f"ci workflow must not define job '{job_name}'; the required "
                "job set is pinned."
            )
    for job_name, expected in JOBS.items():
        if job_name in jobs:
            _validate_job(job_name, jobs[job_name], expected, errors)


def main() -> int:
    if not WORKFLOW.is_file():
        print(f"ERROR: ci workflow is missing: {WORKFLOW}", file=sys.stderr)
        return 1
    text = WORKFLOW.read_text(encoding="utf-8")
    try:
        workflow = yaml.load(text, Loader=_StrictSafeLoader)
    except yaml.YAMLError as exc:
        print(f"ERROR: {WORKFLOW} is not valid YAML: {exc}", file=sys.stderr)
        return 1
    if not isinstance(workflow, dict):
        print(
            f"ERROR: {WORKFLOW} must contain a workflow mapping.",
            file=sys.stderr,
        )
        return 1

    errors: list[str] = []
    _validate_workflow(workflow, errors)
    if errors:
        print("CI workflow guard FAILED:", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print("CI workflow guard: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
