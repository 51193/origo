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
from collections.abc import Iterator
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "release.yml"
WEEKLY_WORKFLOW = ROOT / ".github" / "workflows" / "weekly-build.yml"
RELEASE_ACTION = "softprops/action-gh-release"
RESOLVED_TAG = "${{ steps.resolve-tag.outputs.tag }}"
RESOLVED_TAG_REF = f"refs/tags/{RESOLVED_TAG}"
WRITE_BACK_CONDITION = "steps.release-commit.outputs.changed == 'true'"


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
STAMP_MAPPINGS = re.compile(
    r'for element, replacement in \(\s*\("Version", version\),\s*'
    r'\("AssemblyVersion", assembly_version\),\s*'
    r'\("FileVersion", assembly_version\),\s*\)',
    re.S,
)
MATCHING_XML_SUBSTITUTION = re.compile(
    r"""re\.subn\(\s*rf["']\(<\{element\}>\)\[\^<\]\+\(</\{element\}>\)["']"""
)
COMMENT = re.compile(r"(?:^|\s)#[^\n]*", re.M)
SHELL_SEPARATOR = re.compile(r"&&|\|\||[;\n|&]")
GROUPING = "(){}"
EMBEDDED_COMMAND = re.compile(r"\bgit\b[^\n]*\bpush\b")
GIT_PUSH = re.compile(r"\bgit\s+push\b")
GIT_PUSH_CANDIDATE = re.compile(r"\bgit\b.*\bpush\b", re.S)


def strip_comments(script: str) -> str:
    """Drop shell/Python comments so only executable text is inspected."""
    return COMMENT.sub("", script)


def _bare(token: str) -> str:
    """Return a token without shell grouping characters around it."""
    return token.strip(GROUPING)


def _iter_fragment_pushes(fragment: str, depth: int = 0) -> Iterator[list[str]]:
    """Yield the argument list of every `git push` in one shell fragment.

    The argument list holds the tokens after `push` with options removed, so its
    first element names the remote and the remaining elements are refspecs. An
    option that consumes a separate value (`git -C <path> push`, `git --git-dir
    <path> push`, `git -c <name=value> push`) is stepped over before `push` is
    matched, and shell grouping around the command (`(git push ...)`) is
    ignored. A quoted argument that itself contains a command (`bash -c "git
    push ..."`) is scanned recursively, so a push cannot hide inside a wrapper.
    """
    # Most fragments are not push commands; don't ask shlex to interpret the
    # unrelated Python/heredoc and expression syntax used elsewhere in a step.
    # Candidate fragments are still parsed strictly, so malformed push syntax
    # cannot fall back to whitespace tokenization.
    if not GIT_PUSH_CANDIDATE.search(fragment):
        return

    try:
        tokens = shlex.split(fragment)
    except ValueError as exc:
        raise ValueError(f"malformed shell fragment: {exc}") from exc

    index = 0
    while index < len(tokens):
        if _bare(tokens[index]) != "git":
            index += 1
            continue
        cursor = index + 1
        while cursor < len(tokens) and tokens[cursor].startswith("-"):
            takes_value = (
                cursor + 1 < len(tokens)
                and not tokens[cursor + 1].startswith("-")
                and _bare(tokens[cursor + 1]) != "push"
            )
            cursor += 2 if takes_value else 1
        if cursor < len(tokens) and _bare(tokens[cursor]) == "push":
            yield [token for token in tokens[cursor + 1:] if not token.startswith("-")]
            index = cursor + 1
        else:
            index += 1

    if depth >= 2:
        return
    for token in tokens:
        if token == fragment or " " not in token or not EMBEDDED_COMMAND.search(token):
            continue
        yield from _iter_fragment_pushes(token, depth + 1)


def push_arguments(script: str) -> Iterator[list[str]]:
    """Yield the argument list of every `git push` in a step script.

    The script is split on shell separators first, so a branch push is found
    wherever it sits: after `&&`, behind a `bash -c` wrapper, inside a loop
    body, or on its own line.
    """
    normalized_script = re.sub(r"\\\r?\n", " ", script)
    for fragment in SHELL_SEPARATOR.split(normalized_script):
        yield from _iter_fragment_pushes(fragment)


def contains_push_in_command_substitution(script: str) -> bool:
    """Return true when a push occurs in `$()` or backtick command syntax.

    The workflow guard deliberately fails closed on these constructs instead
    of trying to infer their evaluated shell command and refspecs.
    """
    depth = 0
    backtick = False
    single_quoted = False
    double_quoted = False
    index = 0
    while index < len(script):
        if not double_quoted and script[index] == "'":
            single_quoted = not single_quoted
            index += 1
            continue
        if not single_quoted and script[index] == '"':
            double_quoted = not double_quoted
            index += 1
            continue
        if single_quoted:
            index += 1
            continue
        if script[index] == "`":
            backtick = not backtick
            index += 1
            continue
        if not backtick and script.startswith("$(", index):
            depth += 1
            index += 2
            continue
        if depth and script[index] == "(":
            depth += 1
        elif depth and script[index] == ")":
            depth -= 1
        if (depth or backtick) and GIT_PUSH.match(script, index):
            return True
        index += 1
    return False


def unsafe_push_reasons(arguments: list[str]) -> list[str]:
    """Describe why a push is unsafe for a tag-only snapshot publish.

    An empty result means the push publishes only tags: the first argument is
    the remote, and every refspec after it must name `refs/tags/` on both its
    source and its destination.
    """
    if len(arguments) < 2:
        return ["runs a `git push` without an explicit tag refspec"]
    return [
        f"pushes {refspec!r}, which is not a tag-only refspec"
        for refspec in arguments[1:]
        if any(not part.startswith("refs/tags/") for part in refspec.split(":"))
    ]


def check_weekly_snapshot(errors: list[str], path: Path | None = None) -> None:
    """A snapshot must publish only its tag, and that tag must be self-contained.

    The snapshot commit carries the version stamps of the tag it publishes, so
    the release pipeline has nothing to rewrite and never pushes `main`. No
    step of this workflow may therefore push anything but tags: every `git
    push` is located in the executable text of every step, wherever it sits in
    the shell (compound command, wrapper, loop, or its own line), and each of
    its refspecs must name `refs/tags/` on both source and destination.

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

    try:
        workflow = yaml.safe_load(text)
    except yaml.YAMLError as exc:
        errors.append(f"{relative} is not valid YAML: {exc}")
        return

    steps = [
        (
            str(step.get("name") or f"job step {index}"),
            strip_comments(str(step.get("run") or "")),
        )
        for job in (workflow.get("jobs") or {}).values()
        if isinstance(job, dict)
        for index, step in enumerate(job.get("steps") or [])
        if isinstance(step, dict)
    ]

    for name, script in steps:
        if contains_push_in_command_substitution(script):
            errors.append(
                f"{relative} step {name!r} contains a git push inside command "
                "substitution; the guard refuses to infer its evaluated "
                "refspecs. Put the push in a directly validated shell command."
            )
        try:
            pushes = list(push_arguments(script))
        except ValueError as exc:
            errors.append(
                f"{relative} step {name!r} contains malformed shell; refusing "
                f"to validate git pushes ({exc})."
            )
            continue
        for arguments in pushes:
            for reason in unsafe_push_reasons(arguments):
                errors.append(
                    f"{relative} step {name!r} {reason}; every push in this "
                    "workflow must name refs/tags/ on both its source and its "
                    "destination so a snapshot never has to bypass branch rules."
                )

    scripts = [script for _, script in steps]

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
        if (
            not STAMP_MAPPINGS.search(script)
            or not MATCHING_XML_SUBSTITUTION.search(script)
            or "if count != 1:" not in script
        ):
            errors.append(
                f"{relative} must substitute Version, AssemblyVersion, and "
                "FileVersion into their matching XML elements and require one "
                "match for each, so the tagged commit already matches its tag."
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
    """The release write-back may only run when the version stamps changed.

    The write-back is the only place the release pipeline writes `main`, and it
    may only run for a release whose tag did not already carry the version
    stamps. Every release step that pushes anything other than tags must
    therefore stay behind the write-back condition. The step is identified by
    the push it performs rather than by a refspec spelling, so the check keeps
    working when the destination is written as `:main`, when a new option is
    added, or when the push moves to another step.
    """
    release_job = (workflow.get("jobs") or {}).get("release")
    steps = release_job.get("steps") if isinstance(release_job, dict) else None
    if not isinstance(steps, list):
        return

    for index, step in enumerate(steps):
        if not isinstance(step, dict):
            continue
        script = strip_comments(str(step.get("run") or ""))
        if not any(unsafe_push_reasons(arguments) for arguments in push_arguments(script)):
            continue
        condition = str(step.get("if") or "")
        normalized_condition = condition.strip()
        if normalized_condition.startswith("${{") and normalized_condition.endswith("}}"):
            normalized_condition = normalized_condition[3:-2].strip()
        if normalized_condition != WRITE_BACK_CONDITION:
            errors.append(
                f"release step {index} ({str(step.get('name') or 'unnamed')!r}) "
                "pushes a branch and its condition must be exactly "
                f"{WRITE_BACK_CONDITION!r}, so no other expression can make "
                "the version write-back unconditional."
            )


if __name__ == "__main__":
    raise SystemExit(main())
