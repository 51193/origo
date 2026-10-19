#!/usr/bin/env python3
"""Regression tests for scripts/validate-release-workflow.py.

The fixtures are derived from the real release workflow, so every guard rule
is pinned against the document it protects. Run from scripts/lint-scripts.sh.
"""

from __future__ import annotations

import contextlib
import importlib.util
import io
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "scripts" / "validate-release-workflow.py"
_SPEC = importlib.util.spec_from_file_location("validate_release_workflow", MODULE_PATH)
assert _SPEC is not None and _SPEC.loader is not None
guard = importlib.util.module_from_spec(_SPEC)
sys.modules["validate_release_workflow"] = guard
_SPEC.loader.exec_module(guard)

WORKFLOW = ROOT / ".github" / "workflows" / "release.yml"


class ReleaseWorkflowGuardTests(unittest.TestCase):
    def run_guard(self, text: str) -> tuple[int, str, str]:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            workflow = root / ".github" / "workflows" / "release.yml"
            workflow.parent.mkdir(parents=True)
            workflow.write_text(text, encoding="utf-8")
            old_root, old_workflow = guard.ROOT, guard.WORKFLOW
            guard.ROOT, guard.WORKFLOW = root, workflow
            try:
                stdout = io.StringIO()
                stderr = io.StringIO()
                with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                    code = guard.main()
                return code, stdout.getvalue(), stderr.getvalue()
            finally:
                guard.ROOT, guard.WORKFLOW = old_root, old_workflow

    def mutated(self, old: str, new: str) -> str:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(old, text, f"fixture anchor not found: {old!r}")
        return text.replace(old, new, 1)

    def insert_before_release_action(self, block: str) -> str:
        return self.mutated(
            "      - name: Create GitHub Release",
            block + "      - name: Create GitHub Release",
        )

    def test_current_workflow_passes(self):
        code, stdout, stderr = self.run_guard(WORKFLOW.read_text(encoding="utf-8"))
        self.assertEqual(code, 0, stderr)
        self.assertIn("Release-workflow guard: OK", stdout)

    def test_dynamic_uses_reference_is_rejected(self):
        text = self.mutated(
            "      - uses: actions/checkout@v7",
            "      - uses: ${{ env.CHECKOUT_ACTION }}",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("dynamic action reference", stderr)

    def test_unapproved_action_in_release_job_is_rejected(self):
        text = self.mutated(
            "        uses: softprops/action-gh-release@v3",
            "        uses: some/third-party-release-action@v1",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("unapproved action", stderr)

    def test_gh_api_release_creation_is_rejected(self):
        text = self.insert_before_release_action(
            "      - name: Create via api\n"
            "        run: gh api repos/51193/origo/releases -f tag_name=main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_curl_release_creation_is_rejected(self):
        text = self.insert_before_release_action(
            "      - name: Create via curl\n"
            "        run: |\n"
            "          curl -d '{\"tag_name\":\"main\"}' "
            "https://api.github.com/repos/51193/origo/releases\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_release_create_command_is_rejected_even_with_resolved_tag(self):
        text = self.insert_before_release_action(
            "      - name: Create via CLI\n"
            "        run: gh release create \"${{ steps.resolve-tag.outputs.tag }}\"\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_mixed_read_and_create_step_is_rejected(self):
        text = self.insert_before_release_action(
            "      - name: Mixed\n"
            "        run: |\n"
            "          gh api -X GET repos/51193/origo/releases/latest\n"
            "          gh api repos/51193/origo/releases -f tag_name=main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_inline_comment_does_not_hide_creation(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: |\n"
            "          gh api repos/51193/origo/releases -f tag_name=main  # -X GET\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_unmarked_read_only_call_is_rejected(self):
        text = self.insert_before_release_action(
            "      - name: Read releases\n"
            "        run: gh api repos/51193/origo/releases/latest\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("guard: read-only", stderr)

    def test_marked_read_only_call_is_allowed(self):
        text = self.insert_before_release_action(
            "      - name: Read releases\n"
            "        run: |\n"
            "          # guard: read-only\n"
            "          gh api repos/51193/origo/releases/latest > release-output.json\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_release_edit_is_allowed(self):
        text = self.insert_before_release_action(
            "      - name: Edit release\n"
            "        run: gh release edit \"${{ steps.resolve-tag.outputs.tag }}\" --prerelease\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_missing_version_helper_is_rejected(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            "          echo synced",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must call scripts/write-version-stamp.py", stderr)

    def test_release_action_tag_name_is_checked(self):
        text = self.mutated(
            "          tag_name: ${{ steps.resolve-tag.outputs.tag }}",
            "          tag_name: main",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must set", stderr)

    def test_marker_does_not_waive_creation(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: |\n"
            "          # guard: read-only\n"
            "          gh release create main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_marker_only_applies_to_the_next_line(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: |\n"
            "          # guard: read-only\n"
            "          # a normal comment\n"
            "          gh api repos/51193/origo/releases -f tag_name=main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_marker_substring_is_not_a_waiver(self):
        text = self.insert_before_release_action(
            "      - name: Read releases\n"
            "        run: |\n"
            "          # not guard: read-only\n"
            "          gh api repos/51193/origo/releases/latest\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("guard: read-only", stderr)

    def test_line_continuation_creation_is_rejected(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: |\n"
            "          gh release \\\n"
            "            create main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_double_space_creation_is_rejected(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: gh release  create main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_helper_with_quoted_pwd_is_allowed(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            '          python3 "$PWD/scripts/write-version-stamp.py" "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_release_edit_with_tag_flag_is_allowed(self):
        text = self.insert_before_release_action(
            "      - name: Edit release\n"
            "        run: gh release edit main --tag va.b.c\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_marker_does_not_waive_api_post_creation(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: |\n"
            "          # guard: read-only\n"
            "          gh api repos/51193/origo/releases -f tag_name=main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_marker_does_not_waive_curl_post_creation(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: |\n"
            "          # guard: read-only\n"
            "          curl -d '{\"tag_name\":\"main\"}' "
            "https://api.github.com/repos/51193/origo/releases\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_word_split_continuation_creation_is_rejected(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: |\n"
            "          gh release cre\\\n"
            "          ate main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_path_split_continuation_creation_is_rejected(self):
        text = self.insert_before_release_action(
            "      - name: Sneaky\n"
            "        run: |\n"
            "          gh api repos/51193/origo/relea\\\n"
            "          ses -f tag_name=main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("touches releases in a run step", stderr)

    def test_version_step_must_use_the_resolved_tag(self):
        text = self.mutated(
            '          TAG: ${{ steps.resolve-tag.outputs.tag }}\n'
            "        run: |\n"
            "          set -euo pipefail\n"
            "          # The release workflow builds from a tag checkout.",
            '          TAG: ${{ github.ref_name }}\n'
            "        run: |\n"
            "          set -euo pipefail\n"
            "          # The release workflow builds from a tag checkout.",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("version-stamp step must receive", stderr)

    def test_version_step_ref_name_in_run_is_rejected(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            '          python3 scripts/write-version-stamp.py "${{ github.ref_name }}"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not read github.ref_name", stderr)

    def test_version_step_echoing_the_helper_is_rejected(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            '          echo python3 scripts/write-version-stamp.py "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must call scripts/write-version-stamp.py", stderr)

    def test_version_step_commenting_the_helper_is_rejected(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            '          # python3 scripts/write-version-stamp.py "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must call scripts/write-version-stamp.py", stderr)

    def test_version_step_with_sudo_is_allowed(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            '          sudo python3 scripts/write-version-stamp.py "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)


if __name__ == "__main__":
    unittest.main()
