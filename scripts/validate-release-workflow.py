#!/usr/bin/env python3
"""Guard the release and weekly-snapshot workflow contracts.

`workflow_dispatch` runs from `main`, so `github.ref_name` is the branch name
rather than the requested release tag. The workflow resolves the real tag into
`steps.resolve-tag.outputs.tag`; release creation must pass that output to
`softprops/action-gh-release` explicitly instead of relying on the action's
`github.ref_name` default.

A `workflow_dispatch` input declared as `type: boolean` is a Boolean value in
the `inputs` context. GitHub performs loose equality by coercing mismatched
types to numbers, so `inputs.<name> == 'true'` is false; Boolean inputs must be
tested directly.

A weekly snapshot must be publishable without writing to `main`: the snapshot
commit already carries the version stamps of the tag it publishes, so the
release pipeline finds nothing to rewrite and never creates a release commit.
Branch rules that require a pull request therefore cannot block a snapshot. If
the snapshot workflow ever pushes a branch again, or the write-back loses its
condition, every weekly run starts failing against those rules instead.

Run from `scripts/lint-scripts.sh` so a release run cannot reach the package
build before these invariants fail CI.
"""

from __future__ import annotations

import re
import shlex
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "release.yml"
WEEKLY_WORKFLOW = ROOT / ".github" / "workflows" / "weekly-build.yml"
RELEASE_ACTION = "softprops/action-gh-release"
RESOLVED_TAG = "${{ steps.resolve-tag.outputs.tag }}"
RESOLVED_TAG_REF = f"refs/tags/{RESOLVED_TAG}"
WRITE_BACK_CONDITION = "changed == 'true'"


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
        else:
            lint_step = next(
                (
                    step
                    for step in release_steps
                    if isinstance(step, dict)
                    and "bash scripts/lint-scripts.sh" in str(step.get("run") or "")
                ),
                None,
            )
            lint_env = lint_step.get("env") if isinstance(lint_step, dict) else None
            if not isinstance(lint_env, dict) or lint_env.get("ORIGO_BRANCH_NAME") != RESOLVED_TAG_REF:
                errors.append(
                    "release job must pass ORIGO_BRANCH_NAME as "
                    f"{RESOLVED_TAG_REF} when linting a detached release checkout."
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

    check_weekly_snapshot(errors)
    check_release_writeback_is_conditional(workflow, errors)

    if errors:
        print(
            "Release and weekly-snapshot workflow guard FAILED:",
            file=sys.stderr,
        )
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print("Release/weekly-workflow guard: OK")
    return 0


SUBSTITUTION = re.compile(r"\bre\.subn?\(|\.replace\(|\bsubn?\(")
BUMP_TARGET = re.compile(r"""["']Directory\.Build\.props["']""")
WRITE_OP = re.compile(r"""open\([^)]*["'][wa]|write_text\(|\.write\(""")
COMMENT = re.compile(r"(?:^|\s)#[^\n]*", re.M)


def strip_comments(script: str) -> str:
    """Drop shell/Python comments so only executable text is inspected."""
    return COMMENT.sub("", script)


def push_destinations(command: str) -> list[str]:
    """Return the refspec arguments of a `git push` command line.

    The first non-option argument names the remote; every argument after it is a
    refspec whose source and destination must both name a tag.
    """
    try:
        tokens = shlex.split(command)
    except ValueError:
        return [command]
    arguments = [token for token in tokens[2:] if not token.startswith("-")]
    return arguments[1:] if arguments else []


def check_weekly_snapshot(errors: list[str], path: Path | None = None) -> None:
    """A snapshot must publish only its tag, and that tag must be self-contained.

    The snapshot commit carries the version stamps of the tag it publishes, so
    the release pipeline has nothing to rewrite and never pushes `main`. Every
    refspec of every `git push` in this workflow must therefore name a tag on
    both its source and its destination.

    The stamp rewriting is checked on the executable text of the single step
    that rewrites `Directory.Build.props`: comments are stripped first, that
    step has to perform a substitution, and all three element names have to
    appear in it as quoted literals. Naming the stamps anywhere else in the
    workflow does not make the tagged commit match its tag.
    """
    workflow_path = path or WEEKLY_WORKFLOW
    if not workflow_path.is_file():
        errors.append(f"weekly snapshot workflow is missing: {workflow_path}")
        return

    try:
        relative = workflow_path.relative_to(ROOT)
    except ValueError:
        relative = workflow_path

    text = workflow_path.read_text(encoding="utf-8")

    for index, line in enumerate(text.splitlines(), 1):
        command = line.strip()
        if not command.startswith("git push"):
            continue
        destinations = push_destinations(command)
        if not destinations:
            errors.append(
                f"{relative}:{index} runs a `git push` without an explicit tag "
                f"refspec ({command!r}); a snapshot must publish only its tag."
            )
            continue
        for refspec in destinations:
            if any(not part.startswith("refs/tags/") for part in refspec.split(":")):
                errors.append(
                    f"{relative}:{index} pushes {refspec!r}, which is not a "
                    f"tag-only refspec ({command!r}); every source and "
                    "destination must name refs/tags/ so a snapshot never has "
                    "to bypass branch rules."
                )

    try:
        workflow = yaml.safe_load(text)
    except yaml.YAMLError as exc:
        errors.append(f"{relative} is not valid YAML: {exc}")
        return

    scripts = [
        strip_comments(str(step.get("run") or ""))
        for job in (workflow.get("jobs") or {}).values()
        if isinstance(job, dict)
        for step in job.get("steps") or []
        if isinstance(step, dict)
    ]

    bump_scripts = [
        script
        for script in scripts
        if BUMP_TARGET.search(script) and WRITE_OP.search(script)
    ]
    if len(bump_scripts) != 1:
        errors.append(
            f"{relative} must rewrite Directory.Build.props in exactly one "
            f"step script; found {len(bump_scripts)}. The version stamps have "
            "to be written, not merely named."
        )
    for script in bump_scripts:
        if not SUBSTITUTION.search(script):
            errors.append(
                f"{relative} writes Directory.Build.props without substituting "
                "the version elements, so the tagged commit would not match "
                "its tag."
            )
        for stamp in ("Version", "AssemblyVersion", "FileVersion"):
            if not re.search(rf"""["']{stamp}["']""", script):
                errors.append(
                    f"{relative} must replace the {stamp} element inside the "
                    "step that rewrites Directory.Build.props, so the tagged "
                    "commit already matches its tag and the release pipeline "
                    "has nothing to write back."
                )

    dispatch_scripts = [
        script for script in scripts if "gh workflow run release.yml" in script
    ]
    if len(dispatch_scripts) != 1:
        errors.append(
            f"{relative} must dispatch release.yml from exactly one step "
            f"script; found {len(dispatch_scripts)}. A tag pushed with "
            "GITHUB_TOKEN does not trigger the push workflow, so the dispatch "
            "has to be executed."
        )
    for script in dispatch_scripts:
        for argument in ("-f tag=", "-f snapshot=true"):
            if argument not in script:
                errors.append(
                    f"{relative} must dispatch release.yml with {argument!r} in "
                    "the step that runs it."
                )


def check_release_writeback_is_conditional(workflow: dict, errors: list[str]) -> None:
    """The release write-back may only run when the version stamps changed."""
    release_job = (workflow.get("jobs") or {}).get("release")
    steps = release_job.get("steps") if isinstance(release_job, dict) else None
    if not isinstance(steps, list):
        return

    for step in steps:
        if not isinstance(step, dict):
            continue
        command = str(step.get("run") or "")
        if "git push" not in command or "refs/heads/main" not in command:
            continue
        condition = str(step.get("if") or "")
        if WRITE_BACK_CONDITION not in condition:
            errors.append(
                "the release write-back step must stay guarded by "
                f"{WRITE_BACK_CONDITION!r}, so a snapshot whose tag already "
                "carries the version stamps never pushes main."
            )


if __name__ == "__main__":
    raise SystemExit(main())
