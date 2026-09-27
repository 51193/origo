#!/usr/bin/env python3
"""Behavior tests for the branch/worktree identity command."""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

import work_identity  # noqa: E402


class WorkIdentityTests(unittest.TestCase):
    def test_valid_branch_name_is_accepted(self) -> None:
        self.assertEqual(
            work_identity.validate_branch_name("feat/rouven-crp/0926/layered-checks"),
            (True, "valid branch name"),
        )

    def test_issue_prefixed_purpose_is_accepted(self) -> None:
        self.assertEqual(
            work_identity.validate_branch_name("fix/agent-42/0101/42-save-recovery"),
            (True, "valid branch name"),
        )

    def test_uppercase_and_underscore_are_rejected(self) -> None:
        valid, message = work_identity.validate_branch_name("Feat/RouVen-crp/0926/layered_checks")
        self.assertFalse(valid)
        self.assertIn("type", message)

    def test_invalid_calendar_date_is_rejected(self) -> None:
        valid, message = work_identity.validate_branch_name("fix/rouven-crp/0231/save-recovery")
        self.assertFalse(valid)
        self.assertIn("date", message)

    def test_exempt_branch_names_are_accepted(self) -> None:
        for name in ("main", "master", "dependabot/nuget/xunit", "refs/tags/v0.0.10"):
            self.assertEqual(work_identity.validate_branch_name(name), (True, "exempt context"))

    def test_tool_prefix_is_not_an_exemption(self) -> None:
        valid, message = work_identity.validate_branch_name("codex/rouven-crp/0926/layered-checks")
        self.assertFalse(valid)
        self.assertIn("type", message)

    def test_creator_must_be_lowercase_and_validated(self) -> None:
        with self.assertRaises(work_identity.WorkIdentityError):
            work_identity.normalize_creator("RouVen-crp")
        with self.assertRaises(ValueError):
            work_identity.normalize_creator("not a github login")

    def test_branch_and_worktree_names_are_derived_consistently(self) -> None:
        branch, worktree = work_identity.build_names(
            work_type="feat",
            creator="rouven-crp",
            month_day="0927",
            purpose="layered-checks",
            issue_number="43",
            repository_name="origo",
        )
        self.assertEqual(branch, "feat/rouven-crp/0927/43-layered-checks")
        self.assertEqual(worktree, "origo--feat--rouven-crp--0927--43-layered-checks")

    def test_branch_validation_rejects_uppercase_creator_and_purpose(self) -> None:
        for name in (
            "feat/RouVen-crp/0927/layered-checks",
            "feat/rouven-crp/0927/Layered-Checks",
        ):
            valid, _ = work_identity.validate_branch_name(name)
            self.assertFalse(valid)

    def test_creator_resolution_prefers_explicit_argument(self) -> None:
        with self.assertRaises(work_identity.WorkIdentityError):
            work_identity.resolve_creator("RouVen-crp", Path("/does/not/exist"))

    def test_validate_command_reports_invalid_branch(self) -> None:
        result = subprocess.run(
            ["bash", str(ROOT / "scripts/work-identity.sh"), "validate", "--branch", "codex/nope"],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("branch", result.stderr.lower())

    def test_new_command_creates_named_worktree(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repository = Path(directory) / "repo"
            repository.mkdir()
            subprocess.run(["git", "init", "-q", "-b", "main", str(repository)], check=True)
            (repository / "README.md").write_text("fixture\n", encoding="utf-8")
            subprocess.run(["git", "-C", str(repository), "add", "README.md"], check=True)
            subprocess.run(
                [
                    "git",
                    "-C",
                    str(repository),
                    "-c",
                    "user.name=Fixture",
                    "-c",
                    "user.email=fixture@example.invalid",
                    "commit",
                    "-q",
                    "-m",
                    "fixture",
                ],
                check=True,
            )
            worktree_root = Path(directory) / "worktrees"
            result = subprocess.run(
                [
                    "bash",
                    str(ROOT / "scripts/work-identity.sh"),
                    "new",
                    "--repository",
                    str(repository),
                    "--type",
                    "feat",
                    "--creator",
                    "rouven-crp",
                    "--purpose",
                    "layered-checks",
                    "--date",
                    "0927",
                    "--worktree-root",
                    str(worktree_root),
                ],
                cwd=ROOT,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            expected = worktree_root / "repo--feat--rouven-crp--0927--layered-checks"
            self.assertTrue(expected.is_dir())
            branch = subprocess.check_output(
                ["git", "-C", str(expected), "branch", "--show-current"], text=True
            ).strip()
            self.assertEqual(branch, "feat/rouven-crp/0927/layered-checks")

    def test_default_worktree_root_uses_main_checkout_from_managed_worktree(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repository = Path(directory) / "repo"
            repository.mkdir()
            subprocess.run(["git", "init", "-q", "-b", "main", str(repository)], check=True)
            (repository / "README.md").write_text("fixture\n", encoding="utf-8")
            subprocess.run(["git", "-C", str(repository), "add", "README.md"], check=True)
            subprocess.run(
                [
                    "git", "-C", str(repository), "-c", "user.name=Fixture",
                    "-c", "user.email=fixture@example.invalid", "commit", "-q", "-m", "fixture",
                ],
                check=True,
            )
            managed = Path(directory) / "managed"
            subprocess.run(
                ["git", "-C", str(repository), "worktree", "add", "-q", "-b", "feat/fixture/0927/check", str(managed)],
                check=True,
            )
            self.assertEqual(work_identity._main_checkout_root(managed), repository.resolve())
            result = subprocess.run(
                [
                    "bash",
                    str(ROOT / "scripts/work-identity.sh"),
                    "name",
                    "--repository",
                    str(managed),
                    "--type",
                    "feat",
                    "--creator",
                    "rouven-crp",
                    "--purpose",
                    "layered-checks",
                    "--date",
                    "0927",
                    "--format",
                    "worktree",
                ],
                cwd=ROOT,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(
                result.stdout.strip(),
                "repo--feat--rouven-crp--0927--layered-checks",
            )
            subprocess.run(["git", "-C", str(repository), "worktree", "remove", str(managed)], check=True)


if __name__ == "__main__":
    unittest.main()
