#!/usr/bin/env python3
"""Guard the release workflow's dispatch-safe tag and Boolean-input contract.

`workflow_dispatch` runs from `main`, so `github.ref_name` is the branch name
rather than the requested release tag. The workflow resolves the real tag into
`steps.resolve-tag.outputs.tag`; release creation must pass that output to
`softprops/action-gh-release` explicitly instead of relying on the action's
`github.ref_name` default.

A `workflow_dispatch` input declared as `type: boolean` is a Boolean value in
the `inputs` context. GitHub performs loose equality by coercing mismatched
types to numbers, so `inputs.<name> == 'true'` is false; Boolean inputs must be
tested directly.

Run from `scripts/lint-scripts.sh` so a release run cannot reach the package
build before these invariants fail CI.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "release.yml"
RELEASE_ACTION = "softprops/action-gh-release"
RESOLVED_TAG = "${{ steps.resolve-tag.outputs.tag }}"


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)


def event_mapping(workflow: dict) -> dict:
    """Return the workflow's `on:` mapping despite YAML 1.1 parsing `on` as True."""
    for key in ("on", True):
        value = workflow.get(key)
        if isinstance(value, dict):
            return value
    return {}


def main() -> int:
    if not WORKFLOW.is_file():
        fail(f"release workflow is missing: {WORKFLOW.relative_to(ROOT)}")
        return 1

    text = WORKFLOW.read_text(encoding="utf-8")
    try:
        workflow = yaml.safe_load(text)
    except yaml.YAMLError as exc:
        fail(f"{WORKFLOW.relative_to(ROOT)} is not valid YAML: {exc}")
        return 1

    if not isinstance(workflow, dict):
        fail(f"{WORKFLOW.relative_to(ROOT)} must contain a workflow mapping.")
        return 1

    errors: list[str] = []

    for job_name, job in (workflow.get("jobs") or {}).items():
        if not isinstance(job, dict):
            continue
        for index, step in enumerate(job.get("steps") or []):
            if not isinstance(step, dict):
                continue
            action = str(step.get("uses") or "").split("@", 1)[0]
            if action != RELEASE_ACTION:
                continue
            with_section = step.get("with")
            tag_name = with_section.get("tag_name") if isinstance(with_section, dict) else None
            if tag_name != RESOLVED_TAG:
                errors.append(
                    f"{job_name}.steps[{index}] must set "
                    f"{RELEASE_ACTION} with.tag_name to {RESOLVED_TAG}; "
                    "the action default is github.ref_name, which is main "
                    "for a workflow_dispatch run."
                )

    release_job = (workflow.get("jobs") or {}).get("release")
    release_steps = release_job.get("steps") if isinstance(release_job, dict) else None
    if not isinstance(release_steps, list):
        errors.append("release workflow must define release job steps.")
    else:
        run_commands = [
            str(step.get("run") or "")
            for step in release_steps
            if isinstance(step, dict)
        ]
        if not any("bash scripts/lint-scripts.sh" in command for command in run_commands):
            errors.append(
                "release job must run bash scripts/lint-scripts.sh so the tag "\
                "pipeline enforces script, workflow, and instruction guards."
            )
        for dependency in ("shellcheck", "python3-yaml"):
            if not any(dependency in command for command in run_commands):
                errors.append(
                    f"release job must install script-lint dependency "\
                    f"'{dependency}' before running scripts/lint-scripts.sh."
                )

        api_step = next(
            (
                step
                for step in release_steps
                if isinstance(step, dict)
                and "bash scripts/api-inventory.sh" in str(step.get("run") or "")
            ),
            None,
        )
        if api_step is None:
            errors.append(
                "release job must run bash scripts/api-inventory.sh so the tag "
                "pipeline enforces the shell API baseline."
            )
        else:
            api_env = api_step.get("env")
            current_release_tag = (
                api_env.get("ORIGO_CURRENT_RELEASE_TAG")
                if isinstance(api_env, dict)
                else None
            )
            if current_release_tag != RESOLVED_TAG:
                errors.append(
                    "release job must set ORIGO_CURRENT_RELEASE_TAG to "
                    f"{RESOLVED_TAG} on the shell API baseline step so a "
                    "version-sync commit does not make the current release "
                    "tag its own previous baseline."
                )

    dispatch = event_mapping(workflow).get("workflow_dispatch")
    inputs = dispatch.get("inputs") if isinstance(dispatch, dict) else None
    if isinstance(inputs, dict):
        for name, spec in inputs.items():
            if not isinstance(spec, dict) or str(spec.get("type", "")).lower() != "boolean":
                continue
            pattern = re.compile(
                rf"if:[^\n]*(?<![\w.])inputs\.{re.escape(name)}\b[^\n]*"
                rf"==\s*['\"](?:true|false)['\"]",
                re.IGNORECASE,
            )
            if pattern.search(text):
                errors.append(
                    f"Boolean input 'inputs.{name}' is compared with a quoted "
                    "string in an if: expression. GitHub coerces mismatched "
                    "operands to numbers, so the comparison is always false; "
                    "test the Boolean input directly."
                )

    if errors:
        print(
            f"Release-workflow guard FAILED ({WORKFLOW.relative_to(ROOT)}):",
            file=sys.stderr,
        )
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print("Release-workflow guard: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
