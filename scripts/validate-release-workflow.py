#!/usr/bin/env python3
"""Guard the release workflow's dispatch-safe tag, action, version-stamp,
and Boolean contract.

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

The release-creation rule is a run-text tripwire, not a security boundary: it
rejects literal /releases, release create, and createRelease occurrences in a
release-job run step, with an exact "# guard: read-only" waiver on the
immediately preceding line for genuinely read-only calls. Scripts, tag trees,
env/with values, variables, heredocs, and quoted or dynamically constructed
commands are outside its reach.

The weekly-snapshot rule is structural for its bump step: every non-comment
line must be one of the known version-stamp/tag commands, and the only allowed
push is the exact tag refspec ``git push origin "refs/tags/${TAG}"``. A branch
push, a second push, an unapproved action, or an obfuscated command therefore
fails the guard. This is a regression tripwire, not a security boundary.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "release.yml"
WEEKLY_WORKFLOW = ROOT / ".github" / "workflows" / "weekly-build.yml"
WEEKLY_BUMP_STEP = "Bump version, commit, and tag"
WEEKLY_TAG_PUSH = 'git push origin "refs/tags/${TAG}"'
WEEKLY_TAG_ASSIGNMENT = 'TAG="${{ steps.check.outputs.tag }}"'
# The weekly bump step may only run these commands. A new command needs a
# deliberate review and an entry here; in particular, a branch push is absent.
WEEKLY_BUMP_PREFIXES = (
    "python3 scripts/write-version-stamp.py ",
    "git config ",
    "git add ",
    "git commit -m ",
    "git tag -a ",
)
# A command separator could append a hidden push to an allowed prefix.
WEEKLY_LINE_SEPARATORS = (";", "&&", "||", "|", "&")
WEEKLY_SUBSTITUTIONS = ("$(", "`", "<(")
# Only this action may run in the weekly workflow; a new action could move refs
# outside the inspected shell text.
WEEKLY_ALLOWED_ACTIONS = {"actions/checkout"}
RELEASE_ACTION = "softprops/action-gh-release"
RESOLVED_TAG = "${{ steps.resolve-tag.outputs.tag }}"
RESOLVED_TAG_REF = f"refs/tags/{RESOLVED_TAG}"
# Only these actions may run in the publishing job; a new action needs a
# deliberate review and an entry here.
RELEASE_JOB_ACTIONS = {
    "actions/checkout",
    "actions/setup-dotnet",
    "actions/cache",
    RELEASE_ACTION,
}


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)


def _normalize_command(command: str) -> str:
    """Collapse shell line continuations and whitespace for keyword scans."""
    return " ".join(command.replace("\\\n", " ").replace("\\\r", " ").split())




def _references_wrong_ref(text: str) -> bool:
    return re.search(r"\$\{\{\s*github\.(?:ref_name|head_ref)\s*\}\}", text) is not None


def _env_references_wrong_ref(env: object) -> bool:
    if not isinstance(env, dict):
        return False
    return any(_references_wrong_ref(str(value)) for value in env.values())


def _calls_version_helper(run: str) -> bool:
    """True when a non-comment line really invokes the helper."""
    pattern = re.compile(
        r"^(?:sudo\s+(?:-\S+\s+)*)?(?:env\s+)?"
        r"(?:[A-Za-z_][A-Za-z0-9_]*=\S+\s+)*"
        r"python3\s+(?:-\S+\s+)*(?:\./|\"\$PWD/|\$PWD/)?"
        r"scripts/write-version-stamp\.py(?:\"|\s|$)"
    )
    for line in run.splitlines():
        stripped = line.strip()
        if stripped.startswith("#"):
            continue
        if pattern.match(stripped):
            return True
    return False


def _release_creation_line_conflicts(run: str) -> list[int]:
    """Return offending line numbers where a run step touches releases.

    Only literal run text is scanned; a read-only call is waived by an exact
    ``# guard: read-only`` line immediately above it. This intentionally avoids
    parsing shell syntax.
    """
    offenders: list[int] = []
    read_only = False
    joined = run.replace("\\\n", "").replace("\\\r", "")
    for number, raw_line in enumerate(joined.splitlines(), start=1):
        stripped = raw_line.strip()
        if stripped.startswith("#"):
            read_only = stripped == "# guard: read-only"
            continue
        lowered = _normalize_command(stripped).lower()
        is_creation = "release create" in lowered or "createrelease" in lowered
        writes = bool(
            re.search(
                r"(?:^|\s)(?:-f|--raw-field|-F|--field|--input|-d|--data|--json|"
                r"--post-data|-X\s+POST|--method\s+POST)\b",
                lowered,
            )
        )
        if is_creation or ("/releases" in lowered and not (read_only and not writes)):
            offenders.append(number)
        read_only = False
    return offenders


def _weekly_snapshot_errors(workflow: dict) -> list[str]:
    """Validate the weekly bump step's tag-only push and action allowlist."""
    errors: list[str] = []
    bump_seen = False
    tag_pushes = 0
    for job_name, job in (workflow.get("jobs") or {}).items():
        if not isinstance(job, dict):
            continue
        if job.get("uses"):
            errors.append(
                f"weekly job '{job_name}' is a reusable workflow; only the "
                "reviewed inline weekly job is allowed."
            )
        for index, step in enumerate(job.get("steps") or []):
            if not isinstance(step, dict):
                continue
            uses_text = str(step.get("uses") or "")
            if uses_text:
                action = uses_text.split("@", 1)[0]
                if action not in WEEKLY_ALLOWED_ACTIONS:
                    errors.append(
                        f"weekly {job_name}.steps[{index}] uses unapproved "
                        f"action '{action}'; only actions/checkout may run."
                    )
            run = str(step.get("run") or "")
            if not run:
                continue
            joined = run.replace("\\\n", "").replace("\\\r", "")
            if step.get("name") == WEEKLY_BUMP_STEP:
                bump_seen = True
                for number, raw_line in enumerate(joined.splitlines(), start=1):
                    line = raw_line.strip()
                    if not line or line.startswith("#"):
                        continue
                    if line == WEEKLY_TAG_PUSH:
                        tag_pushes += 1
                        continue
                    if line == WEEKLY_TAG_ASSIGNMENT:
                        continue
                    if any(separator in line for separator in WEEKLY_LINE_SEPARATORS):
                        errors.append(
                            f"weekly bump step line {number} contains a shell "
                            f"separator and can hide a second command: {line!r}"
                        )
                        continue
                    if any(token in line for token in WEEKLY_SUBSTITUTIONS):
                        errors.append(
                            f"weekly bump step line {number} contains command "
                            f"or process substitution: {line!r}"
                        )
                        continue
                    if line.startswith(WEEKLY_BUMP_PREFIXES):
                        continue
                    errors.append(
                        f"weekly bump step line {number} is not an allowed "
                        f"command: {line!r}; only the tag push "
                        f"{WEEKLY_TAG_PUSH!r} may move a ref."
                    )
            else:
                for number, raw_line in enumerate(joined.splitlines(), start=1):
                    line = raw_line.strip()
                    if not line or line.startswith("#"):
                        continue
                    if "push" in line.lower():
                        errors.append(
                            f"weekly step '{step.get('name', index)}' line "
                            f"{number} pushes outside the bump step: {line!r}"
                        )
    if not bump_seen:
        errors.append(
            f"weekly workflow must define the '{WEEKLY_BUMP_STEP}' step."
        )
    if tag_pushes != 1:
        errors.append(
            "weekly bump step must contain exactly one tag push "
            f"{WEEKLY_TAG_PUSH!r}; found {tag_pushes}."
        )
    return errors


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

    if not WEEKLY_WORKFLOW.is_file():
        fail(f"weekly workflow is missing: {WEEKLY_WORKFLOW.relative_to(ROOT)}")
        return 1
    weekly_text = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
    try:
        weekly_workflow = yaml.safe_load(weekly_text)
    except yaml.YAMLError as exc:
        fail(f"{WEEKLY_WORKFLOW.relative_to(ROOT)} is not valid YAML: {exc}")
        return 1
    if not isinstance(weekly_workflow, dict):
        fail(f"{WEEKLY_WORKFLOW.relative_to(ROOT)} must contain a workflow mapping.")
        return 1

    errors: list[str] = []
    errors.extend(_weekly_snapshot_errors(weekly_workflow))

    for job_name, job in (workflow.get("jobs") or {}).items():
        if not isinstance(job, dict):
            continue
        for index, step in enumerate(job.get("steps") or []):
            if not isinstance(step, dict):
                continue
            uses_value = step.get("uses")
            uses_text = str(uses_value) if uses_value is not None else ""
            if "${{" in uses_text:
                errors.append(
                    f"{job_name}.steps[{index}] uses a dynamic action reference "
                    f"('{uses_text}'); pin the action so this guard can review it."
                )
            action = uses_text.split("@", 1)[0]
            if job_name == "release" and action and action not in RELEASE_JOB_ACTIONS:
                errors.append(
                    f"{job_name}.steps[{index}] uses unapproved action '{action}'; "
                    "add it to RELEASE_JOB_ACTIONS only after review."
                )
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
        version_step = next(
            (
                step
                for step in release_steps
                if isinstance(step, dict)
                and _calls_version_helper(str(step.get("run") or ""))
            ),
            None,
        )
        if version_step is None:
            errors.append(
                "release job must call scripts/write-version-stamp.py so the "
                "release and weekly workflows share one validated implementation."
            )
        else:
            version_run = str(version_step.get("run") or "")
            version_env = version_step.get("env")
            if not isinstance(version_env, dict) or version_env.get("TAG") != RESOLVED_TAG:
                errors.append(
                    "the version-stamp step must receive "
                    f"TAG={RESOLVED_TAG} so it rewrites the dispatched tag, "
                    "not github.ref_name."
                )
            if _references_wrong_ref(version_run) or _env_references_wrong_ref(version_env):
                errors.append(
                    "the version-stamp step must not read github.ref_name or "
                    "github.head_ref; use the resolved tag."
                )

        for index, step in enumerate(release_steps):
            if not isinstance(step, dict):
                continue
            offenders = _release_creation_line_conflicts(str(step.get("run") or ""))
            if offenders:
                errors.append(
                    f"release.steps[{index}] touches releases in a run step "
                    f"(lines {offenders}); create the release through "
                    f"{RELEASE_ACTION} and mark a genuinely read-only /releases "
                    "call with a preceding '# guard: read-only' line."
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
            "Release and weekly-snapshot workflow guard FAILED:",
            file=sys.stderr,
        )
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print("Release-workflow guard: OK")
    print("Weekly-snapshot guard: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
