#!/usr/bin/env python3
"""Regression tests for scripts/validate-release-workflow.py.

The fixtures are derived from the real release workflow, so every new guard
rule is pinned against the document it protects. Run from
scripts/lint-scripts.sh.
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

    def test_release_create_from_ref_name_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        env:\n"
            "          TAG: ${{ github.ref_name }}\n"
            '        run: gh release create "${{ github.ref_name }}" --generate-notes\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_release_create_env_mixed_ref_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            "          WRONG: ${{ github.ref_name }}\n"
            '        run: gh release create "${WRONG}"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_release_create_multiline_command_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        run: |\n"
            "          gh release \\\n"
            '            create "${{ github.ref_name }}"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_readonly_release_api_call_is_allowed(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: List releases\n"
            "        run: gh api repos/51193/origo/releases/latest\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_version_step_must_call_the_helper(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            "          echo scripts/write-version-stamp.py",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must call scripts/write-version-stamp.py", stderr)

    def test_gh_api_field_creation_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        run: gh api repos/51193/origo/releases -f tag_name=\"${{ github.ref_name }}\"\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_curl_data_creation_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        run: curl -d '{\"tag_name\": \"${{ github.ref_name }}\"}' "
            "https://api.github.com/repos/51193/origo/releases\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_readonly_release_listing_with_redirect_is_allowed(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: List releases\n"
            "        run: gh api repos/51193/origo/releases/latest > release-output.json\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_release_update_is_allowed(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Update release\n"
            "        run: gh api -X PUT repos/51193/origo/releases/123 -f draft=false\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_gh_api_field_creation_with_env_tag_is_allowed(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Create release\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            '        run: gh api repos/51193/origo/releases -f tag_name="$TAG"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_release_create_must_bind_the_tag_argument(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            '        run: gh release create main --notes "tag=$TAG"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("tag_name/first argument", stderr)

    def test_release_create_with_env_tag_is_allowed(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Create release\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            '        run: gh release create "$TAG" --generate-notes\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

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

    def test_no_space_ref_expression_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        env:\n"
            "          WRONG: ${{github.ref_name}}\n"
            '        run: gh release create "${WRONG}"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_graphql_createrelease_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            '        run: gh api graphql -f query="createRelease"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("resolved tag", stderr)

    def test_explicit_post_release_creation_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        run: gh api -X POST repos/51193/origo/releases "
            "-f tag_name=\"${{ github.ref_name }}\"\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_version_step_run_referencing_ref_name_is_rejected(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            '          python3 scripts/write-version-stamp.py "${{ github.ref_name }}"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not read github.ref_name", stderr)

    def test_mixed_read_and_create_step_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Mixed\n"
            "        run: |\n"
            "          gh api -X GET repos/51193/origo/releases/latest > /dev/null\n"
            '          gh api repos/51193/origo/releases -f tag_name="${{ github.ref_name }}"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_mixed_update_and_create_step_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Mixed\n"
            "        run: |\n"
            "          gh api -X PUT repos/51193/origo/releases/123 -f draft=false\n"
            '          gh api repos/51193/origo/releases -f tag_name="${{ github.ref_name }}"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_shell_comment_mentioning_get_does_not_disable_detection(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Mixed\n"
            "        run: |\n"
            '          echo "use -X GET for reads"\n'
            '          gh api repos/51193/origo/releases -f tag_name="${{ github.ref_name }}"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_release_create_with_leading_flags_is_allowed(self):
        for flags in ("--draft ", "--repo 51193/origo ", "--notes \"notes\" "):
            with self.subTest(flags=flags):
                text = self.mutated(
                    "      - name: Create GitHub Release",
                    "      - name: Create release\n"
                    "        env:\n"
                    "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
                    f'        run: gh release create {flags}"$TAG"\n'
                    "      - name: Create GitHub Release",
                )
                code, _, stderr = self.run_guard(text)
                self.assertEqual(code, 0, stderr)

    def test_release_create_tag_suffix_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Create release\n"
            '        run: gh release create "${{ steps.resolve-tag.outputs.tag }}-beta"\n'
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("tag_name/first argument", stderr)

    def test_no_space_tag_expression_in_tag_name_is_allowed(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Create release\n"
            "        run: gh api repos/51193/origo/releases "
            "-f tag_name=\"${{steps.resolve-tag.outputs.tag}}\"\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_curl_fail_flag_is_not_creation(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Read release\n"
            "        run: curl -f https://api.github.com/repos/51193/origo/releases/latest\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_curl_json_creation_is_rejected(self):
        text = self.mutated(
            "      - name: Create GitHub Release",
            "      - name: Sneaky release\n"
            "        run: curl --json '{\"tag_name\": \"${{ github.ref_name }}\"}' "
            "https://api.github.com/repos/51193/origo/releases\n"
            "      - name: Create GitHub Release",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("github.ref_name", stderr)

    def test_helper_with_sudo_wrapper_is_allowed(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            '          sudo python3 scripts/write-version-stamp.py "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

    def test_helper_with_dot_slash_is_allowed(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            '          python3 ./scripts/write-version-stamp.py "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)

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

    def test_missing_version_helper_is_rejected(self):
        text = self.mutated(
            '          python3 scripts/write-version-stamp.py "$TAG_VERSION"',
            "          echo synced",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("write-version-stamp.py", stderr)


if __name__ == "__main__":
    unittest.main()
