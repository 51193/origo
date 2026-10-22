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
WEEKLY_WORKFLOW = ROOT / ".github" / "workflows" / "weekly-build.yml"


class ReleaseWorkflowGuardTests(unittest.TestCase):
    def run_guard(
        self, text: str, weekly_text: str | None = None
    ) -> tuple[int, str, str]:
        weekly = (
            weekly_text
            if weekly_text is not None
            else WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        )
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            workflow = root / ".github" / "workflows" / "release.yml"
            weekly_workflow = root / ".github" / "workflows" / "weekly-build.yml"
            workflow.parent.mkdir(parents=True)
            workflow.write_text(text, encoding="utf-8")
            weekly_workflow.write_text(weekly, encoding="utf-8")
            old_root, old_workflow, old_weekly = (
                guard.ROOT,
                guard.WORKFLOW,
                guard.WEEKLY_WORKFLOW,
            )
            guard.ROOT, guard.WORKFLOW, guard.WEEKLY_WORKFLOW = (
                root,
                workflow,
                weekly_workflow,
            )
            try:
                stdout = io.StringIO()
                stderr = io.StringIO()
                with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                    code = guard.main()
                return code, stdout.getvalue(), stderr.getvalue()
            finally:
                guard.ROOT, guard.WORKFLOW, guard.WEEKLY_WORKFLOW = (
                    old_root,
                    old_workflow,
                    old_weekly,
                )

    def mutated(self, old: str, new: str) -> str:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(old, text, f"fixture anchor not found: {old!r}")
        return text.replace(old, new, 1)

    def mutated_weekly(self, old: str, new: str) -> str:
        text = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(old, text, f"weekly fixture anchor not found: {old!r}")
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
        self.assertIn("Weekly-snapshot guard: OK", stdout)

    def test_weekly_branch_push_is_rejected(self):
        weekly = self.mutated_weekly(
            '          git push origin "refs/tags/${TAG}"',
            "          git push origin HEAD:main",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("exactly one tag push", stderr)

    def test_weekly_tag_and_branch_push_is_rejected(self):
        weekly = self.mutated_weekly(
            '          git push origin "refs/tags/${TAG}"',
            '          git push origin "refs/tags/${TAG}"\n'
            "          git push origin refs/heads/main",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("not an allowed command", stderr)

    def test_weekly_missing_tag_push_is_rejected(self):
        weekly = self.mutated_weekly(
            '          git push origin "refs/tags/${TAG}"',
            "          echo snapshot tagged",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("exactly one tag push", stderr)

    def test_weekly_command_builtin_push_is_rejected(self):
        weekly = self.mutated_weekly(
            '          git push origin "refs/tags/${TAG}"',
            '          GIT=git; command "$GIT" push origin refs/heads/main',
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("shell separator", stderr)

    def test_weekly_command_substitution_push_is_rejected(self):
        weekly = self.mutated_weekly(
            '          git push origin "refs/tags/${TAG}"',
            "          result=$(git push origin refs/heads/main)",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("substitution", stderr)

    def test_weekly_extra_push_step_is_rejected(self):
        weekly = self.mutated_weekly(
            "      - name: Dispatch the release pipeline for the snapshot tag",
            "      - name: Sneaky push\n"
            "        shell: bash\n"
            "        run: git push origin refs/heads/main\n\n"
            "      - name: Dispatch the release pipeline for the snapshot tag",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("pushes outside the bump step", stderr)

    def test_weekly_comment_push_is_ignored(self):
        weekly = self.mutated_weekly(
            '          git push origin "refs/tags/${TAG}"',
            "          # git push origin HEAD:main\n"
            '          git push origin "refs/tags/${TAG}"',
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 0, stderr)

    def test_weekly_unapproved_action_is_rejected(self):
        weekly = self.mutated_weekly(
            "      - name: Dispatch the release pipeline for the snapshot tag",
            "      - name: Move a ref\n"
            "        uses: some/ref-mover@v1\n\n"
            "      - name: Dispatch the release pipeline for the snapshot tag",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("unapproved action", stderr)

    def test_weekly_push_with_extra_flag_is_rejected(self):
        weekly = self.mutated_weekly(
            '          git push origin "refs/tags/${TAG}"',
            '          git push --tags origin "refs/tags/${TAG}"',
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("exactly one tag push", stderr)

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

    def test_release_checkout_ref_is_pinned(self):
        text = self.mutated(
            "          ref: ${{ inputs.tag || github.ref }}\n          fetch-depth: 0",
            "          ref: main\n          fetch-depth: 0",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("release job checkout must set", stderr)

    def test_release_job_rejects_main_push(self):
        text = self.insert_before_release_action(
            "      - name: Push main\n"
            "        run: git push origin HEAD:main\n"
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("not the reviewed tag-only push command", stderr)

    def test_release_job_requires_exactly_one_tag_push(self):
        text = self.mutated(
            "          git push \\\n"
            '            --force-with-lease="refs/tags/${TAG}:${BASE_SHA}" \\\n'
            '            origin "refs/tags/${TAG}"',
            "          echo no tag push",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("exactly one reviewed tag-only push", stderr)

    def test_release_action_target_commitish_is_checked(self):
        text = self.mutated(
            "          target_commitish: ${{ steps.release-commit.outputs.sha }}",
            "          target_commitish: main",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("target_commitish", stderr)

    def test_missing_source_archive_validation_is_rejected(self):
        text = self.mutated(
            "          bash .release-validators/validate-release-source.sh \\\n",
            "          echo skipped source validation \\\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("validate-release-source.sh", stderr)

    def test_release_job_rejects_extra_main_refspec(self):
        text = self.mutated(
            '          origin "refs/tags/${TAG}"',
            '          origin "refs/tags/${TAG}" "${RELEASE_SHA}:main"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("not the reviewed tag-only push command", stderr)

    def test_release_job_rejects_git_prefix_push(self):
        text = self.mutated(
            "          git push \\\n",
            "          git -C . push \\\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("not the reviewed tag-only push command", stderr)

    def test_release_job_requires_the_lease(self):
        text = self.mutated(
            '            --force-with-lease="refs/tags/${TAG}:${BASE_SHA}" \\\n',
            "            --force \\\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("not the reviewed tag-only push command", stderr)

    def test_release_job_rejects_a_different_tag(self):
        text = self.mutated(
            '            origin "refs/tags/${TAG}"',
            '            origin "refs/tags/v9.9.9"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("not the reviewed tag-only push command", stderr)

    def test_commented_source_validation_is_rejected(self):
        text = self.mutated(
            "          bash .release-validators/validate-release-source.sh \\\n",
            "          # bash .release-validators/validate-release-source.sh \\\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("validate-release-source.sh", stderr)

    def test_masked_source_validation_is_rejected(self):
        text = self.mutated(
            "          bash .release-validators/validate-release-source.sh \\\n",
            "          bash .release-validators/validate-release-source.sh || true \\\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("validate-release-source.sh", stderr)

    def test_source_validation_trusted_verifier_is_pinned(self):
        text = self.mutated(
            '            "$PWD/.release-validators/verify-release.sh"',
            '            "scripts/verify-release.sh"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("trusted", stderr)

    def test_missing_release_sha_output_is_rejected(self):
        text = self.mutated(
            '          echo "sha=$(git rev-parse HEAD)" >> "$GITHUB_OUTPUT"',
            '          echo "sha-missing" >> "$GITHUB_OUTPUT"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must write its sha output", stderr)

    def test_missing_docs_version_authority_is_rejected(self):
        text = self.mutated(
            '          cp Directory.Build.props "${DIRNAME}/"',
            "          true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("Directory.Build.props", stderr)

    def test_missing_trusted_validator_fetch_is_rejected(self):
        text = self.mutated(
            "          git show refs/remotes/origin/main:scripts/verify-release.sh > .release-validators/verify-release.sh",
            "          true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must fetch scripts/verify-release.sh", stderr)

    def test_missing_provenance_step_is_rejected(self):
        text = self.mutated(
            '        run: bash .release-validators/verify-release-provenance.sh "$TAG" "$BASE_COMMIT_SHA"',
            "        run: echo provenance skipped",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("verify-release-provenance.sh", stderr)

    def test_weekly_missing_base_check_is_rejected(self):
        weekly = self.mutated_weekly(
            '            bash scripts/check-weekly-base.sh "$BASE_VERSION" "${LATEST_FORMAL#v}"',
            "            echo base check skipped",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("check-weekly-base.sh", stderr)

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



    def test_weekly_missing_orphan_tag_dispatch_is_rejected(self):
        weekly = self.mutated_weekly(
            '            echo "dispatch_existing=true" >> "$GITHUB_OUTPUT"\n',
            "",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("check gh release view", stderr)

    def test_weekly_dispatch_condition_without_existing_tag_is_rejected(self):
        weekly = self.mutated_weekly(
            "        if: steps.check.outputs.new_version != '' || steps.check.outputs.dispatch_existing == 'true'",
            "        if: steps.check.outputs.new_version != ''",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("dispatch_existing", stderr)

if __name__ == "__main__":
    unittest.main()
