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
        r"scripts/python/write-version-stamp\.py(?:\"|\s|$)"
    )
    for line in run.splitlines():
        stripped = line.strip()
        if stripped.startswith("#") or "||" in stripped:
            continue
        if pattern.match(stripped):
            return True
    return False


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
    """True when a non-comment line starts with ``bash <script>``."""
    pattern = re.compile(rf"^bash\s+{re.escape(script)}(?:\s|$)")
    for line in run.splitlines():
        stripped = line.strip()
        if stripped.startswith("#") or "||" in stripped:
            continue
        if pattern.match(stripped):
            return True
    return False


def _step_index_with_run(release_steps: list, script: str) -> int | None:
    for index, step in enumerate(release_steps):
        if not isinstance(step, dict):
            continue
        if _calls_bash_script(str(step.get("run") or ""), script):
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
            if step.get("name") == WEEKLY_CHECK_STEP:
                check_run = run
            if step.get("name") == WEEKLY_DISPATCH_STEP:
                dispatch_run = run
            if _calls_bash_script(run, "scripts/bash/check-weekly-base.sh"):
                base_check_seen = True
            if ("gh api" in run and "/releases/tags/" in run
                    and "dispatch_existing=true" in run):
                orphan_tag_recovery_seen = True
            if "Dispatch the release pipeline" in str(step.get("name") or ""):
                if "dispatch_existing" in str(step.get("if") or ""):
                    orphan_tag_dispatch_condition_seen = True
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
            '"$assets" -ge 9': (
                "the weekly check must require all nine expected snapshot assets"
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
        for marker, message in required_check_markers.items():
            if marker not in check_run:
                errors.append(f"weekly workflow check: {message}.")

        orphan_start = check_run.find('if [[ -s "$ORPHAN_TAGS_FILE" ]]; then')
        window_start = check_run.find('if [[ -z "$WINDOW_COMMITS" ]]', orphan_start)
        if orphan_start < 0 or window_start < 0:
            errors.append(
                "weekly workflow check must record/probe orphans before considering the window."
            )
        elif "exit 0" in check_run[orphan_start:window_start]:
            errors.append(
                "weekly orphan recovery must not exit before the current window can emit a new tag."
            )

        error_marker = 'echo "ERROR: GitHub release lookup for ${release_tag} failed: ${state}" >&2'
        error_index = check_run.find(error_marker)
        if error_index < 0:
            errors.append(
                "weekly release lookup must fail on non-404 GitHub API errors."
            )
        else:
            next_fi = check_run.find("fi", error_index)
            if next_fi < 0 or "exit 1" not in check_run[error_index:next_fi]:
                errors.append(
                    "weekly release lookup must exit 1 on non-404 GitHub API errors, not return a false completeness result."
                )

    if not dispatch_run:
        errors.append(
            f"weekly workflow must define the '{WEEKLY_DISPATCH_STEP}' step."
        )
    else:
        if 'while IFS= read -r ORPHAN_TAG; do' not in dispatch_run:
            errors.append(
                "weekly dispatch step must iterate every recovered orphan tag."
            )
        if 'done < "$ORPHAN_TAGS_FILE"' not in dispatch_run:
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
        if not any("bash scripts/bash/lint-scripts.sh" in command for command in run_commands):
            errors.append(
                "release job must run bash scripts/bash/lint-scripts.sh so the tag "\
                "pipeline enforces script, workflow, and instruction guards."
            )
        else:
            lint_step = next(
                (
                    step
                    for step in release_steps
                    if isinstance(step, dict)
                    and "bash scripts/bash/lint-scripts.sh" in str(step.get("run") or "")
                ),
                None,
            )
            lint_env = lint_step.get("env") if isinstance(lint_step, dict) else None
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

        provenance_index = _step_index_with_run(
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

        source_index = _step_index_with_run(
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
            if "$PWD/.release-validators/scripts/bash/verify-release.sh" not in source_run:
                errors.append(
                    "the source-archive validation step must pass the trusted "
                    "$PWD/.release-validators/scripts/bash/verify-release.sh verifier."
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
        if None not in (
            provenance_index,
            version_index,
            source_index,
            push_index,
            action_index,
        ):
            if not (
                provenance_index
                < version_index
                < source_index
                < push_index
                < action_index
            ):
                errors.append(
                    "release job must run tag provenance, version stamping, "
                    "source-archive validation, the tag push, and GitHub Release "
                    "creation in that order."
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

        for validator in (
            "verify-release-provenance.sh",
            "validate-release-source.sh",
            "verify-release.sh",
        ):
            if not any(
                f"git show refs/remotes/origin/main:scripts/bash/{validator}" in command
                for command in run_commands
            ):
                errors.append(
                    f"release job must fetch scripts/bash/{validator} from "
                    "refs/remotes/origin/main so the tag tree cannot replace "
                    "the validator."
                )

        if not any("cp Directory.Build.props" in command for command in run_commands):
            errors.append(
                "release job must copy Directory.Build.props into the "
                "documentation snapshot so the manual's version reference resolves."
            )
        if not any("git archive" in command for command in run_commands):
            errors.append(
                "release job must build the source archive with git archive "
                "before validating it."
            )

        errors.extend(_release_push_errors(release_steps))

        for dependency in ("shellcheck", "python3-yaml"):
            if not any(dependency in command for command in run_commands):
                errors.append(
                    f"release job must install script-lint dependency "\
                    f"'{dependency}' before running scripts/bash/lint-scripts.sh."
                )

        api_step = next(
            (
                step
                for step in release_steps
                if isinstance(step, dict)
                and "bash scripts/bash/api-inventory.sh" in str(step.get("run") or "")
            ),
            None,
        )
        if api_step is None:
            errors.append(
                "release job must run bash scripts/bash/api-inventory.sh so the tag "
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
