#!/usr/bin/env python3
"""Negative fixtures for the release/weekly workflow guard.

`scripts/validate-release-workflow.py` has to reject a snapshot workflow that
would push a branch anywhere in a step script, one that only names the version
stamps without rewriting them, and a release write-back that lost its
condition. Every case below mutates the tracked workflow in exactly one way,
asserts the mutation applied, and then asserts the guard reports that specific
problem — so the guard cannot silently stop enforcing it.

Run from `scripts/lint-scripts.sh` alongside the other script self-tests.
"""

from __future__ import annotations

import importlib.util
import pathlib
import re
import tempfile
import unittest

import yaml

ROOT = pathlib.Path(__file__).resolve().parents[1]
GUARD_PATH = ROOT / "scripts" / "validate-release-workflow.py"
WORKFLOW_PATH = ROOT / ".github" / "workflows" / "weekly-build.yml"
RELEASE_WORKFLOW_PATH = ROOT / ".github" / "workflows" / "release.yml"

PUSH_LINE = 'git push origin "refs/tags/${TAG}"'
XML_REPLACEMENT = 'rf"(<{element}>)[^<]+(</{element}>)"'
REPLACEMENT_LOOP = re.compile(r"for element, replacement in \(\n(?:.*\n)*?\s*\):")
DISPATCH_STEP = "      - name: Dispatch the release pipeline for the snapshot tag"
STAMPS = ("Version", "AssemblyVersion", "FileVersion")
WRITE_BACK_IF = "if: steps.release-commit.outputs.changed == 'true'"
MAIN_REFSPEC = 'origin "${RELEASE_SHA}:refs/heads/main"'
MAIN_SHORTHAND = 'origin "${RELEASE_SHA}:main"'


def load_guard():
    spec = importlib.util.spec_from_file_location(
        "origo_release_workflow_guard", GUARD_PATH
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


GUARD = load_guard()


class GuardFixtureCase(unittest.TestCase):
    """Shared mutation helpers; subclasses supply the workflow under test."""

    workflow_path: pathlib.Path

    def setUp(self) -> None:
        self.workflow = self.workflow_path.read_text(encoding="utf-8")

    def mutate(self, old: str, new: str) -> str:
        self.assertIn(old, self.workflow, f"fixture anchor is gone: {old!r}")
        mutated = self.workflow.replace(old, new, 1)
        self.assertNotEqual(self.workflow, mutated, "fixture did not apply")
        return mutated

    def assert_reports(self, errors: list[str], fragment: str) -> None:
        self.assertTrue(
            any(fragment in error for error in errors),
            f"guard did not report {fragment!r}; got {errors}",
        )


class WeeklySnapshotGuardTests(GuardFixtureCase):
    workflow_path = WORKFLOW_PATH

    def errors_for(self, text: str) -> list[str]:
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "weekly-build.yml"
            path.write_text(text, encoding="utf-8")
            errors: list[str] = []
            GUARD.check_weekly_snapshot(errors, path)
            return errors

    def test_tracked_workflow_is_accepted(self) -> None:
        self.assertEqual([], self.errors_for(self.workflow))

    def test_branch_refspec_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, f'{PUSH_LINE} "refs/heads/main"')
        )
        self.assert_reports(errors, "refs/heads/main")

    def test_push_that_deletes_a_branch_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, f'{PUSH_LINE} ":refs/heads/main"')
        )
        self.assert_reports(errors, "refs/heads/main")

    def test_push_without_an_explicit_refspec_is_rejected(self) -> None:
        errors = self.errors_for(self.mutate(PUSH_LINE, "git push origin"))
        self.assert_reports(errors, "without an explicit tag refspec")

    def test_branch_push_after_a_separator_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, 'git fetch --tags && git push origin "refs/heads/main"')
        )
        self.assert_reports(errors, "refs/heads/main")

    def test_branch_push_behind_a_command_wrapper_is_rejected(self) -> None:
        mutated = self.mutate(
            DISPATCH_STEP,
            "      - name: wrapped\n"
            '        run: bash -c "git push origin refs/heads/main"\n\n'
            f"{DISPATCH_STEP}",
        )
        self.assert_reports(self.errors_for(mutated), "refs/heads/main")

    def test_branch_push_inside_command_substitution_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, "result=$(git push origin refs/heads/main)")
        )
        self.assert_reports(errors, "command substitution")

    def test_malformed_shell_is_rejected_without_fallback_parsing(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, 'git push origin "refs/tags/${TAG}')
        )
        self.assert_reports(errors, "malformed shell")

    def test_branch_push_with_a_path_option_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, 'git -C . push origin "refs/heads/main"')
        )
        self.assert_reports(errors, "refs/heads/main")

    def test_branch_push_with_a_git_dir_option_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, 'git --git-dir /tmp/x push origin "refs/heads/main"')
        )
        self.assert_reports(errors, "refs/heads/main")

    def test_branch_push_inside_a_subshell_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, '(git push origin "refs/heads/main")')
        )
        self.assert_reports(errors, "refs/heads/main")

    def test_branch_push_inside_a_loop_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(
                PUSH_LINE, 'for r in refs/heads/main; do git push origin "$r"; done'
            )
        )
        self.assert_reports(errors, "not a tag-only refspec")

    def test_bare_branch_destination_is_rejected(self) -> None:
        errors = self.errors_for(self.mutate(PUSH_LINE, "git push origin main"))
        self.assert_reports(errors, "not a tag-only refspec")

    def test_tag_only_pushes_are_accepted(self) -> None:
        for replacement in (
            'git -C . push origin "refs/tags/${TAG}"',
            'git push origin "refs/tags/${TAG}" "refs/tags/${TAG}-2"',
        ):
            with self.subTest(replacement=replacement):
                self.assertEqual([], self.errors_for(self.mutate(PUSH_LINE, replacement)))

    def test_commented_out_branch_push_is_accepted(self) -> None:
        mutated = self.mutate(
            PUSH_LINE, f"{PUSH_LINE}\n          # git push origin refs/heads/main"
        )
        self.assertEqual([], self.errors_for(mutated))

    def test_stamps_named_only_in_a_comment_are_rejected(self) -> None:
        mutated = REPLACEMENT_LOOP.sub(
            "for element, replacement in ():", self.workflow, count=1
        )
        self.assertNotEqual(self.workflow, mutated, "replacement loop fixture did not apply")
        errors = self.errors_for(mutated)
        for stamp in STAMPS:
            self.assert_reports(errors, stamp)

    def test_unrelated_step_naming_the_stamps_is_rejected(self) -> None:
        mutated = REPLACEMENT_LOOP.sub(
            "for element, replacement in ():", self.workflow, count=1
        )
        mutated = mutated.replace(
            DISPATCH_STEP,
            "      - name: unrelated\n"
            "        run: echo '\"Version\" \"AssemblyVersion\" \"FileVersion\"'\n\n"
            f"{DISPATCH_STEP}",
            1,
        )
        self.assertNotEqual(self.workflow, mutated, "unrelated-step fixture did not apply")
        errors = self.errors_for(mutated)
        for stamp in STAMPS:
            self.assert_reports(errors, stamp)

    def test_bump_step_without_a_substitution_is_rejected(self) -> None:
        errors = self.errors_for(self.mutate("re.subn(", "noop("))
        self.assert_reports(errors, "without substituting")

    def test_wrong_xml_closing_element_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(
                XML_REPLACEMENT,
                'rf"(<{element}>)[^<]+(</Wrong{element}>)"',
            )
        )
        self.assert_reports(errors, "matching XML element")

    def test_quoted_hash_does_not_hide_a_branch_push(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, 'echo "push #1"; git push origin refs/heads/main')
        )
        self.assert_reports(errors, "refs/heads/main")

    def test_quoted_hash_does_not_hide_a_command_substitution(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, 'echo "note # $(git push origin refs/heads/main)"')
        )
        self.assert_reports(errors, "command substitution")

    def test_expansion_between_git_and_push_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, "git${IFS}push origin refs/heads/main")
        )
        self.assert_reports(errors, "cannot resolve")

    def test_push_assigned_to_a_variable_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, 'CMD="git push origin refs/heads/main"; $CMD')
        )
        self.assert_reports(errors, "assigns a `git push` command")

    def test_variable_command_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, "g=git; p=push; $g $p origin refs/heads/main")
        )
        self.assert_reports(errors, "cannot resolve")

    def test_delegated_script_is_rejected(self) -> None:
        errors = self.errors_for(self.mutate(PUSH_LINE, "bash .github/publish-main.sh"))
        self.assert_reports(errors, "cannot be validated")

    def test_branch_capable_push_option_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(PUSH_LINE, 'git push --all origin "refs/tags/${TAG}"')
        )
        self.assert_reports(errors, "can publish branches")

    def test_substitution_result_must_be_written_back(self) -> None:
        errors = self.errors_for(
            self.mutate("content, count = re.subn(", "discard, count = re.subn(")
        )
        self.assert_reports(errors, "writes a different value")

    def test_fixed_replacement_value_is_rejected(self) -> None:
        errors = self.errors_for(
            self.mutate(
                "lambda match, value=replacement:",
                'lambda match, value="0.0.0":',
            )
        )
        self.assert_reports(errors, "replacement binding")

    def test_missing_match_count_check_is_rejected(self) -> None:
        errors = self.errors_for(self.mutate("if count != 1:", "if False:"))
        self.assert_reports(errors, "exactly one match")

    def test_prose_mention_of_git_push_is_accepted(self) -> None:
        mutated = self.mutate(
            DISPATCH_STEP,
            "      - name: prose\n"
            "        run: echo no git push was needed\n\n"
            f"{DISPATCH_STEP}",
        )
        self.assertEqual([], self.errors_for(mutated))

    def test_forced_tag_refspec_is_accepted(self) -> None:
        errors = self.errors_for(
            self.mutate(
                PUSH_LINE, 'git push origin "+refs/tags/${TAG}:refs/tags/${TAG}"'
            )
        )
        self.assertEqual([], errors)

    def test_step_quoting_the_props_file_without_stamping_is_accepted(self) -> None:
        mutated = self.mutate(
            DISPATCH_STEP,
            "      - name: notes\n"
            "        run: python3 -c \"open('NOTES.md', 'w').write("
            "'Directory.Build.props')\"\n\n"
            f"{DISPATCH_STEP}",
        )
        self.assertEqual([], self.errors_for(mutated))

    def test_parenthesized_match_count_check_is_accepted(self) -> None:
        errors = self.errors_for(self.mutate("if count != 1:", "if (count != 1):"))
        self.assertEqual([], errors)


class ReleaseWritebackGuardTests(GuardFixtureCase):
    workflow_path = RELEASE_WORKFLOW_PATH

    def errors_for(self, text: str) -> list[str]:
        errors: list[str] = []
        GUARD.check_release_writeback_is_conditional(yaml.safe_load(text), errors)
        return errors

    def test_tracked_release_workflow_is_accepted(self) -> None:
        self.assertEqual([], self.errors_for(self.workflow))

    def test_writeback_without_its_condition_is_rejected(self) -> None:
        errors = self.errors_for(self.mutate(WRITE_BACK_IF, "if: always()"))
        self.assert_reports(errors, GUARD.WRITE_BACK_CONDITION)

    def test_writeback_condition_cannot_be_weakened_by_boolean_or(self) -> None:
        mutated = self.mutate(
            WRITE_BACK_IF,
            "if: always() || steps.release-commit.outputs.changed == 'true'",
        )
        self.assert_reports(self.errors_for(mutated), "condition must be exactly")

    def test_writeback_with_a_main_shorthand_and_no_condition_is_rejected(self) -> None:
        mutated = self.mutate(WRITE_BACK_IF, "if: always()")
        self.assertIn(MAIN_REFSPEC, mutated, f"fixture anchor is gone: {MAIN_REFSPEC!r}")
        mutated = mutated.replace(MAIN_REFSPEC, MAIN_SHORTHAND, 1)
        self.assertNotEqual(self.workflow, mutated, "shorthand fixture did not apply")
        self.assert_reports(self.errors_for(mutated), GUARD.WRITE_BACK_CONDITION)

    def test_guard_accepts_a_tag_only_push_without_a_condition(self) -> None:
        text = (
            "jobs:\n"
            "  release:\n"
            "    steps:\n"
            "      - name: tag only\n"
            '        run: git push origin "refs/tags/${TAG}"\n'
        )
        self.assertEqual([], self.errors_for(text))

    def test_guard_rejects_a_branch_push_moved_to_another_step(self) -> None:
        text = (
            "jobs:\n"
            "  release:\n"
            "    steps:\n"
            "      - name: push main\n"
            '        run: git fetch origin && git push origin "HEAD:refs/heads/main"\n'
        )
        self.assert_reports(self.errors_for(text), GUARD.WRITE_BACK_CONDITION)

    def test_branch_push_in_a_new_job_is_rejected(self) -> None:
        text = self.workflow + (
            "\n  publish-main:\n"
            "    runs-on: ubuntu-latest\n"
            "    steps:\n"
            "      - name: push main\n"
            '        run: git push origin "HEAD:refs/heads/main"\n'
        )
        self.assert_reports(self.errors_for(text), "only the release job")

    def test_writeback_condition_cannot_be_bypassed_by_an_expansion(self) -> None:
        mutated = self.mutate(WRITE_BACK_IF, "if: always()")
        mutated = mutated.replace("git push --atomic", "git${IFS}push --atomic", 1)
        self.assertNotEqual(self.workflow, mutated, "expansion fixture did not apply")
        self.assert_reports(self.errors_for(mutated), GUARD.WRITE_BACK_CONDITION)

    def test_malformed_shell_reports_a_guard_error(self) -> None:
        mutated = self.mutate("git push --atomic", 'git push origin "unclosed')
        self.assert_reports(self.errors_for(mutated), "malformed shell")

    def test_release_sync_step_must_write_what_it_substitutes(self) -> None:
        mutated = self.mutate("content, count = re.subn(", "discard, count = re.subn(")
        errors: list[str] = []
        GUARD.check_release_stamp_sync(yaml.safe_load(mutated), errors)
        self.assert_reports(errors, "writes a different value")


if __name__ == "__main__":
    unittest.main()
