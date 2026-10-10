#!/usr/bin/env python3
"""Negative fixtures for the weekly-snapshot workflow guard.

`scripts/validate-release-workflow.py` has to reject a snapshot workflow that
would push a branch, and one that only names the version stamps without
rewriting them. Every case below mutates the tracked workflow in exactly one
way, asserts the mutation applied, and then asserts the guard reports that
specific problem — so the guard cannot silently stop enforcing it.

Run from `scripts/lint-scripts.sh` alongside the other script self-tests.
"""

from __future__ import annotations

import importlib.util
import pathlib
import re
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
GUARD_PATH = ROOT / "scripts" / "validate-release-workflow.py"
WORKFLOW_PATH = ROOT / ".github" / "workflows" / "weekly-build.yml"

PUSH_LINE = 'git push origin "refs/tags/${TAG}"'
REPLACEMENT_LOOP = re.compile(r"for element, replacement in \(\n(?:.*\n)*?\s*\):")
DISPATCH_STEP = "      - name: Dispatch the release pipeline for the snapshot tag"
STAMPS = ("Version", "AssemblyVersion", "FileVersion")


def load_guard():
    spec = importlib.util.spec_from_file_location(
        "origo_release_workflow_guard", GUARD_PATH
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


GUARD = load_guard()


class WeeklySnapshotGuardTests(unittest.TestCase):
    def setUp(self) -> None:
        self.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

    def errors_for(self, text: str) -> list[str]:
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "weekly-build.yml"
            path.write_text(text, encoding="utf-8")
            errors: list[str] = []
            GUARD.check_weekly_snapshot(errors, path)
            return errors

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


if __name__ == "__main__":
    unittest.main()
