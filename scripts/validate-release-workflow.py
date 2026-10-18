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

The release-creation scan is a tripwire for the reviewed workflow shape, not a
sandbox: commands assembled through variables or heredocs are out of reach.
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


_WRONG_REF = re.compile(r"\$\{\{\s*github\.(?:ref_name|head_ref)\s*\}\}")
_VERSION_CALL = re.compile(
    r"^python3\s+(?:-\S+\s+)*scripts/write-version-stamp\.py(?:\s|$)"
)
_RELEASE_CREATE = re.compile(r"\bgh\s+release\s+create\b")
_API_POST_FLAGS = re.compile(r"(?:^|\s)(?:-f|--raw-field|-F|--field|--input)\b")
_TAG_EXPR = r"\$\{\{\s*steps\.resolve-tag\.outputs\.tag\s*\}\}"
_VALUE_FLAGS = {"--repo", "-R", "--notes", "--notes-file", "--title", "--target",
                "--discussion-category"}


def _references_wrong_ref(text: str) -> bool:
    return _WRONG_REF.search(text) is not None


def _env_references_wrong_ref(env: object) -> bool:
    if not isinstance(env, dict):
        return False
    return any(_references_wrong_ref(str(value)) for value in env.values())


def _calls_version_helper(run: str) -> bool:
    """True when a non-comment line really invokes the helper."""
    pattern = re.compile(
        r"^(?:sudo\s+)?(?:env\s+(?:[A-Za-z_][A-Za-z0-9_]*=\S+\s+)*)?"
        r"python3\s+(?:-\S+\s+)*(?:\./|\$PWD/)?scripts/write-version-stamp\.py(?:\s|$)"
    )
    for line in run.splitlines():
        stripped = line.strip()
        if stripped.startswith("#"):
            continue
        if pattern.match(stripped):
            return True
    return False


def _command_segments(run: str) -> list[str]:
    """Split a step into the commands the shell would run separately."""
    text = run.replace("\\\n", " ").replace("\\\r", " ")
    segments: list[str] = []
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        for part in re.split(r"&&|\|\||;", stripped):
            part = _normalize_command(part)
            if part:
                segments.append(part)
    return segments


def _segment_creates_release(segment: str) -> bool:
    """Detect release creation within one command segment."""
    lowered = segment.lower()
    if _RELEASE_CREATE.search(segment) or "createrelease" in lowered:
        return True
    if "/releases" not in lowered:
        return False
    if re.search(r"(?:-x|--request|-X|--method)\s+(?:get|put|patch)\b", segment, re.IGNORECASE):
        return False
    if re.search(r"(?:-x|--request|-X|--method)\s+post\b", segment, re.IGNORECASE):
        return True
    if re.match(r"gh\s+api\b", segment) and _API_POST_FLAGS.search(segment):
        return True
    if re.match(r"curl\b", segment) and re.search(r"(?:^|\s)(?:-d|--data|--json)\b", segment):
        return True
    if re.search(r"(?:^|\s)--post-(?:data|file)\b", segment):
        return True
    if re.match(r"https?\s+post\b", segment, re.IGNORECASE):
        return True
    return False


def _tag_token_is_resolved(token: str, env: object) -> bool:
    token = token.strip("\"'")
    if re.fullmatch(_TAG_EXPR, token):
        return True
    match = re.fullmatch(r"\$\{?([A-Za-z_][A-Za-z0-9_]*)\}?", token)
    if match and isinstance(env, dict) and env.get(match.group(1)) == RESOLVED_TAG:
        return True
    return False


def _release_tag_argument_ok(segment: str, env: object) -> bool:
    match = _RELEASE_CREATE.search(segment)
    if not match:
        return False
    tokens = segment[match.end():].split()
    index = 0
    while index < len(tokens) and tokens[index].startswith("-"):
        index += 2 if tokens[index] in _VALUE_FLAGS else 1
    if index >= len(tokens):
        return False
    rest = " ".join(tokens[index:])
    if re.match(r"[\"']?" + _TAG_EXPR + r"(?:\"|\'|\s|$)", rest):
        return True
    return _tag_token_is_resolved(tokens[index], env)


def _release_tag_name_ok(segment: str, env: object) -> bool:
    field = re.search(r"tag_name[\"']?\s*[=:]\s*[\"']?", segment)
    if not field:
        return False
    rest = segment[field.end():]
    if re.match(_TAG_EXPR, rest):
        return True
    if isinstance(env, dict):
        match = re.match(r"\$\{?([A-Za-z_][A-Za-z0-9_]*)\}?", rest)
        if match and env.get(match.group(1)) == RESOLVED_TAG:
            return True
    return False


def _release_uses_resolved_tag(segment: str, env: object) -> bool:
    return _release_tag_argument_ok(segment, env) or _release_tag_name_ok(segment, env)


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
            segments = [
                segment
                for segment in _command_segments(str(step.get("run") or ""))
                if _segment_creates_release(segment)
            ]
            if not segments:
                continue
            env = step.get("env")
            if _env_references_wrong_ref(env):
                errors.append(
                    f"release.steps[{index}] creates a release from github.ref_name/"
                    "github.head_ref; pass the resolved tag instead."
                )
                continue
            for segment in segments:
                if _references_wrong_ref(segment):
                    errors.append(
                        f"release.steps[{index}] creates a release from "
                        "github.ref_name/github.head_ref; pass the resolved tag "
                        "instead."
                    )
                elif not _release_uses_resolved_tag(segment, env):
                    errors.append(
                        f"release.steps[{index}] creates a release without the "
                        "resolved tag as its tag_name/first argument; pass "
                        f"{RESOLVED_TAG} explicitly."
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
