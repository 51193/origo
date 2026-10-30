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

Run from `scripts/bash/lint-scripts.sh` so a release run cannot reach the package
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

The release rule applies the same tag-only invariant to the publishing job:
every non-comment line containing a push token must be the exact reviewed
tag-only command (no branch refspec, extra refspec, prefix command, or plain
push), requires exactly one such push, requires the provenance and source-archive
validators in order before it, and pins the release action's target_commitish so
a missing tag can never be created at the default branch.

Required-execution rules are structural: a required command only counts when a
non-comment shell line actually runs it. `echo`, `true`, comments, and
`<command> || true` masking therefore cannot satisfy a rule. The release job
must stage the trusted validators with the exact reviewed
`git show refs/remotes/origin/main:... > ...` lines; any tag-tree copy into
`.release-validators/` is rejected. The weekly completeness assertion must be a
code line starting with `[[` and ending with `]]`; comment-, `|| true`- or
`true`-stamped marker text is rejected.

Required-execution lines must also be plain: a required step must use the
default/Bash shell, must not set `if:` or `continue-on-error:`, must not hide
commands in a heredoc, an always-false branch, a nested control-flow block, a
function definition, or a chained `&&`/`||` line, and a matched command line must
not contain unquoted control operators or command substitution. Writes into `.release-validators/` are only allowed through the
exact reviewed staging lines; copy, extraction, shell `-c`, and redirect forms
are rejected. Job-level `if:`/`continue-on-error:` is rejected on the release
job and on weekly jobs that carry required steps. Required trusted-validator
invocations must be exactly `bash <reviewed path>` (no shell flags, `sh`,
`source`, or `.`), and reads of `.release-validators/` may not move the working
directory into it or reference it through variable indirection. Weekly marker and
completeness checks ignore heredoc bodies and require the completeness assertion
before any early `exit 0`. The release and weekly completion steps pin their
required gate sets, ordering, step conditions, and the artifact validator argv. Like the rest
of the guard this is a regression tripwire over run text, not a security
boundary: scripts, tag trees, environment indirection, and dynamically
constructed commands remain outside its reach.
"""

from __future__ import annotations

import re
import shlex
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github" / "workflows" / "release.yml"
WEEKLY_WORKFLOW = ROOT / ".github" / "workflows" / "weekly-build.yml"
WEEKLY_CHECK_STEP = "Check for commits in the weekly window"
WEEKLY_DISPATCH_STEP = "Dispatch the release pipeline for the snapshot tag"
WEEKLY_BUMP_STEP = "Bump version, commit, and tag"
WEEKLY_TAG_PUSH = 'git push origin "refs/tags/${TAG}"'
WEEKLY_TAG_ASSIGNMENT = 'TAG="${{ steps.check.outputs.tag }}"'
# The weekly bump step may only run these commands. A new command needs a
# deliberate review and an entry here; in particular, a branch push is absent.
WEEKLY_BUMP_CONDITION = "steps.check.outputs.new_version != ''"
WEEKLY_DISPATCH_CONDITION = (
    "steps.check.outputs.new_version != '' || "
    "steps.check.outputs.dispatch_existing == 'true'"
)
WEEKLY_BUMP_PREFIXES = (
    "python3 scripts/python/write-version-stamp.py ",
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
RELEASE_COMMIT_SHA = "${{ steps.release-commit.outputs.sha }}"
TAG_COMMIT_SHA = "${{ steps.tag-commit.outputs.base_commit }}"
RELEASE_CHECKOUT_REF = "${{ inputs.tag || github.ref }}"
# Only these actions may run in the publishing job; a new action needs a
# deliberate review and an entry here.
RELEASE_JOB_ACTIONS = {
    "actions/checkout",
    "actions/setup-dotnet",
    "actions/cache",
    RELEASE_ACTION,
}

# Trusted validators are fetched from reviewed main and staged at the exact
# paths their repository-root resolution expects.
VALIDATOR_STAGING = {
    "verify-release-provenance.sh": ".release-validators/verify-release-provenance.sh",
    "validate-release-source.sh": ".release-validators/validate-release-source.sh",
    "verify-release.sh": ".release-validators/scripts/verify-release.sh",
    "validate-release-artifacts.sh": ".release-validators/validate-release-artifacts.sh",
}
STAGING_COMMAND = (
    "git show refs/remotes/origin/main:scripts/bash/{validator} > {destination}"
)
RELEASE_PACK_PROJECTS = (
    "Origo.Core.Contracts/Origo.Core.Contracts.csproj",
    "Origo.Core.Kernel/Origo.Core.Kernel.csproj",
    "Origo.Core/Origo.Core.csproj",
    "Origo.GodotAdapter/Origo.GodotAdapter.csproj",
    "Origo.ConsoleBridge/Origo.ConsoleBridge.csproj",
)
PACKAGE_VALIDATOR = "scripts/bash/validate-release-packages.sh"
GODOT_JOB = "godot-integration-tests"
RELEASE_SNAPSHOT_MARK_STEP = "Mark snapshot release as pre-release"
# The snapshot marker is identified by its reviewed contents, not just its name.
RELEASE_SNAPSHOT_MARK_IF = (
    "${{ inputs.snapshot || contains(steps.resolve-tag.outputs.tag, '-') }}"
)
RELEASE_SNAPSHOT_MARK_RUN = 'gh release edit "${TAG}" --prerelease'
# Gate scripts that must run in the release job before the tag moves.
RELEASE_GATE_SCRIPTS = (
    ("format gate", "scripts/bash/format.sh"),
    ("test gate", "scripts/bash/test.sh"),
    ("benchmark gate", "scripts/bash/benchmark.sh"),
    ("package consumer smoke", "scripts/bash/package-consumer-smoke.sh"),
)
# The artifact-set validator is staged from reviewed main because old release
# tags predate it; the tag tree must never be the source of that validator.
ARTIFACTS_VALIDATOR = ".release-validators/validate-release-artifacts.sh"
ARTIFACTS_VALIDATOR_TAG_TREE = "scripts/bash/validate-release-artifacts.sh"
ARTIFACTS_VALIDATOR_ARGV = (
    "bash", ARTIFACTS_VALIDATOR, "./artifacts", "${TAG#v}",
)
ARTIFACTS_STEP_PRELUDE_RE = re.compile(
    r"^(?:set\s|export\s|[A-Za-z_][A-Za-z0-9_]*=)"
)
WRITE_COMMANDS = frozenset({
    "cp", "install", "mv", "rsync", "ln", "tee", "dd", "cat", "sed", "perl",
    "python", "python3", "find", "git", "curl", "wget", "tar", "unzip",
    "touch", "truncate",
})
SHELL_COMMANDS = frozenset({"bash", "sh", "dash", "zsh"})
ARTIFACTS_WRITE_COMMANDS = WRITE_COMMANDS | {"rm", "unlink", "shred"}


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


_ASSIGNMENT_RE = re.compile(r"[A-Za-z_][A-Za-z0-9_]*=")


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
    """Return shell lines that can execute, with comments removed.

    Backslash continuations are joined exactly like the shell joins them, so a
    required command is only ever satisfied by code that runs. Inline comments
    are stripped as well, so a marker or argument hidden after ``#`` cannot
    satisfy a rule. Lines containing ``||`` are returned here and filtered by
    :func:`_runs_command`, because ``<command> || true`` must not satisfy a
    required-execution rule.
    """
    joined = text.replace("\\\n", " ").replace("\\\r", " ")
    lines: list[str] = []
    for raw_line in joined.splitlines():
        line = _strip_shell_comment(raw_line.strip())
        if not line:
            continue
        lines.append(line)
    return lines


def _active_text(text: str) -> str:
    """Comment-free, continuation-joined view used for marker checks."""
    return "\n".join(_active_lines(text))


_HEREDOC_DELIMITER_CHARS = frozenset(
    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-"
)


def _heredoc_delimiters(line: str) -> list[str]:
    """Return every heredoc delimiter introduced by one shell line.

    Handles ``<<EOF``, ``<<-EOF``, ``<<'EOF-1'``, ``<<"EOF"``, ``<<\\EOF``,
    and multiple heredocs on one line. Quote and command-substitution context is
    tracked, so a heredoc inside ``"$(...)"`` (the weekly window helper) is
    still recognized while operators inside quoted text are ignored.
    """
    delimiters: list[str] = []
    contexts: list[str] = []
    index = 0
    while index < len(line):
        char = line[index]
        context = contexts[-1] if contexts else None
        if context == "single":
            if char == "'":
                contexts.pop()
            index += 1
            continue
        if context == "double":
            if char == '"':
                contexts.pop()
            elif line.startswith("$(", index):
                contexts.append("subshell")
                index += 2
                continue
            index += 1
            continue
        if char in ('"', "'"):
            contexts.append("single" if char == "'" else "double")
            index += 1
            continue
        if line.startswith("$(", index):
            contexts.append("subshell")
            index += 2
            continue
        if char == ")" and context == "subshell":
            contexts.pop()
            index += 1
            continue
        if not line.startswith("<<", index):
            index += 1
            continue
        cursor = index + 2
        if cursor < len(line) and line[cursor] == "-":
            cursor += 1
        while cursor < len(line) and line[cursor] in " \t":
            cursor += 1
        if cursor < len(line) and line[cursor] in ('"', "'"):
            closing = line.find(line[cursor], cursor + 1)
            if closing < 0:
                break
            delimiters.append(line[cursor + 1 : closing])
            index = closing + 1
            continue
        if cursor < len(line) and line[cursor] == "\\":
            cursor += 1
        start = cursor
        while cursor < len(line) and line[cursor] in _HEREDOC_DELIMITER_CHARS:
            cursor += 1
        if cursor > start:
            delimiters.append(line[start:cursor])
            index = cursor
            continue
        index += 2
    return delimiters


def _strip_heredocs(text: str) -> str:
    """Drop heredoc bodies so marker checks cannot read inert script text."""
    kept: list[str] = []
    pending: list[str] = []
    for raw_line in text.splitlines():
        stripped = raw_line.strip()
        if pending:
            if stripped == pending[0]:
                pending.pop(0)
            continue
        kept.append(raw_line)
        if stripped.startswith("#"):
            continue
        pending.extend(_heredoc_delimiters(raw_line))
    return "\n".join(kept)


def _command_tokens(line: str) -> list[str] | None:
    """Tokenize a code line, stripping ``sudo``/``env``/assignment prefixes."""
    try:
        tokens = shlex.split(line, posix=True)
    except ValueError:
        return None
    index = 0
    while index < len(tokens):
        token = tokens[index]
        if token == "sudo":
            index += 1
            while index < len(tokens) and tokens[index].startswith("-"):
                index += 1
            continue
        if token == "timeout":
            index += 1
            # Skip the duration argument of `timeout <duration> <command>`.
            if index < len(tokens) and not tokens[index].startswith("-"):
                index += 1
            continue
        if token == "env":
            index += 1
            while index < len(tokens) and (
                tokens[index].startswith("-") or _ASSIGNMENT_RE.match(tokens[index])
            ):
                index += 1
            continue
        if _ASSIGNMENT_RE.match(token):
            index += 1
            continue
        break
    return tokens[index:] or None


def _runs_command(line: str) -> list[str] | None:
    """Return the argv of an executing line, or ``None`` when masked/empty."""
    if "||" in line:
        return None
    return _command_tokens(line)


def _active_commands(text: str) -> list[tuple[str, list[str]]]:
    """Return ``(line, argv)`` pairs for non-comment, unmasked commands."""
    commands: list[tuple[str, list[str]]] = []
    for line in _active_lines(text):
        tokens = _runs_command(line)
        if tokens:
            commands.append((line, tokens))
    return commands


def _unquoted_shell_metachars(line: str) -> str:
    """Return shell metacharacters that can chain, mask, or substitute.

    ``;``, ``&``, ``|``, backticks, ``$(``, ``<(``, and ``<<`` are detected
    outside single quotes. Inside double quotes the shell still performs
    command substitution and backtick expansion, so ``$(`` and backticks are
    rejected there as well.
    """
    quote = ""
    found: list[str] = []
    index = 0
    while index < len(line):
        char = line[index]
        if quote == "'":
            if char == "'":
                quote = ""
            index += 1
            continue
        if quote == '"':
            if char == '"':
                quote = ""
            elif char == "`":
                found.append("`")
            elif char == "$" and index + 1 < len(line) and line[index + 1] == "(":
                found.append("$(")
                index += 1
            index += 1
            continue
        if char in ('"', "'"):
            quote = char
            index += 1
            continue
        if char in ";&|`":
            found.append(char)
        elif char == "$" and index + 1 < len(line) and line[index + 1] == "(":
            found.append("$(")
            index += 1
        elif char == "<" and index + 1 < len(line) and line[index + 1] in "(<":
            found.append(line[index : index + 2])
            index += 1
        index += 1
    return "".join(found)


def _is_direct_execution(line: str) -> bool:
    """True when a line runs one command without chaining/substitution masking."""
    return not _unquoted_shell_metachars(line)


def _direct_commands(text: str) -> list[tuple[str, list[str]]]:
    """Return commands that are direct, single-command lines."""
    return [
        (line, tokens)
        for line, tokens in _active_commands(text)
        if _is_direct_execution(line)
    ]


def _step_conditional_errors(step: dict, label: str) -> list[str]:
    """Reject YAML conditionals on a step the guard requires to run."""
    errors: list[str] = []
    if "if" in step:
        errors.append(
            f"{label} must not set 'if:' because the guard requires it to run "
            "unconditionally."
        )
    if "continue-on-error" in step:
        errors.append(
            f"{label} must not set 'continue-on-error:' because a failed required "
            "step would otherwise be masked."
        )
    return errors


_FUNCTION_DEFINITION_RE = re.compile(
    r"^(?:function\s+[A-Za-z_][A-Za-z0-9_]*\s*(?:\(\s*\))?"
    r"|[A-Za-z_][A-Za-z0-9_]*\s*\(\s*\))\s*\{"
)
_AFTER_REQUIRED_ALLOWED_COMMANDS = frozenset({"echo", "printf", "grep", "set"})


def _step_hiding_errors(run: str, label: str) -> list[str]:
    """Reject shell constructs that would stop a matched command from running."""
    errors: list[str] = []
    for line in _active_lines(run):
        if "<<" in line:
            errors.append(
                f"{label} must not hide required commands in a heredoc: {line!r}."
            )
            continue
        if "<#" in line or "#>" in line:
            errors.append(
                f"{label} must not hide required commands in a PowerShell "
                f"block comment: {line!r}."
            )
            continue
        if re.match(r"if\s+(?:false\b|\[\s*false\s*\])", line):
            errors.append(
                f"{label} must not hide required commands in an always-false "
                f"branch: {line!r}."
            )
            continue
        if _FUNCTION_DEFINITION_RE.match(line):
            errors.append(
                f"{label} must not wrap required commands in a function "
                f"definition: {line!r}."
            )
            continue
        if line.endswith(("&&", "||")) or line.startswith(("&&", "||", ";", "&")):
            errors.append(
                f"{label} must not chain a required step into a conditional "
                f"fragment: {line!r}."
            )
    return errors


_CONTROL_FLOW_OPENERS = frozenset({"if", "for", "while", "until", "case", "select"})
_CONTROL_FLOW_CLOSERS = frozenset({"fi", "done", "esac"})
_SHORT_CIRCUIT_COMMANDS = frozenset({"exit", "return", "trap", "exec"})


def _default_shell_errors(step: dict, label: str) -> list[str]:
    """Reject a non-default/non-Bash shell on a step the guard requires."""
    shell = step.get("shell")
    if shell is None:
        return []
    if str(shell).strip() == "bash":
        return []
    return [
        f"{label} must use the default/Bash shell, not shell: {shell!r}, because "
        "the guard inspects its run text as shell."
    ]


def _disables_errexit(tokens: list[str]) -> bool:
    """True for ``set +e`` and ``set +o errexit``/``set +o pipefail`` forms."""
    if not tokens or tokens[0] != "set":
        return False
    arguments = tokens[1:]
    for position, argument in enumerate(arguments):
        if argument.startswith("+") and "e" in argument:
            return True
        if (
            argument == "+o"
            and position + 1 < len(arguments)
            and arguments[position + 1] in {"errexit", "pipefail"}
        ):
            return True
    return False


def _required_execution_status_errors(
    step: dict, run: str, label: str, required_lines: set[str]
) -> list[str]:
    """Require a trustworthy exit status around the required command.

    The GitHub Bash shell runs with ``-e -o pipefail`` by default, so an
    explicit ``set -e`` is not required; disabling errexit is. The required
    command must also be the last executable line (only display/set prelude is
    allowed after it), so a trailing ``true`` cannot mask a failed gate.
    """
    errors: list[str] = []
    lines = _active_lines(run)
    for line in lines:
        tokens = _command_tokens(line) or []
        if _disables_errexit(tokens):
            errors.append(f"{label} must not disable errexit: {line!r}.")
    required_indices = [
        index for index, line in enumerate(lines) if line in required_lines
    ]
    if not required_indices:
        return errors
    last_required = required_indices[-1]
    for line in lines[last_required + 1 :]:
        tokens = _command_tokens(line) or []
        first = tokens[0] if tokens else ""
        if first in _AFTER_REQUIRED_ALLOWED_COMMANDS:
            continue
        errors.append(
            f"{label} must not run commands after its required command: "
            f"{line!r}."
        )
    return errors


def _is_success_exit(tokens: list[str]) -> bool:
    """True when an exit/return line can report success to the step."""
    if not tokens:
        return False
    if tokens[0] not in {"exit", "return"}:
        return False
    arguments = tokens[1:]
    if not arguments:
        return True
    status = arguments[0]
    if status == "0":
        return True
    if status.startswith(("$", "`")):
        return True
    return not re.fullmatch(r"[1-9][0-9]*", status)


def _brace_depths(lines: list[str]) -> list[int]:
    """Track only function/brace/subshell depth, not if/for keyword depth."""
    depths: list[int] = []
    depth = 0
    for line in lines:
        tokens = _command_tokens(line) or []
        first = tokens[0] if tokens else ""
        stripped = line.rstrip()
        closes = first in {"}", ")"}
        if closes:
            depth = max(0, depth - 1)
        depths.append(depth)
        opens = (
            first in {"{", "("}
            or stripped.endswith(("{", "("))
            or _FUNCTION_DEFINITION_RE.match(line) is not None
        )
        if opens:
            depth += 1
    return depths


def _required_flow_errors(
    run: str, label: str, required_lines: set[str], allow_conditional_exit_zero: bool = False
) -> list[str]:
    """Reject short-circuiting or nesting that makes a required line inert."""
    errors: list[str] = []
    lines = _active_lines(run)
    if not required_lines:
        return errors
    depths: list[int] = []
    depth = 0
    for line in lines:
        tokens = _command_tokens(line) or []
        first = tokens[0] if tokens else ""
        stripped = line.rstrip()
        opens_block = (
            first in _CONTROL_FLOW_OPENERS
            or first in {"{", "("}
            or stripped.endswith(("{", "("))
            or _FUNCTION_DEFINITION_RE.match(line) is not None
        )
        closes_block = first in _CONTROL_FLOW_CLOSERS or first in {"}", ")"}
        if closes_block:
            depth = max(0, depth - 1)
        depths.append(depth)
        if opens_block:
            depth += 1
    required_indices = [index for index, line in enumerate(lines) if line in required_lines]
    if not required_indices:
        errors.append(
            f"{label} must contain a required command the guard can locate."
        )
        return errors
    first_required = required_indices[0]
    for index, line in enumerate(lines):
        tokens = _command_tokens(line) or []
        first = tokens[0] if tokens else ""
        if first == "trap":
            errors.append(
                f"{label} must not install a trap in a required step: {line!r}."
            )
            continue
        if (
            first in {"exit", "return"}
            and index < first_required
            and tokens[1:2] in (["0"], [])
            and not (allow_conditional_exit_zero and depths[index] > 0)
        ):
            errors.append(
                f"{label} must not return success before its required command: "
                f"{line!r}."
            )
            continue
        if line in required_lines:
            if depths[index] > 0:
                errors.append(
                    f"{label} required command must not be inside a control-flow "
                    f"block: {line!r}."
                )
            continue
        if (
            index < first_required
            and depths[index] == 0
            and first in _SHORT_CIRCUIT_COMMANDS
        ):
            errors.append(
                f"{label} must not short-circuit before its required command: "
                f"{line!r}."
            )
    return errors


def _line_runs_bash_script(tokens: list[str], script: str) -> bool:
    if tokens[0] not in {"bash", "sh"}:
        return False
    index = 1
    while index < len(tokens) and tokens[index].startswith("-"):
        index += 1
    return index < len(tokens) and tokens[index] == script


def _line_runs_reviewed_script(tokens: list[str], script: str) -> bool:
    return tokens[:2] == ["bash", script]


def _line_calls_version_helper(tokens: list[str]) -> bool:
    helper_paths = {
        "scripts/python/write-version-stamp.py",
        "./scripts/python/write-version-stamp.py",
        "$PWD/scripts/python/write-version-stamp.py",
    }
    if tokens[0] not in {"python3", "python"}:
        return False
    index = 1
    while index < len(tokens) and tokens[index].startswith("-"):
        index += 1
    return index < len(tokens) and tokens[index] in helper_paths


def _line_calls_dotnet_pack(tokens: list[str], project: str) -> bool:
    return tokens[:3] == ["dotnet", "pack", project]


def _line_copies_nuget_config(tokens: list[str]) -> bool:
    return tokens[:2] == ["cp", "nuget.config.example"]


def _line_builds_source_archive(tokens: list[str]) -> bool:
    return (
        tokens[:2] == ["git", "archive"]
        and any("origo-source-${VERSION}.tar.gz" in token for token in tokens)
    )


def _line_packages_documentation_archive(tokens: list[str]) -> bool:
    return (
        tokens[:2] == ["tar", "-czf"]
        and any(token.endswith("${DIRNAME}.tar.gz") for token in tokens)
    ) or (
        tokens[:2] == ["zip", "-rq"]
        and any(token.endswith("${DIRNAME}.zip") for token in tokens)
    )


def _line_is_apt_install(tokens: list[str]) -> bool:
    return tokens and tokens[0] == "apt-get" and "install" in tokens


def _line_is_tag_push(tokens: list[str]) -> bool:
    return tokens == [
        "git",
        "push",
        "--force-with-lease=refs/tags/${TAG}:${BASE_SHA}",
        "origin",
        "refs/tags/${TAG}",
    ]


def _artifact_validation_step_errors(step: dict, label: str) -> list[str]:
    """Require exactly the reviewed artifact validator invocation plus prelude."""
    errors: list[str] = []
    run = str(step.get("run") or "")
    commands = _active_commands(run)
    validator_commands = [
        (line, tokens)
        for line, tokens in commands
        if tokens[:2] == ["bash", ARTIFACTS_VALIDATOR]
    ]
    if len(validator_commands) != 1:
        errors.append(
            f"{label} must run the artifact-set validator exactly once."
        )
    else:
        line, tokens = validator_commands[0]
        if tuple(tokens) != ARTIFACTS_VALIDATOR_ARGV:
            errors.append(
                f"{label} must run exactly 'bash {ARTIFACTS_VALIDATOR} "
                "./artifacts \"${TAG#v}\"'; got {line!r}."
            )
    validator_line = validator_commands[0][0] if len(validator_commands) == 1 else None
    seen_validator = False
    for line in _active_lines(run):
        if validator_line is not None and line == validator_line:
            seen_validator = True
            continue
        if seen_validator:
            errors.append(
                f"{label} must not run commands after the artifact-set "
                f"validator: {line!r}."
            )
            continue
        tokens = _command_tokens(line) or []
        if _disables_errexit(tokens):
            errors.append(f"{label} must not disable errexit: {line!r}.")
            continue
        if not ARTIFACTS_STEP_PRELUDE_RE.match(line):
            errors.append(
                f"{label} may only contain prelude lines before the exact "
                f"artifact-set validator command: {line!r}."
            )
    return errors


def _artifacts_write_errors(
    steps: list, artifacts_index: int, limit_index: int | None
) -> list[str]:
    """Reject artifact-directory writes after the artifact-set validation."""
    errors: list[str] = []
    limit = limit_index if limit_index is not None else len(steps)
    for index in range(artifacts_index + 1, limit):
        step = steps[index]
        if not isinstance(step, dict):
            continue
        label = str(step.get("name") or f"release.steps[{index}]")
        for line in _active_lines(str(step.get("run") or "")):
            tokens = _command_tokens(line) or []
            command = tokens[0].rsplit("/", 1)[-1] if tokens else ""
            mentions_artifacts = any(
                "./artifacts" in token or "artifacts/" in token for token in tokens
            ) or "./artifacts" in line
            if not mentions_artifacts:
                continue
            writer = command in ARTIFACTS_WRITE_COMMANDS or any(
                token.rsplit("/", 1)[-1] in ARTIFACTS_WRITE_COMMANDS
                for token in tokens[1:]
            )
            if writer or ">" in line:
                errors.append(
                    f"{label} must not write the artifacts directory after the "
                    f"artifact-set validation: {line!r}."
                )
    return errors


def _cleanup_relative_target(target: str) -> str:
    """Normalize a cd/pushd target for the .release-validators prefix check."""
    normalized = target.rstrip("/")
    for prefix in ("./", "$PWD/", "${PWD}/"):
        if normalized.startswith(prefix):
            normalized = normalized[len(prefix):]
    return normalized


def _release_validators_write_errors(steps: list, allowed_lines: set[str]) -> list[str]:
    """Reject any write into .release-validators outside the reviewed staging.

    A ``cd``/``pushd`` into ``.release-validators`` is rejected outright, and the
    working-directory state is tracked so a later relative redirect or copy
    cannot write there without an absolute path in the line.
    """
    errors: list[str] = []
    for index, step in enumerate(steps):
        if not isinstance(step, dict):
            continue
        label = str(step.get("name") or f"release.steps[{index}]")
        inside_validators = False
        for line in _active_lines(str(step.get("run") or "")):
            tokens = _command_tokens(line) or []
            command = tokens[0].rsplit("/", 1)[-1] if tokens else ""
            if command in {"cd", "pushd"}:
                targets = [token for token in tokens[1:] if not token.startswith("-")]
                target = _cleanup_relative_target(targets[0]) if targets else ""
                if target == ".release-validators" or target.startswith(
                    ".release-validators/"
                ):
                    errors.append(
                        f"{label} must not change directory into "
                        f".release-validators: {line!r}."
                    )
                    inside_validators = True
                else:
                    inside_validators = False
                continue
            if command == "popd":
                inside_validators = False
                continue
            if inside_validators:
                metachars = _unquoted_shell_metachars(line)
                writer = command in WRITE_COMMANDS or any(
                    token.rsplit("/", 1)[-1] in WRITE_COMMANDS
                    for token in (tokens or [])[1:]
                )
                if metachars or writer or ">" in line:
                    errors.append(
                        f"{label} must not write .release-validators from a "
                        f"changed working directory: {line!r}."
                    )
                continue
            if ".release-validators" not in line:
                continue
            if line in allowed_lines:
                continue
            metachars = _unquoted_shell_metachars(line)
            if metachars:
                errors.append(
                    f"{label} must not write .release-validators through shell "
                    f"masking ({metachars}): {line!r}."
                )
                continue
            shell_dash_c = command in SHELL_COMMANDS and any(
                token.startswith("-") and "c" in token.lstrip("-")
                for token in tokens[1:]
            )
            writer = command in WRITE_COMMANDS or any(
                token.rsplit("/", 1)[-1] in WRITE_COMMANDS for token in tokens[1:]
            )
            if command == "eval" or shell_dash_c:
                errors.append(
                    f"{label} must not write .release-validators through "
                    f"'{command}' evaluation: {line!r}."
                )
                continue
            if writer:
                errors.append(
                    f"{label} must not write .release-validators with "
                    f"'{command}': {line!r}."
                )
                continue
            if ">" in line:
                errors.append(
                    f"{label} must write .release-validators only through the "
                    f"reviewed git-show staging lines: {line!r}."
                )
            elif not (
                line.startswith("bash .release-validators/")
                or line.startswith("mkdir -p .release-validators/")
            ):
                errors.append(
                    f"{label} must not reference .release-validators outside "
                    "the reviewed staging lines and exact invocations: "
                    f"{line!r}."
                )
    return errors


def _calls_version_helper(run: str) -> bool:
    """True when a non-comment, unmasked line really invokes the helper."""
    helper_paths = {
        "scripts/python/write-version-stamp.py",
        "./scripts/python/write-version-stamp.py",
        "$PWD/scripts/python/write-version-stamp.py",
    }
    for _, tokens in _direct_commands(run):
        if tokens[0] not in {"python3", "python"}:
            continue
        index = 1
        while index < len(tokens) and tokens[index].startswith("-"):
            index += 1
        if index < len(tokens) and tokens[index] in helper_paths:
            return True
    return False


def _calls_dotnet_pack(run: str, project: str) -> bool:
    return any(
        tokens[:3] == ["dotnet", "pack", project]
        for _, tokens in _direct_commands(run)
    )


def _copies_directory_build_props(run: str) -> bool:
    return any(
        tokens[:2] == ["cp", "Directory.Build.props"]
        for _, tokens in _direct_commands(run)
    )


def _packages_documentation_snapshot(run: str) -> bool:
    tar_ok = any(
        tokens[:2] == ["tar", "-czf"]
        and any(token.endswith("${DIRNAME}.tar.gz") for token in tokens)
        for _, tokens in _direct_commands(run)
    )
    zip_ok = any(
        tokens[:2] == ["zip", "-rq"]
        and any(token.endswith("${DIRNAME}.zip") for token in tokens)
        for _, tokens in _direct_commands(run)
    )
    dirname_ok = any(
        line.startswith('DIRNAME="origo-docs-${VERSION}"') for line in _active_lines(run)
    )
    return tar_ok and zip_ok and dirname_ok


def _builds_source_archive(run: str) -> bool:
    return any(
        tokens[:2] == ["git", "archive"]
        and any("origo-source-${VERSION}.tar.gz" in token for token in tokens)
        for _, tokens in _direct_commands(run)
    )


def _copies_nuget_config(run: str) -> bool:
    return any(
        tokens[:2] == ["cp", "nuget.config.example"]
        for _, tokens in _direct_commands(run)
    )


def _release_push_errors(release_steps: list) -> list[str]:
    """Require exactly the reviewed tag-only push command.

    This is a structural tripwire, not a shell parser: every non-comment line
    that contains a ``push`` token must be the exact reviewed command. That
    rejects branch refspecs, extra refspecs, prefix commands, and a plain
    ``git push`` without spawning shell parsing edge cases.
    """
    expected = [
        "git",
        "push",
        "--force-with-lease=refs/tags/${TAG}:${BASE_SHA}",
        "origin",
        "refs/tags/${TAG}",
    ]
    errors: list[str] = []
    tag_pushes = 0
    for index, step in enumerate(release_steps):
        if not isinstance(step, dict):
            continue
        run = str(step.get("run") or "")
        joined = run.replace("\\\n", "").replace("\\\r", "")
        for number, raw_line in enumerate(joined.splitlines(), start=1):
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue
            try:
                tokens = shlex.split(line, posix=True)
            except ValueError:
                errors.append(
                    f"release.steps[{index}] line {number} cannot be parsed as shell: {line!r}."
                )
                continue
            if "push" not in tokens:
                continue
            if tokens != expected:
                errors.append(
                    f"release.steps[{index}] line {number} is not the reviewed "
                    f"tag-only push command: {line!r}."
                )
                continue
            tag_pushes += 1
    if tag_pushes != 1:
        errors.append(
            "release job must contain exactly one reviewed tag-only push so "
            f"the release commit reaches the tag without writing main; found {tag_pushes}."
        )
    return errors


def _calls_bash_script(run: str, script: str) -> bool:
    """True when a non-comment, unmasked line runs ``bash <script>``."""
    for _, tokens in _direct_commands(run):
        if tokens[0] not in {"bash", "sh"}:
            continue
        index = 1
        while index < len(tokens) and tokens[index].startswith("-"):
            index += 1
        if index < len(tokens) and tokens[index] == script:
            return True
    return False


def _step_index_with_run(release_steps: list, script: str) -> int | None:
    for index, step in enumerate(release_steps):
        if not isinstance(step, dict):
            continue
        if _calls_bash_script(str(step.get("run") or ""), script):
            return index
    return None


def _runs_reviewed_script(text: str, script: str) -> bool:
    """True when an unmasked line runs exactly ``bash <script>`` (no flags)."""
    return any(
        tokens[:2] == ["bash", script] for _, tokens in _direct_commands(text)
    )


def _step_index_with_reviewed_script(release_steps: list, script: str) -> int | None:
    for index, step in enumerate(release_steps):
        if not isinstance(step, dict):
            continue
        if _runs_reviewed_script(str(step.get("run") or ""), script):
            return index
    return None


def _step_index_with_push(release_steps: list) -> int | None:
    expected = [
        "git",
        "push",
        "--force-with-lease=refs/tags/${TAG}:${BASE_SHA}",
        "origin",
        "refs/tags/${TAG}",
    ]
    for index, step in enumerate(release_steps):
        if not isinstance(step, dict):
            continue
        joined = str(step.get("run") or "").replace("\\\n", "").replace("\\\r", "")
        for raw_line in joined.splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue
            try:
                if shlex.split(line, posix=True) == expected:
                    return index
            except ValueError:
                continue
    return None


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
    base_check_seen = False
    orphan_tag_recovery_seen = False
    orphan_tag_dispatch_condition_seen = False
    check_run = ""
    dispatch_run = ""
    check_step = None
    bump_step = None
    dispatch_step = None
    tag_pushes = 0
    for job_name, job in (workflow.get("jobs") or {}).items():
        if not isinstance(job, dict):
            continue
        if job.get("uses"):
            errors.append(
                f"weekly job '{job_name}' is a reusable workflow; only the "
                "reviewed inline weekly job is allowed."
            )
        job_required = any(
            isinstance(step, dict)
            and str(step.get("name") or "")
            in {WEEKLY_CHECK_STEP, WEEKLY_BUMP_STEP, WEEKLY_DISPATCH_STEP}
            for step in (job.get("steps") or [])
        )
        if job_required:
            for conditional in ("if", "continue-on-error"):
                if conditional in job:
                    errors.append(
                        f"weekly job '{job_name}' must not set '{conditional}:' "
                        "because the guard requires its steps to run."
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
            if step.get("name") == WEEKLY_CHECK_STEP:
                check_run = run
                check_step = step
            if step.get("name") == WEEKLY_BUMP_STEP:
                bump_step = step
            if step.get("name") == WEEKLY_DISPATCH_STEP:
                dispatch_run = run
                dispatch_step = step
            if _calls_bash_script(run, "scripts/bash/check-weekly-base.sh"):
                base_check_seen = True
            run_code = _active_text(_strip_heredocs(run))
            if ("gh api" in run_code and "/releases/tags/" in run_code
                    and 'echo "dispatch_existing=true"' in run_code):
                orphan_tag_recovery_seen = True
            if "Dispatch the release pipeline" in str(step.get("name") or ""):
                if "dispatch_existing" in str(step.get("if") or ""):
                    orphan_tag_dispatch_condition_seen = True
            joined = run.replace("\\\n", "").replace("\\\r", "")
            if step.get("name") == WEEKLY_BUMP_STEP:
                bump_seen = True
                if "<<" in _active_text(_strip_heredocs(run)):
                    errors.append(
                        "weekly bump step must not hide tag commands in a heredoc."
                    )
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
    for name, step_dict, expected_if in (
        (WEEKLY_CHECK_STEP, check_step, None),
        (WEEKLY_BUMP_STEP, bump_step, WEEKLY_BUMP_CONDITION),
        (WEEKLY_DISPATCH_STEP, dispatch_step, WEEKLY_DISPATCH_CONDITION),
    ):
        if not isinstance(step_dict, dict):
            continue
        label = f"weekly step '{name}'"
        errors.extend(_default_shell_errors(step_dict, label))
        run = str(step_dict.get("run") or "")
        if "<#" in run or "#>" in run:
            errors.append(
                f"{label} must not use PowerShell block comments in a required "
                "shell step."
            )
        if "continue-on-error" in step_dict:
            errors.append(
                f"{label} must not set 'continue-on-error:' because a failed "
                "required step would otherwise be masked."
            )
        actual_if = step_dict.get("if")
        if expected_if is None:
            if "if" in step_dict:
                errors.append(
                    f"{label} must not set 'if:' because it must run "
                    "unconditionally."
                )
        elif actual_if != expected_if:
            errors.append(
                f"{label} must set 'if:' to exactly {expected_if!r} so the "
                "documented dispatch/bump condition cannot be weakened."
            )
        if name in {WEEKLY_BUMP_STEP, WEEKLY_DISPATCH_STEP}:
            for line in _active_lines(run):
                tokens = _command_tokens(line) or []
                first = tokens[0] if tokens else ""
                if first == "trap":
                    errors.append(
                        f"{label} must not install a trap: {line!r}."
                    )
                elif first in {"exit", "return"}:
                    errors.append(
                        f"{label} must not exit before its work completes: "
                        f"{line!r}."
                    )
    if not bump_seen:
        errors.append(
            f"weekly workflow must define the '{WEEKLY_BUMP_STEP}' step."
        )
    if not base_check_seen:
        errors.append(
            "weekly workflow must run scripts/bash/check-weekly-base.sh so a "
            "snapshot cannot sort at or below the latest formal release."
        )
    if tag_pushes != 1:
        errors.append(
            "weekly bump step must contain exactly one tag push "
            f"{WEEKLY_TAG_PUSH!r}; found {tag_pushes}."
        )
    if not orphan_tag_recovery_seen:
        errors.append(
            "weekly workflow must check the GitHub release state for an existing tag "
            "and dispatch the release pipeline when it is missing, draft, or incomplete."
        )
    if not orphan_tag_dispatch_condition_seen:
        errors.append(
            "weekly dispatch step must also run when the tag exists without "
            "a GitHub Release (dispatch_existing)."
        )
    if not check_run:
        errors.append(
            f"weekly workflow must define the '{WEEKLY_CHECK_STEP}' step."
        )
    else:
        required_check_markers = {
            'release_is_complete': (
                "the weekly check must define release_is_complete()"
            ),
            'gh api': (
                "the weekly check must query the GitHub API for release state"
            ),
            '/releases/tags/': (
                "the weekly check must query a release by tag"
            ),
            '"$draft" == "false"': (
                "the weekly check must reject draft releases"
            ),
            '"$prerelease" == "true"': (
                "the weekly check must require a pre-release"
            ),
            'expected_snapshot_assets': (
                "the weekly check must compare the exact nine expected snapshot asset names"
            ),
            'LC_ALL=C sort': (
                "the weekly check must sort asset names in a locale-independent order"
            ),
            '"$assets" == "$expected"': (
                "the weekly check must require all nine expected snapshot assets by name"
            ),
            'Origo.ConsoleBridge.${version}.nupkg': (
                "the weekly check must expect Origo.ConsoleBridge.${version}.nupkg"
            ),
            'Origo.Core.${version}.nupkg': (
                "the weekly check must expect Origo.Core.${version}.nupkg"
            ),
            'Origo.Core.Contracts.${version}.nupkg': (
                "the weekly check must expect Origo.Core.Contracts.${version}.nupkg"
            ),
            'Origo.Core.Kernel.${version}.nupkg': (
                "the weekly check must expect Origo.Core.Kernel.${version}.nupkg"
            ),
            'Origo.GodotAdapter.${version}.nupkg': (
                "the weekly check must expect Origo.GodotAdapter.${version}.nupkg"
            ),
            'origo-docs-${version}.tar.gz': (
                "the weekly check must expect origo-docs-${version}.tar.gz"
            ),
            'origo-docs-${version}.zip': (
                "the weekly check must expect origo-docs-${version}.zip"
            ),
            'origo-source-${version}.tar.gz': (
                "the weekly check must expect origo-source-${version}.tar.gz"
            ),
            'nuget.config': (
                "the weekly check must expect nuget.config"
            ),
            'HTTP 404': (
                "the weekly check must distinguish a missing release (404) from other API errors"
            ),
            'record_orphan "$TAG"': (
                "the weekly check must recover an incomplete tag found by the remote check"
            ),
            'if [[ -z "$WINDOW_COMMITS" ]]': (
                "the weekly check must still consider the current window after orphan recovery"
            ),
            'echo "new_version=${NEW_VERSION}" >> "$GITHUB_OUTPUT"': (
                "the weekly check must emit new_version when the window has commits"
            ),
            'echo "tag=${TAG}" >> "$GITHUB_OUTPUT"': (
                "the weekly check must emit tag when the window has commits"
            ),
        }
        check_stripped = _strip_heredocs(check_run)
        check_code = _active_text(check_stripped)
        check_lines = _active_lines(check_stripped)
        for marker, message in required_check_markers.items():
            if marker not in check_code:
                errors.append(f"weekly workflow check: {message}.")

        # The completeness result must be an actual assertion line, not marker
        # text hidden in a comment, heredoc body, or behind `true`, and it must
        # be evaluated before any early exit from the check step.
        check_depths = _brace_depths(check_lines)
        exit_indices = [
            index
            for index, line in enumerate(check_lines)
            if check_depths[index] == 0
            and _is_success_exit(_command_tokens(line) or [])
        ]
        first_exit_index = exit_indices[0] if exit_indices else None
        completeness_index = next(
            (
                index
                for index, line in enumerate(check_lines)
                if line.startswith("[[")
                and line.endswith("]]")
                and '"$draft" == "false"' in line
                and '"$prerelease" == "true"' in line
                and '"$assets" == "$expected"' in line
            ),
            None,
        )
        if completeness_index is None:
            errors.append(
                "weekly release completeness must be a code line starting with "
                "`[[` and ending with `]]` that requires a published pre-release "
                "with the exact expected asset names; comment, heredoc, echo, "
                "`|| true`, and `true` substitutes do not count."
            )
        else:
            if first_exit_index is not None and completeness_index > first_exit_index:
                errors.append(
                    "weekly release completeness assertion must be evaluated "
                    "before any early exit in the check step."
                )

        orphan_start = check_code.find('if [[ -s "$ORPHAN_TAGS_FILE" ]]; then')
        window_start = check_code.find('if [[ -z "$WINDOW_COMMITS" ]]', orphan_start)
        if orphan_start < 0 or window_start < 0:
            errors.append(
                "weekly workflow check must record/probe orphans before considering the window."
            )
        elif "exit 0" in check_code[orphan_start:window_start]:
            errors.append(
                "weekly orphan recovery must not exit before the current window can emit a new tag."
            )
        orphan_line_index = next(
            (
                index
                for index, line in enumerate(check_lines)
                if 'if [[ -s "$ORPHAN_TAGS_FILE" ]]' in line
            ),
            None,
        )
        if (
            first_exit_index is not None
            and orphan_line_index is not None
            and first_exit_index < orphan_line_index
        ):
            errors.append(
                "weekly check step must not exit before the orphan scan completes."
            )

        error_marker = 'echo "ERROR: GitHub release lookup for ${release_tag} failed: ${state}" >&2'
        error_index = check_code.find(error_marker)
        if error_index < 0:
            errors.append(
                "weekly release lookup must fail on non-404 GitHub API errors."
            )
        else:
            next_fi = check_code.find("fi", error_index)
            if next_fi < 0 or "exit 1" not in check_code[error_index:next_fi]:
                errors.append(
                    "weekly release lookup must exit 1 on non-404 GitHub API errors, not return a false completeness result."
                )

    if not dispatch_run:
        errors.append(
            f"weekly workflow must define the '{WEEKLY_DISPATCH_STEP}' step."
        )
    else:
        dispatch_code = _active_text(_strip_heredocs(dispatch_run))
        if "<<" in dispatch_code:
            errors.append(
                "weekly dispatch step must not use a heredoc around the "
                "release-pipeline commands."
            )
        if 'while IFS= read -r ORPHAN_TAG; do' not in dispatch_code:
            errors.append(
                "weekly dispatch step must iterate every recovered orphan tag."
            )
        if 'done < "$ORPHAN_TAGS_FILE"' not in dispatch_code:
            errors.append(
                "weekly dispatch step must consume the complete orphan tag file."
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
            target_commitish = (
                with_section.get("target_commitish")
                if isinstance(with_section, dict)
                else None
            )
            if target_commitish != RELEASE_COMMIT_SHA:
                errors.append(
                    f"{job_name}.steps[{index}] must set "
                    f"{RELEASE_ACTION} with.target_commitish to "
                    f"{RELEASE_COMMIT_SHA}; a missing tag must never be created "
                    "at the default branch."
                )
            prerelease = with_section.get("prerelease") if isinstance(with_section, dict) else None
            expected_prerelease = (
                "${{ inputs.snapshot || contains(steps.resolve-tag.outputs.tag, '-') }}"
            )
            if prerelease != expected_prerelease:
                errors.append(
                    f"{job_name}.steps[{index}] must set "
                    f"{RELEASE_ACTION} with.prerelease to {expected_prerelease}; "
                    "a cancelled/failed later edit must not leave a snapshot as the stable Latest release."
                )

    release_job = (workflow.get("jobs") or {}).get("release")
    if isinstance(release_job, dict):
        for conditional in ("if", "continue-on-error"):
            if conditional in release_job:
                errors.append(
                    "release job must not set "
                    f"'{conditional}:' because the guard requires its steps to run."
                )
        release_needs = release_job.get("needs")
        if isinstance(release_needs, str):
            release_needs = [release_needs]
        if not isinstance(release_needs, list) or GODOT_JOB not in [
            str(need) for need in release_needs
        ]:
            errors.append(
                f"release job must declare 'needs: {GODOT_JOB}' so the Godot "
                "integration gate runs before publishing."
            )
    release_steps = release_job.get("steps") if isinstance(release_job, dict) else None
    if not isinstance(release_steps, list):
        errors.append("release workflow must define release job steps.")
    else:
        checkout_step = next(
            (
                step
                for step in release_steps
                if isinstance(step, dict)
                and str(step.get("uses") or "").split("@", 1)[0] == "actions/checkout"
            ),
            None,
        )
        checkout_with = checkout_step.get("with") if isinstance(checkout_step, dict) else None
        if not isinstance(checkout_with, dict) or checkout_with.get("ref") != RELEASE_CHECKOUT_REF:
            errors.append(
                "the release job checkout must set "
                f"ref to {RELEASE_CHECKOUT_REF}; an explicit ref keeps a "
                "re-run working after this workflow moved the tag."
            )

        run_commands = [
            str(step.get("run") or "")
            for step in release_steps
            if isinstance(step, dict)
        ]
        step_names = [
            str(step.get("name") or "")
            for step in release_steps
            if isinstance(step, dict)
        ]
        non_empty_names = [name for name in step_names if name]
        duplicate_names = sorted(
            {
                name
                for name in non_empty_names
                if non_empty_names.count(name) > 1
            }
        )
        for name in duplicate_names:
            errors.append(f"release step name '{name}' must be unique.")
        snapshot_mark_indices = [
            index
            for index, step in enumerate(release_steps)
            if isinstance(step, dict)
            and str(step.get("name") or "") == RELEASE_SNAPSHOT_MARK_STEP
        ]
        snapshot_mark_index: int | None = None
        if len(snapshot_mark_indices) != 1:
            errors.append(
                f"release job must define exactly one "
                f"'{RELEASE_SNAPSHOT_MARK_STEP}' step."
            )
        else:
            candidate = release_steps[snapshot_mark_indices[0]]
            candidate_run = str(candidate.get("run") or "")
            has_reviewed_command = any(
                _normalize_command(line) == _normalize_command(RELEASE_SNAPSHOT_MARK_RUN)
                for line in _active_lines(candidate_run)
            )
            if candidate.get("if") != RELEASE_SNAPSHOT_MARK_IF:
                errors.append(
                    "the snapshot pre-release marker step must keep its reviewed "
                    "'if:' condition; that condition is the only allowed step "
                    "condition in the release job."
                )
            elif not has_reviewed_command:
                errors.append(
                    "the snapshot pre-release marker step must run the reviewed "
                    f"command {RELEASE_SNAPSHOT_MARK_RUN!r}."
                )
            else:
                snapshot_mark_index = snapshot_mark_indices[0]

        lint_index = _step_index_with_run(release_steps, "scripts/bash/lint-scripts.sh")
        if lint_index is None:
            errors.append(
                "release job must run bash scripts/bash/lint-scripts.sh so the tag "
                "pipeline enforces script, workflow, and instruction guards."
            )
        else:
            lint_env = release_steps[lint_index].get("env")
            if not isinstance(lint_env, dict) or lint_env.get("ORIGO_BRANCH_NAME") != RESOLVED_TAG_REF:
                errors.append(
                    "release job must pass ORIGO_BRANCH_NAME as "
                    f"{RESOLVED_TAG_REF} when linting a detached release checkout."
                )
        version_index = next(
            (
                index
                for index, step in enumerate(release_steps)
                if isinstance(step, dict)
                and _calls_version_helper(str(step.get("run") or ""))
            ),
            None,
        )
        version_step = (
            release_steps[version_index] if version_index is not None else None
        )
        if version_step is None:
            errors.append(
                "release job must call scripts/python/write-version-stamp.py so the "
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

        provenance_index = _step_index_with_reviewed_script(
            release_steps, ".release-validators/verify-release-provenance.sh"
        )
        if provenance_index is None:
            errors.append(
                "release job must run scripts/bash/verify-release-provenance.sh so a "
                "formal tag cannot bypass main review and a snapshot tag must "
                "come from a weekly version-stamp commit."
            )
        else:
            provenance_env = release_steps[provenance_index].get("env")
            if not isinstance(provenance_env, dict) or provenance_env.get("TAG") != RESOLVED_TAG:
                errors.append(
                    "the provenance step must receive "
                    f"TAG={RESOLVED_TAG}."
                )
            if (
                not isinstance(provenance_env, dict)
                or provenance_env.get("BASE_COMMIT_SHA") != TAG_COMMIT_SHA
            ):
                errors.append(
                    "the provenance step must receive "
                    f"BASE_COMMIT_SHA={TAG_COMMIT_SHA}."
                )

        source_index = _step_index_with_reviewed_script(
            release_steps, ".release-validators/validate-release-source.sh"
        )
        if source_index is None:
            errors.append(
                "release job must run bash scripts/bash/validate-release-source.sh "
                "so the attached source archive proves its version stamps."
            )
        else:
            source_env = release_steps[source_index].get("env")
            if not isinstance(source_env, dict) or source_env.get("TAG") != RESOLVED_TAG:
                errors.append(
                    "the source-archive validation step must receive "
                    f"TAG={RESOLVED_TAG}."
                )
            source_run = str(release_steps[source_index].get("run") or "")
            source_line = next(
                (
                    line
                    for line in _active_lines(source_run)
                    if line.startswith("bash .release-validators/validate-release-source.sh")
                ),
                None,
            )
            if (source_line is None
                    or '"$PWD/.release-validators/scripts/verify-release.sh"' not in source_line):
                errors.append(
                    "the source-archive validation step must run the validator and "
                    "pass the trusted \"$PWD/.release-validators/scripts/verify-release.sh\" "
                    "verifier."
                )

        metadata_index = next(
            (
                index
                for index, step in enumerate(release_steps)
                if isinstance(step, dict)
                and step.get("name") == "Verify release metadata"
            ),
            None,
        )
        metadata_step = (
            release_steps[metadata_index] if metadata_index is not None else None
        )
        if metadata_step is None:
            errors.append(
                "release job must define the 'Verify release metadata' step so the "
                "formal CHANGELOG/analyzer/stamp gate actually runs."
            )
        else:
            metadata_run = str(metadata_step.get("run") or "")
            if not _runs_reviewed_script(
                metadata_run, ".release-validators/scripts/verify-release.sh"
            ):
                errors.append(
                    "the metadata verification step must execute the trusted "
                    ".release-validators/scripts/verify-release.sh verifier."
                )
            exports_tag_version = any(
                tokens[0] == "export"
                and any(token.startswith("TAG_VERSION=") for token in tokens)
                for _, tokens in _direct_commands(metadata_run)
            )
            if not exports_tag_version:
                errors.append(
                    "the metadata verification step must export TAG_VERSION for "
                    "the trusted verifier."
                )

        push_index = _step_index_with_push(release_steps)
        action_index = next(
            (
                index
                for index, step in enumerate(release_steps)
                if isinstance(step, dict)
                and str(step.get("uses") or "").split("@", 1)[0] == RELEASE_ACTION
            ),
            None,
        )

        def step_index_with(predicate) -> int | None:
            for index, step in enumerate(release_steps):
                if isinstance(step, dict) and predicate(str(step.get("run") or "")):
                    return index
            return None

        gate_indices: dict[str, int | None] = {}
        for gate_label, gate_script in RELEASE_GATE_SCRIPTS:
            gate_index = _step_index_with_run(release_steps, gate_script)
            gate_indices[gate_label] = gate_index
            if gate_index is None:
                errors.append(
                    f"release job must run bash {gate_script} so the documented "
                    f"{gate_label} runs before the tag moves."
                )
        format_index = gate_indices["format gate"]
        test_index = gate_indices["test gate"]
        benchmark_index = gate_indices["benchmark gate"]
        package_smoke_index = gate_indices["package consumer smoke"]
        api_index = _step_index_with_run(release_steps, "scripts/bash/api-inventory.sh")

        pack_indices = [
            step_index_with(
                lambda run, project=project: _calls_dotnet_pack(run, project)
            )
            for project in RELEASE_PACK_PROJECTS
        ]
        for project, pack_index in zip(RELEASE_PACK_PROJECTS, pack_indices):
            if pack_index is None:
                errors.append(
                    f"release job must pack {project} so the five-package release "
                    "set is complete."
                )
        package_index = _step_index_with_run(release_steps, PACKAGE_VALIDATOR)
        if package_index is None:
            errors.append(
                f"release job must run bash {PACKAGE_VALIDATOR} so the five package "
                "identities and shell/kernel pairing are validated."
            )
        else:
            package_env = release_steps[package_index].get("env")
            if not isinstance(package_env, dict) or package_env.get("TAG") != RESOLVED_TAG:
                errors.append(
                    "the release package validation step must receive "
                    f"TAG={RESOLVED_TAG}."
                )
        nuget_index = step_index_with(_copies_nuget_config)
        if nuget_index is None:
            errors.append(
                "release job must copy nuget.config.example to "
                "./artifacts/nuget.config so the documented nuget.config asset "
                "is attached."
            )
        docs_index = step_index_with(_packages_documentation_snapshot)
        if docs_index is None:
            errors.append(
                "release job must build the documentation tar.gz and zip snapshot "
                "(origo-docs-<version>) so the two documentation assets are attached."
            )
        source_tree_index = step_index_with(_builds_source_archive)
        if source_tree_index is None:
            errors.append(
                "release job must build the source archive with git archive and the "
                "origo-source-<version>.tar.gz name before validating it."
            )
        artifacts_index = _step_index_with_reviewed_script(
            release_steps, ARTIFACTS_VALIDATOR
        )
        if artifacts_index is None:
            errors.append(
                "release job must run the staged artifact-set validator "
                f"(bash {ARTIFACTS_VALIDATOR}) after source-archive validation and "
                "before moving the tag so the attached artifact set is exactly the "
                "documented nine names."
            )
        else:
            artifacts_env = release_steps[artifacts_index].get("env")
            if not isinstance(artifacts_env, dict) or artifacts_env.get("TAG") != RESOLVED_TAG:
                errors.append(
                    "the artifact-set validation step must receive "
                    f"TAG={RESOLVED_TAG}."
                )
        for command in run_commands:
            for line in _active_lines(command):
                if line.startswith(
                    (f"bash {ARTIFACTS_VALIDATOR_TAG_TREE}",
                     f"sh {ARTIFACTS_VALIDATOR_TAG_TREE}")
                ):
                    errors.append(
                        "release job must not execute the tag-tree artifact-set "
                        f"validator; it is staged from main: {line!r}."
                    )
        if artifacts_index is not None:
            artifacts_label = str(
                release_steps[artifacts_index].get("name")
                or f"release.steps[{artifacts_index}]"
            )
            errors.extend(
                _artifact_validation_step_errors(
                    release_steps[artifacts_index], artifacts_label
                )
            )
            errors.extend(
                _artifacts_write_errors(release_steps, artifacts_index, action_index)
            )
        if not any(_copies_directory_build_props(command) for command in run_commands):
            errors.append(
                "release job must copy Directory.Build.props into the "
                "documentation snapshot so the manual's version reference resolves."
            )

        last_pack_index = max(
            (index for index in pack_indices if index is not None), default=None
        )
        ordered_steps = [
            ("tag provenance", provenance_index),
            ("version stamping", version_index),
            ("script lint", lint_index),
            ("format gate", format_index),
            ("shell API baseline gate", api_index),
            ("test gate", test_index),
            ("benchmark gate", benchmark_index),
            ("package consumer smoke", package_smoke_index),
            ("packing the five packages", last_pack_index),
            ("release package validation", package_index),
            ("the NuGet config copy", nuget_index),
            ("the documentation snapshot", docs_index),
            ("the source archive", source_tree_index),
            ("source-archive validation", source_index),
            ("artifact-set validation", artifacts_index),
            ("the tag push", push_index),
            ("GitHub Release creation", action_index),
        ]
        if all(index is not None for _, index in ordered_steps):
            if any(
                previous_index >= next_index
                for (_, previous_index), (_, next_index) in zip(
                    ordered_steps, ordered_steps[1:]
                )
            ):
                errors.append(
                    "release job must run tag provenance, version stamping, script "
                    "lint, the format/API/test/benchmark/package-smoke gates, "
                    "packing, package validation, the NuGet config copy, the "
                    "documentation snapshot, the source archive, source-archive "
                    "validation, artifact-set validation, the tag push, and "
                    "GitHub Release creation in that order."
                )

        release_commit_step = next(
            (
                step
                for step in release_steps
                if isinstance(step, dict) and step.get("id") == "release-commit"
            ),
            None,
        )
        if release_commit_step is None:
            errors.append(
                "release job must define the release-commit step so "
                "target_commitish has a SHA to pin."
            )
        elif 'echo "sha=' not in str(release_commit_step.get("run") or ""):
            errors.append(
                "the release-commit step must write its sha output for "
                "target_commitish."
            )

        staging_lines = [
            line
            for command in run_commands
            for line in _active_lines(command)
        ]
        expected_staging = {
            validator: STAGING_COMMAND.format(
                validator=validator, destination=destination
            )
            for validator, destination in VALIDATOR_STAGING.items()
        }
        for validator, expected_line in expected_staging.items():
            fetch_prefix = f"git show refs/remotes/origin/main:scripts/bash/{validator}"
            if not any(line.startswith(fetch_prefix) for line in staging_lines):
                errors.append(
                    f"release job must fetch scripts/bash/{validator} from "
                    "refs/remotes/origin/main so the tag tree cannot replace "
                    "the validator."
                )
            if expected_line not in staging_lines:
                errors.append(
                    "release job must stage the trusted validator with exactly "
                    f"'{expected_line}'."
                )
        errors.extend(
            _release_validators_write_errors(
                release_steps, set(expected_staging.values())
            )
        )
        if not any(
            line == expected_staging["verify-release.sh"]
            for line in staging_lines
        ):
            errors.append(
                "release job must stage the trusted verifier at "
                ".release-validators/scripts/verify-release.sh: the verifier "
                "resolves the repository root as two directories above its own "
                "path, so a deeper staging directory silently breaks it."
            )

        for command in run_commands:
            for line in _active_lines(command):
                if line in expected_staging.values():
                    continue
                tokens = _command_tokens(line) or []
                if not tokens:
                    continue
                if any(
                    f"scripts/bash/{validator}" in token
                    for validator in VALIDATOR_STAGING
                    for token in tokens
                ):
                    errors.append(
                        "release job must execute staged validators from "
                        f".release-validators, not the tag tree: {line!r}."
                    )

        errors.extend(_release_push_errors(release_steps))

        installed_packages: set[str] = set()
        for command in run_commands:
            for _, tokens in _direct_commands(command):
                if tokens[0] == "apt-get" and "install" in tokens:
                    installed_packages.update(tokens)
        for dependency in ("shellcheck", "python3-yaml"):
            if dependency not in installed_packages:
                errors.append(
                    f"release job must install script-lint dependency "
                    f"'{dependency}' before running scripts/bash/lint-scripts.sh."
                )

        if api_index is None:
            errors.append(
                "release job must run bash scripts/bash/api-inventory.sh so the tag "
                "pipeline enforces the shell API baseline."
            )
        else:
            api_env = release_steps[api_index].get("env")
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

        required_step_indices: set[int] = {
            index
            for index in (
                lint_index,
                format_index,
                api_index,
                test_index,
                benchmark_index,
                package_smoke_index,
                version_index,
                provenance_index,
                source_index,
                metadata_index,
                last_pack_index,
                package_index,
                nuget_index,
                docs_index,
                source_tree_index,
                artifacts_index,
                push_index,
                action_index,
                *pack_indices,
            )
            if index is not None
        }
        required_matchers: dict[int, list] = {}

        def require_line(index: int | None, matcher) -> None:
            if index is None:
                return
            required_matchers.setdefault(index, []).append(matcher)

        require_line(
            lint_index,
            lambda line, tokens: _line_runs_bash_script(
                tokens, "scripts/bash/lint-scripts.sh"
            ),
        )
        require_line(
            format_index,
            lambda line, tokens: _line_runs_bash_script(
                tokens, "scripts/bash/format.sh"
            ),
        )
        require_line(
            api_index,
            lambda line, tokens: _line_runs_bash_script(
                tokens, "scripts/bash/api-inventory.sh"
            ),
        )
        require_line(
            test_index,
            lambda line, tokens: _line_runs_bash_script(
                tokens, "scripts/bash/test.sh"
            ),
        )
        require_line(
            benchmark_index,
            lambda line, tokens: _line_runs_bash_script(
                tokens, "scripts/bash/benchmark.sh"
            ),
        )
        require_line(
            package_smoke_index,
            lambda line, tokens: _line_runs_bash_script(
                tokens, "scripts/bash/package-consumer-smoke.sh"
            ),
        )
        require_line(
            provenance_index,
            lambda line, tokens: _line_runs_reviewed_script(
                tokens, ".release-validators/verify-release-provenance.sh"
            ),
        )
        require_line(
            source_index,
            lambda line, tokens: _line_runs_reviewed_script(
                tokens, ".release-validators/validate-release-source.sh"
            ),
        )
        require_line(
            metadata_index,
            lambda line, tokens: _line_runs_reviewed_script(
                tokens, ".release-validators/scripts/verify-release.sh"
            ),
        )
        require_line(
            artifacts_index,
            lambda line, tokens: _line_runs_reviewed_script(
                tokens, ARTIFACTS_VALIDATOR
            ),
        )
        require_line(version_index, lambda line, tokens: _line_calls_version_helper(tokens))
        require_line(package_index, lambda line, tokens: _line_runs_bash_script(tokens, PACKAGE_VALIDATOR))
        require_line(nuget_index, lambda line, tokens: _line_copies_nuget_config(tokens))
        require_line(
            docs_index,
            lambda line, tokens: _line_packages_documentation_archive(tokens),
        )
        require_line(source_tree_index, lambda line, tokens: _line_builds_source_archive(tokens))
        require_line(push_index, lambda line, tokens: _line_is_tag_push(tokens))
        for project, pack_index in zip(RELEASE_PACK_PROJECTS, pack_indices):
            require_line(
                pack_index,
                lambda line, tokens, project=project: _line_calls_dotnet_pack(
                    tokens, project
                ),
            )
        for index, step in enumerate(release_steps):
            if not isinstance(step, dict):
                continue
            run = str(step.get("run") or "")
            if any(
                line in expected_staging.values() for line in _active_lines(run)
            ):
                required_step_indices.add(index)
                require_line(
                    index,
                    lambda line, tokens, allowed=set(expected_staging.values()): (
                        line in allowed
                    ),
                )
            if any(_line_is_apt_install(tokens) for _, tokens in _active_commands(run)):
                required_step_indices.add(index)
                require_line(index, lambda line, tokens: _line_is_apt_install(tokens))
        for index, step in enumerate(release_steps):
            if not isinstance(step, dict):
                continue
            if index == snapshot_mark_index:
                if "continue-on-error" in step:
                    errors.append(
                        f"{label} must not set 'continue-on-error:' because a "
                        "failed pre-release marking would leave a snapshot as "
                        "the Latest release."
                    )
                continue
            label = str(step.get("name") or f"release.steps[{index}]")
            errors.extend(_step_conditional_errors(step, label))
        for index in sorted(required_step_indices):
            step = release_steps[index]
            if not isinstance(step, dict):
                continue
            label = str(step.get("name") or f"release.steps[{index}]")
            run = str(step.get("run") or "")
            errors.extend(_step_hiding_errors(run, label))
            errors.extend(_default_shell_errors(step, label))
            matchers = required_matchers.get(index)
            if not matchers:
                continue
            required_lines = {
                line
                for line, tokens in _active_commands(run)
                if any(matcher(line, tokens) for matcher in matchers)
            }
            errors.extend(
                _required_flow_errors(
                    run,
                    label,
                    required_lines,
                    allow_conditional_exit_zero=(index == push_index),
                )
            )
            errors.extend(
                _required_execution_status_errors(step, run, label, required_lines)
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
