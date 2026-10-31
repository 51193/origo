#!/usr/bin/env python3
"""Regression tests for scripts/python/validate-release-workflow.py.

The fixtures are derived from the real release workflow, so every guard rule
is pinned against the document it protects. Run from scripts/bash/lint-scripts.sh.
"""

from __future__ import annotations

import contextlib
import importlib.util
import io
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = ROOT / "scripts" / "python" / "validate-release-workflow.py"
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
            '          python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
            "          echo synced",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must call scripts/python/write-version-stamp.py", stderr)

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
            '            "$PWD/.release-validators/scripts/verify-release.sh"',
            '            "scripts/bash/verify-release.sh"',
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
            "          git show refs/remotes/origin/main:scripts/bash/verify-release.sh > .release-validators/scripts/verify-release.sh",
            "          true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must fetch scripts/bash/verify-release.sh", stderr)

    def test_missing_metadata_step_is_rejected(self):
        text = self.mutated(
            '      - name: Verify release metadata\n',
            '      - name: Metadata step removed\n',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("Verify release metadata", stderr)

    def test_wrong_staged_verifier_depth_is_rejected(self):
        text = self.mutated(
            "git show refs/remotes/origin/main:scripts/bash/verify-release.sh > .release-validators/scripts/verify-release.sh",
            "git show refs/remotes/origin/main:scripts/bash/verify-release.sh > .release-validators/scripts/bash/verify-release.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("two directories above", stderr)

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
            '            bash scripts/bash/check-weekly-base.sh "$BASE_VERSION" "${LATEST_FORMAL#v}"',
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
            '          python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
            '          python3 "$PWD/scripts/python/write-version-stamp.py" "$TAG_VERSION"',
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
            '          python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
            '          python3 scripts/python/write-version-stamp.py "${{ github.ref_name }}"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not read github.ref_name", stderr)

    def test_version_step_echoing_the_helper_is_rejected(self):
        text = self.mutated(
            '          python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
            '          echo python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must call scripts/python/write-version-stamp.py", stderr)

    def test_version_step_commenting_the_helper_is_rejected(self):
        text = self.mutated(
            '          python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
            '          # python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must call scripts/python/write-version-stamp.py", stderr)

    def test_version_step_with_sudo_is_allowed(self):
        text = self.mutated(
            '          python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
            '          sudo python3 scripts/python/write-version-stamp.py "$TAG_VERSION"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 0, stderr)



    def test_release_action_without_prerelease_is_rejected(self):
        text = self.mutated(
            "          prerelease: ${{ inputs.snapshot || contains(steps.resolve-tag.outputs.tag, '-') }}\n",
            "",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("with.prerelease", stderr)

    def test_weekly_missing_orphan_tag_dispatch_is_rejected(self):
        old = '            echo "dispatch_existing=true" >> "$GITHUB_OUTPUT"\n'
        weekly = self.mutated_weekly(old, "")
        # The workflow contains the marker in both the orphan scan and the
        # remote-only tag branch; remove the second occurrence too.
        weekly = weekly.replace(old, "", 1)
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("check the GitHub release state", stderr)

    def test_weekly_orphan_recovery_exit_before_window_is_rejected(self):
        old = '            echo "dispatch_existing=true" >> "$GITHUB_OUTPUT"\n'
        weekly = self.mutated_weekly(old, old + "            exit 0\n")
        code, _, stderr = self.run_guard(WORKFLOW.read_text(encoding="utf-8"), weekly)
        self.assertEqual(code, 1)
        self.assertIn("must not exit before the current window", stderr)

    def test_weekly_non_404_return_is_rejected(self):
        old = (
            '            echo "ERROR: GitHub release lookup for ${release_tag} failed: ${state}" >&2\n'
            "            exit 1\n"
        )
        new = (
            '            echo "ERROR: GitHub release lookup for ${release_tag} failed: ${state}" >&2\n'
            "            return 1\n"
        )
        weekly = self.mutated_weekly(old, new)
        code, _, stderr = self.run_guard(WORKFLOW.read_text(encoding="utf-8"), weekly)
        self.assertEqual(code, 1)
        self.assertIn("must exit 1", stderr)

    def test_weekly_dispatch_skipping_orphan_file_is_rejected(self):
        old = '            done < "$ORPHAN_TAGS_FILE"'
        weekly = self.mutated_weekly(old, "              done < /dev/null")
        code, _, stderr = self.run_guard(WORKFLOW.read_text(encoding="utf-8"), weekly)
        self.assertEqual(code, 1)
        self.assertIn("consume the complete orphan tag file", stderr)

    def test_weekly_partial_release_completeness_is_rejected(self):
        old = '[[ "$draft" == "false" && "$prerelease" == "true" && "$assets" == "$expected" ]]'
        weekly = self.mutated_weekly(old, '[[ "$assets" -ge 9 ]]')
        code, _, stderr = self.run_guard(WORKFLOW.read_text(encoding="utf-8"), weekly)
        self.assertEqual(code, 1)
        self.assertIn("reject draft releases", stderr)

    def test_weekly_missing_expected_asset_name_is_rejected(self):
        old = '              "nuget.config" \\\n'
        weekly = self.mutated_weekly(old, '              "unexpected.config" \\\n')
        code, _, stderr = self.run_guard(WORKFLOW.read_text(encoding="utf-8"), weekly)
        self.assertEqual(code, 1)
        self.assertIn("nuget.config", stderr)

    def test_weekly_missing_new_tag_output_is_rejected(self):
        old = '          echo "tag=${TAG}" >> "$GITHUB_OUTPUT"\n'
        weekly = self.mutated_weekly(old, "")
        code, _, stderr = self.run_guard(WORKFLOW.read_text(encoding="utf-8"), weekly)
        self.assertEqual(code, 1)
        self.assertIn("must emit tag", stderr)

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


    # ------------------------------------------------------------------
    # Execution integrity: a required command only counts when code runs it.
    # ------------------------------------------------------------------
    def test_echoed_lint_scripts_command_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: echo bash scripts/bash/lint-scripts.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run bash scripts/bash/lint-scripts.sh", stderr)

    def test_masked_lint_scripts_command_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: bash scripts/bash/lint-scripts.sh || true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run bash scripts/bash/lint-scripts.sh", stderr)

    def test_echoed_api_inventory_command_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/api-inventory.sh",
            "        run: echo bash scripts/bash/api-inventory.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run bash scripts/bash/api-inventory.sh", stderr)

    def test_echoed_metadata_verifier_is_rejected(self):
        text = self.mutated(
            "          bash .release-validators/scripts/verify-release.sh",
            "          echo .release-validators/scripts/verify-release.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must execute the trusted", stderr)

    def test_tag_tree_verifier_copy_is_rejected(self):
        staging = (
            "          git show refs/remotes/origin/main:scripts/bash/verify-release.sh "
            "> .release-validators/scripts/verify-release.sh"
        )
        text = self.mutated(
            staging,
            "          # " + staging.strip() + "\n"
            "          cp scripts/bash/verify-release.sh "
            ".release-validators/scripts/verify-release.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not write .release-validators", stderr)

    def test_comment_stamped_source_verifier_is_rejected(self):
        text = self.mutated(
            '            "$PWD/.release-validators/scripts/verify-release.sh"',
            '            # "$PWD/.release-validators/scripts/verify-release.sh"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("pass the trusted", stderr)

    def test_echoed_shellcheck_install_is_rejected(self):
        text = self.mutated(
            "          timeout 300 sudo apt-get -o Acquire::Retries=3 install -y -qq shellcheck python3-yaml",
            "          echo timeout 300 sudo apt-get -o Acquire::Retries=3 install -y -qq shellcheck python3-yaml",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must install script-lint dependency 'shellcheck'", stderr)

    def test_weekly_true_stamped_completeness_is_rejected(self):
        old = (
            '              [[ "$draft" == "false" && "$prerelease" == "true" '
            '&& "$assets" == "$expected" ]]'
        )
        weekly = self.mutated_weekly(old, "              true  # " + old.strip())
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("completeness must be a code line starting with", stderr)

    def test_weekly_commented_completeness_is_rejected(self):
        old = (
            '              [[ "$draft" == "false" && "$prerelease" == "true" '
            '&& "$assets" == "$expected" ]]'
        )
        weekly = self.mutated_weekly(old, "              # " + old.strip())
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("completeness must be a code line starting with", stderr)

    # ------------------------------------------------------------------
    # Release artifact set: the exact nine assets are assembled and validated.
    # ------------------------------------------------------------------
    def artifacts_step(self) -> str:
        return (
            "      - name: Validate release artifact set\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            '        run: bash .release-validators/validate-release-artifacts.sh '
            './artifacts "${TAG#v}"\n\n'
        )

    def test_missing_artifact_set_validation_step_is_rejected(self):
        text = self.mutated(self.artifacts_step(), "")
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must run the staged artifact-set validator", stderr
        )

    def test_artifact_validation_after_tag_push_is_rejected(self):
        step = self.artifacts_step()
        text = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(step, text)
        text = text.replace(step, "", 1).rstrip("\n") + "\n\n" + step.rstrip("\n") + "\n"
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("in that order", stderr)

    def test_missing_nuget_config_copy_step_is_rejected(self):
        step = (
            "      - name: Copy NuGet config to artifacts\n"
            "        run: cp nuget.config.example ./artifacts/nuget.config\n\n"
        )
        text = self.mutated(step, "")
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must copy nuget.config.example", stderr)

    def test_missing_documentation_snapshot_step_is_rejected(self):
        step = (
            "      - name: Package documentation snapshot\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            '          VERSION="${TAG#v}"\n'
            '          DIRNAME="origo-docs-${VERSION}"\n'
            '          mkdir -p "${DIRNAME}/docs"\n'
            '          cp -r docs/. "${DIRNAME}/docs/"\n'
            '          cp AGENTS.md "${DIRNAME}/"\n'
            '          cp CHANGELOG.md "${DIRNAME}/"\n'
            '          cp Directory.Build.props "${DIRNAME}/"\n'
            "          mkdir -p ./artifacts\n"
            '          tar -czf "./artifacts/${DIRNAME}.tar.gz" "${DIRNAME}"\n'
            '          zip -rq "./artifacts/${DIRNAME}.zip" "${DIRNAME}"\n\n'
        )
        text = self.mutated(step, "")
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must build the documentation tar.gz and zip snapshot", stderr
        )

    def test_missing_pack_steps_are_rejected(self):
        projects = (
            "Origo.Core.Contracts/Origo.Core.Contracts.csproj",
            "Origo.Core.Kernel/Origo.Core.Kernel.csproj",
            "Origo.Core/Origo.Core.csproj",
            "Origo.GodotAdapter/Origo.GodotAdapter.csproj",
            "Origo.ConsoleBridge/Origo.ConsoleBridge.csproj",
        )
        for project in projects:
            package = project.split("/", 1)[0]
            step = (
                f"      - name: Pack {package}\n"
                f"        run: dotnet pack {project} --no-build "
                "--configuration Release --output ./artifacts\n\n"
            )
            with self.subTest(project=project):
                text = self.mutated(step, "")
                code, _, stderr = self.run_guard(text)
                self.assertEqual(code, 1)
                self.assertIn(f"must pack {project}", stderr)

    def test_missing_package_validator_step_is_rejected(self):
        step = (
            "      - name: Validate release package set\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            '        run: bash scripts/bash/validate-release-packages.sh '
            './artifacts "${TAG#v}"\n\n'
        )
        text = self.mutated(step, "")
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must run bash scripts/bash/validate-release-packages.sh", stderr
        )

    def test_missing_source_archive_step_is_rejected(self):
        step = (
            "      - name: Package release source tree\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            '          VERSION="${TAG#v}"\n'
            "          mkdir -p ./artifacts\n"
            "          git archive \\\n"
            "            --format=tar.gz \\\n"
            '            --prefix="origo-${VERSION}/" \\\n'
            '            --output="artifacts/origo-source-${VERSION}.tar.gz" \\\n'
            "            HEAD\n\n"
        )
        text = self.mutated(step, "")
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must build the source archive with git archive", stderr)



    # ------------------------------------------------------------------
    # Execution masking: control operators, comments, and hidden execution.
    # ------------------------------------------------------------------
    def test_semicolon_chained_required_command_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: bash scripts/bash/lint-scripts.sh ; true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run bash scripts/bash/lint-scripts.sh", stderr)

    def test_ampersand_chained_required_command_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: bash scripts/bash/lint-scripts.sh & true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run bash scripts/bash/lint-scripts.sh", stderr)

    def test_piped_required_command_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: bash scripts/bash/lint-scripts.sh | cat",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run bash scripts/bash/lint-scripts.sh", stderr)

    def test_command_substituted_required_command_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            '        run: bash scripts/bash/lint-scripts.sh "$(date)"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run bash scripts/bash/lint-scripts.sh", stderr)

    def test_required_step_with_if_is_rejected(self):
        text = self.mutated(
            "      - name: Lint scripts and workflows\n",
            "      - name: Lint scripts and workflows\n        if: false\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set 'if:'", stderr)

    def test_required_step_with_continue_on_error_is_rejected(self):
        text = self.mutated(
            "      - name: Verify release metadata\n",
            "      - name: Verify release metadata\n        continue-on-error: true\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set 'continue-on-error:'", stderr)

    def test_required_command_in_heredoc_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: |\n"
            "          cat <<'EOF'\n"
            "          bash scripts/bash/lint-scripts.sh\n"
            "          EOF",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not hide required commands in a heredoc", stderr)

    def test_required_command_in_if_false_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: |\n"
            "          if false; then\n"
            "            bash scripts/bash/lint-scripts.sh\n"
            "          fi",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("always-false branch", stderr)

    def test_required_command_after_and_chain_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: |\n"
            "          false &&\n"
            "          bash scripts/bash/lint-scripts.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not chain a required step", stderr)

    def test_required_command_in_function_definition_is_rejected(self):
        text = self.mutated(
            "        run: bash scripts/bash/lint-scripts.sh",
            "        run: |\n"
            "          run_lint() {\n"
            "            bash scripts/bash/lint-scripts.sh\n"
            "          }",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not wrap required commands", stderr)

    # ------------------------------------------------------------------
    # Trusted staging: writes into .release-validators outside reviewed lines.
    # ------------------------------------------------------------------
    def test_release_validators_new_writer_command_forms_are_rejected(self):
        # Red-first forms: each of these leaked a tag-tree copy into
        # .release-validators on the pre-hardening guard (verified against the
        # matching old guard + old workflow pair, where every form exited 0).
        # The writer-specific diagnostic keeps the test from passing on an
        # unrelated guard error.
        anchor = "          mkdir -p .release-validators/scripts\n"
        writers = (
            (
                "/bin/cp scripts/bash/verify-release.sh "
                ".release-validators/scripts/verify-release.sh",
                "must not write .release-validators with 'cp'",
            ),
            (
                "bash -c 'cp scripts/bash/verify-release.sh "
                ".release-validators/scripts/verify-release.sh'",
                "through 'bash' evaluation",
            ),
            (
                "tee .release-validators/scripts/verify-release.sh "
                "< scripts/bash/verify-release.sh",
                "must not write .release-validators with 'tee'",
            ),
            (
                "dd if=scripts/bash/verify-release.sh "
                "of=.release-validators/scripts/verify-release.sh",
                "must not write .release-validators with 'dd'",
            ),
            (
                "python3 -c \"open('.release-validators/scripts/"
                "verify-release.sh', 'w')\"",
                "must not write .release-validators with 'python3'",
            ),
            (
                "find . -name verify-release.sh -exec cp {} "
                ".release-validators/scripts/verify-release.sh +",
                "must not write .release-validators with 'find'",
            ),
        )
        for writer, diagnostic in writers:
            with self.subTest(writer=writer):
                text = self.mutated(anchor, anchor + "          " + writer + "\n")
                code, _, stderr = self.run_guard(text)
                self.assertEqual(code, 1)
                self.assertIn(diagnostic, stderr)

    def test_release_validators_legacy_writer_command_forms_are_rejected(self):
        # Already rejected by the earlier COPY_COMMANDS rule, so these are not
        # red-first for a new intent; kept as regression coverage.
        anchor = "          mkdir -p .release-validators/scripts\n"
        writers = (
            "install -m 644 scripts/bash/verify-release.sh "
            ".release-validators/scripts/verify-release.sh",
            "mv scripts/bash/verify-release.sh "
            ".release-validators/scripts/verify-release.sh",
            "rsync -a scripts/bash/verify-release.sh "
            ".release-validators/scripts/verify-release.sh",
            "ln -s scripts/bash/verify-release.sh "
            ".release-validators/scripts/verify-release.sh",
        )
        for writer in writers:
            with self.subTest(writer=writer):
                text = self.mutated(anchor, anchor + "          " + writer + "\n")
                code, _, stderr = self.run_guard(text)
                self.assertEqual(code, 1)
                self.assertIn(".release-validators", stderr)

    def test_release_validators_redirect_forms_are_rejected(self):
        # Redirect writes into .release-validators were already rejected before
        # the writer-command hardening; kept as regression coverage.
        anchor = "          mkdir -p .release-validators/scripts\n"
        writers = (
            "cat scripts/bash/verify-release.sh "
            "> .release-validators/scripts/verify-release.sh",
            "echo payload >> .release-validators/scripts/verify-release.sh",
        )
        for writer in writers:
            with self.subTest(writer=writer):
                text = self.mutated(anchor, anchor + "          " + writer + "\n")
                code, _, stderr = self.run_guard(text)
                self.assertEqual(code, 1)
                self.assertIn(".release-validators", stderr)

    # Artifact-set validator staging/invocation and weekly tail masking.
    def test_tag_tree_artifact_validator_invocation_is_rejected(self):
        text = self.mutated(
            '        run: bash .release-validators/validate-release-artifacts.sh '
            './artifacts "${TAG#v}"',
            '        run: bash scripts/bash/validate-release-artifacts.sh '
            './artifacts "${TAG#v}"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must not execute the tag-tree artifact-set validator", stderr
        )

    def test_missing_artifact_validator_staging_is_rejected(self):
        staging = (
            "          git show "
            "refs/remotes/origin/main:scripts/bash/validate-release-artifacts.sh "
            "> .release-validators/validate-release-artifacts.sh\n"
        )
        text = self.mutated(staging, "")
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must fetch scripts/bash/validate-release-artifacts.sh", stderr
        )
        self.assertIn(
            "must stage the trusted validator", stderr
        )

    def test_weekly_masked_completeness_is_rejected(self):
        old = (
            '              [[ "$draft" == "false" && "$prerelease" == "true" '
            '&& "$assets" == "$expected" ]]'
        )
        weekly = self.mutated_weekly(old, old + " || true")
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("completeness must be a code line", stderr)

    def test_weekly_trailing_semicolon_completeness_is_rejected(self):
        old = (
            '              [[ "$draft" == "false" && "$prerelease" == "true" '
            '&& "$assets" == "$expected" ]]'
        )
        weekly = self.mutated_weekly(old, old + "; true")
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("completeness must be a code line", stderr)

    # Job/step conditions, working-directory writes, invocation variants, and
    # heredoc/early-exit weekly masking.
    def test_release_job_if_is_rejected(self):
        text = self.mutated(
            "  release:\n    needs: godot-integration-tests\n    runs-on: ubuntu-latest\n",
            "  release:\n    needs: godot-integration-tests\n    runs-on: ubuntu-latest\n"
            "    if: false\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("release job must not set 'if:'", stderr)

    def test_release_job_continue_on_error_is_rejected(self):
        text = self.mutated(
            "  release:\n    needs: godot-integration-tests\n    runs-on: ubuntu-latest\n",
            "  release:\n    needs: godot-integration-tests\n    runs-on: ubuntu-latest\n"
            "    continue-on-error: true\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("release job must not set 'continue-on-error:'", stderr)

    def test_weekly_job_if_is_rejected(self):
        weekly = self.mutated_weekly(
            "  weekly-build:\n    runs-on: ubuntu-latest\n",
            "  weekly-build:\n    runs-on: ubuntu-latest\n    if: false\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("weekly job 'weekly-build' must not set 'if:'", stderr)

    def test_weekly_job_continue_on_error_is_rejected(self):
        weekly = self.mutated_weekly(
            "  weekly-build:\n    runs-on: ubuntu-latest\n",
            "  weekly-build:\n    runs-on: ubuntu-latest\n"
            "    continue-on-error: true\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn(
            "weekly job 'weekly-build' must not set 'continue-on-error:'", stderr
        )

    def test_cd_into_release_validators_is_rejected(self):
        anchor = "          mkdir -p .release-validators/scripts\n"
        text = self.mutated(
            anchor,
            anchor + "          cd .release-validators\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not change directory into .release-validators", stderr)

    def test_relative_write_after_cd_into_release_validators_is_rejected(self):
        anchor = "          mkdir -p .release-validators/scripts\n"
        text = self.mutated(
            anchor,
            anchor
            + "          cd .release-validators\n"
            + "          echo payload > verify-release-provenance.sh\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not write .release-validators from a changed", stderr)

    def test_relative_write_after_pushd_into_release_validators_is_rejected(self):
        anchor = "          mkdir -p .release-validators/scripts\n"
        text = self.mutated(
            anchor,
            anchor
            + "          pushd .release-validators/scripts\n"
            + "          echo payload > verify-release.sh\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not change directory into .release-validators", stderr)

    def test_tag_tree_validator_invocation_variants_are_rejected(self):
        anchor = "          mkdir -p .release-validators/scripts\n"
        variants = (
            "bash -x scripts/bash/verify-release.sh",
            "source scripts/bash/verify-release.sh",
            ". scripts/bash/verify-release.sh",
            "sh scripts/bash/verify-release.sh",
            "eval 'bash scripts/bash/verify-release.sh'",
            "command bash scripts/bash/verify-release.sh",
            "xargs -0 bash scripts/bash/verify-release.sh",
            "nice -n 5 bash scripts/bash/verify-release.sh",
        )
        for variant in variants:
            with self.subTest(variant=variant):
                text = self.mutated(anchor, anchor + "          " + variant + "\n")
                code, _, stderr = self.run_guard(text)
                self.assertEqual(code, 1)
                self.assertIn(
                    "must execute staged validators from .release-validators",
                    stderr,
                )

    def test_flagged_staged_validator_invocation_is_rejected(self):
        text = self.mutated(
            "          bash .release-validators/scripts/verify-release.sh",
            "          bash -x .release-validators/scripts/verify-release.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must execute the trusted", stderr)

    def test_weekly_completeness_in_heredoc_is_rejected(self):
        completeness = (
            '              [[ "$draft" == "false" && "$prerelease" == "true" '
            '&& "$assets" == "$expected" ]]'
        )
        heredoc_anchor = (
            "          print(previous_monday.strftime('%Y-%m-%dT%H:%M:%SZ'))\n"
        )
        weekly = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(completeness + "\n", weekly)
        weekly = weekly.replace(completeness + "\n", "", 1)
        self.assertIn(heredoc_anchor, weekly)
        weekly = weekly.replace(
            heredoc_anchor, heredoc_anchor + completeness + "\n", 1
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("completeness must be a code line", stderr)

    def test_weekly_marker_in_heredoc_is_rejected(self):
        marker_line = (
            '            echo "dispatch_existing=true" >> "$GITHUB_OUTPUT"\n'
        )
        heredoc_anchor = (
            "          print(previous_monday.strftime('%Y-%m-%dT%H:%M:%SZ'))\n"
        )
        weekly = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        self.assertEqual(weekly.count(marker_line), 2)
        weekly = weekly.replace(marker_line, "", 2)
        weekly = weekly.replace(
            heredoc_anchor, heredoc_anchor + marker_line, 1
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must check the GitHub release state", stderr)

    def test_weekly_early_exit_before_completeness_is_rejected(self):
        weekly = self.mutated_weekly(
            '          if [[ "${GITHUB_REF}" != "refs/heads/main" ]]; then\n',
            "          exit 0\n"
            '          if [[ "${GITHUB_REF}" != "refs/heads/main" ]]; then\n',
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("before any early exit", stderr)



    def test_weekly_dispatch_marker_in_heredoc_is_rejected(self):
        # A heredoc body in the dispatch step must not satisfy the orphan-loop
        # markers.
        dispatch_step = (
            "      - name: Dispatch the release pipeline for the snapshot tag\n"
            "        if: steps.check.outputs.new_version != '' || "
            "steps.check.outputs.dispatch_existing == 'true'\n"
            "        shell: bash\n"
            "        env:\n"
            "          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}\n"
            "          TAG: ${{ steps.check.outputs.tag }}\n"
            "        run: |\n"
        )
        weekly = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(dispatch_step, weekly)
        self.assertIn('while IFS= read -r ORPHAN_TAG; do', weekly)
        weekly = weekly.replace(
            "          while IFS= read -r ORPHAN_TAG; do",
            "          cat <<'EOF'\n"
            "          while IFS= read -r ORPHAN_TAG; do\n"
            "          EOF",
            1,
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must not use a heredoc", stderr)

    def test_weekly_bump_heredoc_is_rejected(self):
        # On the pre-hardening guard this form passed: the tag command opens a
        # heredoc whose body contains the push line (counted as the reviewed tag
        # push) and a ``#`` terminator line is comment-stripped, so the guard
        # believed the tag was pushed although the shell never executes the body.
        tag_line = (
            '          git tag -a "$TAG" -m "Weekly snapshot '
            "${{ steps.check.outputs.new_version }}"
            '"\n'
        )
        push_line = '          git push origin "refs/tags/${TAG}"\n'
        weekly = self.mutated_weekly(
            tag_line, tag_line.rstrip("\n") + " <<'#'\n"
        )
        self.assertEqual(weekly.count(push_line), 1)
        weekly = weekly.replace(push_line, push_line + "          #\n", 1)
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must not hide tag commands in a heredoc", stderr)


    # ------------------------------------------------------------------
    # Required-execution integrity: inert required commands.
    # ------------------------------------------------------------------
    def test_required_step_with_pwsh_block_comment_is_rejected(self):
        text = self.mutated(
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: bash scripts/bash/lint-scripts.sh",
            "      - name: Lint scripts and workflows\n"
            "        shell: pwsh\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            "          <#\n"
            "          bash scripts/bash/lint-scripts.sh\n"
            "          #>",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must use the default/Bash shell", stderr)

    def test_required_step_exit_zero_before_command_is_rejected(self):
        text = self.mutated(
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: bash scripts/bash/lint-scripts.sh",
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            "          exit 0\n"
            "          bash scripts/bash/lint-scripts.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not return success before its required command", stderr)

    def test_required_step_trap_exit_before_command_is_rejected(self):
        text = self.mutated(
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: bash scripts/bash/lint-scripts.sh",
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            "          trap 'exit 0' EXIT\n"
            "          bash scripts/bash/lint-scripts.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not install a trap", stderr)

    def test_required_command_in_runtime_false_branch_is_rejected(self):
        text = self.mutated(
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: bash scripts/bash/lint-scripts.sh",
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            "          if [ 1 -eq 2 ]; then\n"
            "            bash scripts/bash/lint-scripts.sh\n"
            "          fi",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not be inside a control-flow block", stderr)

    # ------------------------------------------------------------------
    # Documented release gates, ordering, and conditionals.
    # ------------------------------------------------------------------
    def test_missing_documented_gate_steps_are_rejected(self):
        steps = (
            "      - name: Format check\n"
            "        run: bash scripts/bash/format.sh\n\n",
            "      - name: CI — build, tests, line coverage gates (>= 90%)\n"
            "        run: bash scripts/bash/test.sh\n\n",
            "      - name: Benchmarks — TypedData + subsystem baselines\n"
            "        run: bash scripts/bash/benchmark.sh\n\n",
            "      - name: Shell-only package consumer smoke\n"
            "        run: bash scripts/bash/package-consumer-smoke.sh\n\n",
        )
        for step in steps:
            with self.subTest(step=step.splitlines()[0]):
                text = self.mutated(step, "")
                code, _, stderr = self.run_guard(text)
                self.assertEqual(code, 1)
                self.assertIn("must run bash scripts/bash/", stderr)

    def test_gate_step_conditionals_are_rejected(self):
        text = self.mutated(
            "      - name: Format check\n"
            "        run: bash scripts/bash/format.sh",
            "      - name: Format check\n"
            "        if: false\n"
            "        run: bash scripts/bash/format.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set 'if:'", stderr)

        text = self.mutated(
            "      - name: CI — build, tests, line coverage gates (>= 90%)\n"
            "        run: bash scripts/bash/test.sh",
            "      - name: CI — build, tests, line coverage gates (>= 90%)\n"
            "        continue-on-error: true\n"
            "        run: bash scripts/bash/test.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set 'continue-on-error:'", stderr)

    def test_release_job_requires_godot_needs(self):
        text = self.mutated(
            "  release:\n    needs: godot-integration-tests\n",
            "  release:\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must declare 'needs: godot-integration-tests'", stderr)

    def test_gate_after_tag_push_is_rejected(self):
        step = (
            "      - name: Format check\n"
            "        run: bash scripts/bash/format.sh\n\n"
        )
        text = self.mutated(step, "")
        self.assertIn(step, WORKFLOW.read_text(encoding="utf-8"))
        text = text.rstrip("\n") + "\n\n" + step.rstrip("\n") + "\n"
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("in that order", stderr)

    # ------------------------------------------------------------------
    # Artifact validator argv and post-validation artifact writes.
    # ------------------------------------------------------------------
    def test_artifact_validator_decoy_argv_is_rejected(self):
        text = self.mutated(
            "        run: bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"',
            "        run: bash .release-validators/validate-release-artifacts.sh "
            './decoy "${TAG#v}"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run exactly", stderr)

    def test_artifact_step_trailing_write_is_rejected(self):
        text = self.mutated(
            "        run: bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"',
            "        run: |\n"
            "          bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"\n'
            "          cp decoy ./artifacts/extra",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must not run commands after the artifact-set validator", stderr
        )

    def test_artifacts_write_after_validation_is_rejected(self):
        step = (
            "      - name: Validate release artifact set\n"
            "        env:\n"
            "          TAG: ${{ steps.resolve-tag.outputs.tag }}\n"
            '        run: bash .release-validators/validate-release-artifacts.sh '
            './artifacts "${TAG#v}"\n\n'
        )
        tamper = (
            "      - name: Tamper artifacts\n"
            "        run: cp decoy ./artifacts/extra\n\n"
        )
        text = self.mutated(step, step + tamper)
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must not write the artifacts directory after the artifact-set "
            "validation",
            stderr,
        )

    # ------------------------------------------------------------------
    # Weekly step conditions and early exits.
    # ------------------------------------------------------------------
    def test_weekly_check_step_if_is_rejected(self):
        weekly = self.mutated_weekly(
            "      - name: Check for commits in the weekly window\n"
            "        id: check\n",
            "      - name: Check for commits in the weekly window\n"
            "        id: check\n"
            "        if: false\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must not set 'if:'", stderr)

    def test_weekly_bump_condition_is_pinned(self):
        weekly = self.mutated_weekly(
            "      - name: Bump version, commit, and tag\n"
            "        if: steps.check.outputs.new_version != ''\n",
            "      - name: Bump version, commit, and tag\n"
            "        if: false\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must set 'if:' to exactly", stderr)

    def test_weekly_bump_continue_on_error_is_rejected(self):
        weekly = self.mutated_weekly(
            "      - name: Bump version, commit, and tag\n",
            "      - name: Bump version, commit, and tag\n"
            "        continue-on-error: true\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must not set 'continue-on-error:'", stderr)

    def test_weekly_dispatch_condition_arm_is_pinned(self):
        weekly = self.mutated_weekly(
            "        if: steps.check.outputs.new_version != '' || "
            "steps.check.outputs.dispatch_existing == 'true'\n",
            "        if: steps.check.outputs.dispatch_existing == 'true'\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must set 'if:' to exactly", stderr)

    def test_weekly_dispatch_exit_zero_is_rejected(self):
        weekly = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        marker = "      - name: Dispatch the release pipeline for the snapshot tag"
        start = weekly.index(marker)
        run_marker = "        run: |\n"
        run_index = weekly.index(run_marker, start) + len(run_marker)
        weekly = weekly[:run_index] + "          exit 0\n" + weekly[run_index:]
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must not exit before its work completes", stderr)

    def test_weekly_check_step_pwsh_shell_is_rejected(self):
        weekly = self.mutated_weekly(
            "      - name: Check for commits in the weekly window\n"
            "        id: check\n"
            "        shell: bash\n",
            "      - name: Check for commits in the weekly window\n"
            "        id: check\n"
            "        shell: pwsh\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must use the default/Bash shell", stderr)

    # ------------------------------------------------------------------
    # Heredoc variants hiding the weekly completeness assertion.
    # ------------------------------------------------------------------
    def hide_completeness_in_heredoc(self, heredoc_lines: tuple[str, ...]) -> str:
        completeness = (
            '              [[ "$draft" == "false" && "$prerelease" == "true" '
            '&& "$assets" == "$expected" ]]'
        )
        weekly = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(completeness + "\n", weekly)
        insertion = "".join(f"{line}\n" for line in heredoc_lines)
        return weekly.replace(completeness + "\n", insertion, 1)

    def test_weekly_completeness_in_backslash_heredoc_is_rejected(self):
        weekly = self.hide_completeness_in_heredoc(
            (
                r"          cat <<\EOF",
                '              [[ "$draft" == "false" && "$prerelease" == "true" '
                '&& "$assets" == "$expected" ]]',
                "          EOF",
            )
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("completeness must be a code line", stderr)

    def test_weekly_completeness_in_quoted_dash_heredoc_is_rejected(self):
        weekly = self.hide_completeness_in_heredoc(
            (
                "          cat <<'EOF-1'",
                '              [[ "$draft" == "false" && "$prerelease" == "true" '
                '&& "$assets" == "$expected" ]]',
                "          EOF-1",
            )
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("completeness must be a code line", stderr)

    def test_weekly_completeness_in_second_of_multiple_heredocs_is_rejected(self):
        weekly = self.hide_completeness_in_heredoc(
            (
                "          cat <<A <<B",
                "          first-body",
                "          A",
                '              [[ "$draft" == "false" && "$prerelease" == "true" '
                '&& "$assets" == "$expected" ]]',
                "          B",
            )
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("completeness must be a code line", stderr)

    # ------------------------------------------------------------------
    # .release-validators variable indirection.
    # ------------------------------------------------------------------
    def test_release_validators_variable_indirection_is_rejected(self):
        anchor = "          mkdir -p .release-validators/scripts\n"
        text = self.mutated(
            anchor,
            anchor
            + "          V=.release-validators\n"
            + '          cd "$V"\n'
            + "          printf payload > validate-release-provenance.sh\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must not reference .release-validators outside", stderr
        )



    def test_weekly_marker_in_backslash_heredoc_is_rejected(self):
        marker_line = (
            '            echo "dispatch_existing=true" >> "$GITHUB_OUTPUT"\n'
        )
        weekly = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        self.assertEqual(weekly.count(marker_line), 2)
        weekly = weekly.replace(marker_line, "", 2)
        anchor = "          ORPHAN_TAGS_FILE=\"${RUNNER_TEMP}/origo-orphan-tags.txt\"\n"
        self.assertIn(anchor, weekly)
        hidden = (
            r"          cat <<\EOF"
            "\n"
            + marker_line
            + "          EOF\n"
        )
        weekly = weekly.replace(anchor, anchor + hidden, 1)
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must check the GitHub release state", stderr)



    def test_required_step_conditional_exit_zero_is_rejected(self):
        text = self.mutated(
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: bash scripts/bash/lint-scripts.sh",
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            "          if [[ 1 -eq 1 ]]; then\n"
            "            exit 0\n"
            "          fi\n"
            "          bash scripts/bash/lint-scripts.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not return success before its required command", stderr)



    # ------------------------------------------------------------------
    # Final audit: marker name is not a conditional allowlist.
    # ------------------------------------------------------------------
    def test_renamed_snapshot_marker_does_not_allow_step_conditionals(self):
        text = self.mutated(
            "      - name: Format check\n"
            "        run: bash scripts/bash/format.sh",
            "      - name: Mark snapshot release as pre-release\n"
            "        if: false\n"
            "        run: bash scripts/bash/format.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertTrue(
            "must be unique" in stderr
            or "must define exactly one" in stderr
            or "must not set 'if:'" in stderr,
            stderr,
        )

    def test_snapshot_marker_content_is_pinned(self):
        text = self.mutated(
            '          gh release edit "${TAG}" --prerelease',
            "          true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run the reviewed command", stderr)

    # ------------------------------------------------------------------
    # Final audit: errexit disabling and trailing success commands.
    # ------------------------------------------------------------------
    def test_required_step_disable_errexit_is_rejected(self):
        text = self.mutated(
            "      - name: Format check\n"
            "        run: bash scripts/bash/format.sh",
            "      - name: Format check\n"
            "        run: |\n"
            "          set +e\n"
            "          bash scripts/bash/format.sh\n"
            "          true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not disable errexit", stderr)

    def test_required_command_must_be_last_executable_line(self):
        text = self.mutated(
            "      - name: Format check\n"
            "        run: bash scripts/bash/format.sh",
            "      - name: Format check\n"
            "        run: |\n"
            "          bash scripts/bash/format.sh\n"
            "          true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not run commands after its required command", stderr)

    def test_artifact_step_disable_errexit_is_rejected(self):
        text = self.mutated(
            "        run: bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"',
            "        run: |\n"
            "          set +e\n"
            "          bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not disable errexit", stderr)

    def test_artifact_step_trailing_command_is_rejected(self):
        text = self.mutated(
            "        run: bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"',
            "        run: |\n"
            "          bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"\n'
            "          set -x",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must not run commands after the artifact-set validator", stderr
        )

    # ------------------------------------------------------------------
    # Final audit: brace blocks and function-body hiding.
    # ------------------------------------------------------------------
    def test_required_command_in_brace_block_is_rejected(self):
        text = self.mutated(
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: bash scripts/bash/lint-scripts.sh",
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            "          false && {\n"
            "            bash scripts/bash/lint-scripts.sh\n"
            "          }\n"
            "          true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not be inside a control-flow block", stderr)

    def test_function_definition_with_argument_list_is_rejected(self):
        for declaration in ("function bash() {", "function bash {"):
            with self.subTest(declaration=declaration):
                text = self.mutated(
                    "      - name: Lint scripts and workflows\n"
                    "        env:\n"
                    "          ORIGO_BRANCH_NAME: "
                    "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
                    "        run: bash scripts/bash/lint-scripts.sh",
                    "      - name: Lint scripts and workflows\n"
                    "        env:\n"
                    "          ORIGO_BRANCH_NAME: "
                    "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
                    "        run: |\n"
                    f"          {declaration}\n"
                    "            bash scripts/bash/lint-scripts.sh\n"
                    "          }",
                )
                code, _, stderr = self.run_guard(text)
                self.assertEqual(code, 1)
                self.assertIn(
                    "must not wrap required commands in a function definition",
                    stderr,
                )

    # ------------------------------------------------------------------
    # Final audit: weekly variable-form early success exit.
    # ------------------------------------------------------------------
    def test_weekly_check_variable_exit_is_rejected(self):
        weekly = WEEKLY_WORKFLOW.read_text(encoding="utf-8")
        marker = "      - name: Check for commits in the weekly window"
        start = weekly.index(marker)
        run_marker = "        run: |\n"
        run_index = weekly.index(run_marker, start) + len(run_marker)
        weekly = (
            weekly[:run_index]
            + "          CODE=0\n"
            + '          exit "$CODE"\n'
            + weekly[run_index:]
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("must not exit before the orphan scan completes", stderr)

    # ------------------------------------------------------------------
    # Final audit: the PowerShell block-comment rule is independent of shell.
    # ------------------------------------------------------------------
    def test_powershell_block_comment_is_rejected_without_shell_override(self):
        text = self.mutated(
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: bash scripts/bash/lint-scripts.sh",
            "      - name: Lint scripts and workflows\n"
            "        env:\n"
            "          ORIGO_BRANCH_NAME: "
            "refs/tags/${{ steps.resolve-tag.outputs.tag }}\n"
            "        run: |\n"
            "          <#\n"
            "          bash scripts/bash/lint-scripts.sh\n"
            "          #>",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("PowerShell block comment", stderr)



    def test_snapshot_marker_continue_on_error_is_rejected(self):
        if_line = (
            "        if: ${{ inputs.snapshot || "
            "contains(steps.resolve-tag.outputs.tag, '-') }}\n"
        )
        text = self.mutated(
            if_line,
            if_line + "        continue-on-error: true\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set 'continue-on-error:'", stderr)



    def test_required_step_set_plus_o_errexit_is_rejected(self):
        text = self.mutated(
            "      - name: Format check\n"
            "        run: bash scripts/bash/format.sh",
            "      - name: Format check\n"
            "        run: |\n"
            "          set +o errexit\n"
            "          bash scripts/bash/format.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not disable errexit", stderr)

    def test_artifact_step_set_plus_o_errexit_is_rejected(self):
        text = self.mutated(
            "        run: bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"',
            "        run: |\n"
            "          set +o errexit\n"
            "          bash .release-validators/validate-release-artifacts.sh "
            './artifacts "${TAG#v}"',
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not disable errexit", stderr)


    # ------------------------------------------------------------------
    # Godot prerequisite job: the release needs: edge must point at a job
    # whose complete body still runs the reviewed gate.
    # ------------------------------------------------------------------
    def without_godot_job(self) -> str:
        text = WORKFLOW.read_text(encoding="utf-8")
        start = text.index("\n  godot-integration-tests:\n")
        end = text.index("\n  release:\n", start)
        return text[:start] + text[end:]

    def test_missing_godot_job_is_rejected(self) -> None:
        code, _, stderr = self.run_guard(self.without_godot_job())
        self.assertEqual(code, 1)
        self.assertIn(
            "must define the 'godot-integration-tests' job", stderr
        )

    def test_echoed_godot_gate_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Godot headless integration tests\n"
            "        run: bash scripts/bash/godot-test.sh",
            "      - name: Godot headless integration tests\n"
            "        run: echo bash scripts/bash/godot-test.sh",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run exactly the reviewed commands", stderr)

    def test_godot_gate_extra_command_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Godot headless integration tests\n"
            "        run: bash scripts/bash/godot-test.sh",
            "      - name: Godot headless integration tests\n"
            "        run: |\n"
            "          bash scripts/bash/godot-test.sh\n"
            "          true",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run exactly the reviewed commands", stderr)

    def test_godot_job_condition_is_rejected(self) -> None:
        text = self.mutated(
            "  godot-integration-tests:\n    runs-on: ubuntu-latest\n",
            "  godot-integration-tests:\n    runs-on: ubuntu-latest\n"
            "    if: false\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "the 'godot-integration-tests' job must not set 'if:'", stderr
        )

    def test_godot_job_continue_on_error_is_rejected(self) -> None:
        text = self.mutated(
            "  godot-integration-tests:\n    runs-on: ubuntu-latest\n",
            "  godot-integration-tests:\n    runs-on: ubuntu-latest\n"
            "    continue-on-error: true\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "the 'godot-integration-tests' job must not set "
            "'continue-on-error:'",
            stderr,
        )

    def test_godot_step_condition_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Godot headless integration tests\n",
            "      - name: Godot headless integration tests\n"
            "        if: false\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set 'if:'", stderr)

    def test_godot_step_continue_on_error_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Godot headless integration tests\n",
            "      - name: Godot headless integration tests\n"
            "        continue-on-error: true\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set 'continue-on-error:'", stderr)

    def test_godot_step_shell_override_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Godot headless integration tests\n",
            "      - name: Godot headless integration tests\n"
            "        shell: bash\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must use the default shell", stderr)

    def test_godot_step_env_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Godot headless integration tests\n",
            "      - name: Godot headless integration tests\n"
            "        env:\n"
            "          EXTRA: value\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set step 'env:'", stderr)

    def test_godot_checkout_ref_drift_is_rejected(self) -> None:
        text = self.mutated(
            "          ref: ${{ inputs.tag || github.ref }}",
            "          ref: main",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn(
            "must use exactly the reviewed action inputs", stderr
        )
        self.assertIn("inputs.tag || github.ref", stderr)

    def test_godot_cache_drift_is_rejected(self) -> None:
        text = self.mutated(
            "          path: .godot_binary",
            "          path: .godot_cache",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must use exactly the reviewed action inputs", stderr)

    def test_godot_apt_package_drift_is_rejected(self) -> None:
        text = self.mutated(
            "libxcursor1 libxinerama1 libxrandr2 libasound2t64 libpulse0 "
            "libgl1 libfontconfig1",
            "libxcursor1 libxinerama1 libxrandr2 libasound2t64 libgl1 "
            "libfontconfig1",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run exactly the reviewed commands", stderr)

    def test_godot_tag_layout_probe_drift_is_rejected(self) -> None:
        text = self.mutated(
            "for path in scripts/bash/godot-test.sh "
            "scripts/python/write-version-stamp.py; do",
            "for path in scripts/bash/godot-test.sh; do",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must run exactly the reviewed commands", stderr)

    def test_godot_unapproved_action_is_rejected(self) -> None:
        text = self.mutated(
            "        uses: actions/setup-dotnet@v6",
            "        uses: actions/setup-dotnet-legacy@v6",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("uses unapproved action", stderr)

    def test_top_level_defaults_is_rejected(self) -> None:
        text = self.mutated(
            "name: Release\n\n",
            "name: Release\n\ndefaults:\n  run:\n    shell: bash\n\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set top-level 'defaults:'", stderr)

    def test_top_level_env_is_rejected(self) -> None:
        text = self.mutated(
            "name: Release\n\n",
            "name: Release\n\nenv:\n  BASH_ENV: /tmp/evil\n\n",
        )
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1)
        self.assertIn("must not set top-level 'env:'", stderr)

    def test_weekly_job_env_is_rejected(self) -> None:
        weekly = self.mutated_weekly(
            "jobs:\n  weekly-build:\n    runs-on: ubuntu-latest\n",
            "jobs:\n  weekly-build:\n    runs-on: ubuntu-latest\n"
            "    env:\n      BAD: 1\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("weekly job 'weekly-build' must not set 'env:'", stderr)

    def test_weekly_top_level_env_is_rejected(self) -> None:
        weekly = self.mutated_weekly(
            "name: Weekly Build\n",
            "name: Weekly Build\nenv:\n  BASH_ENV: /tmp/evil\n",
        )
        code, _, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8"), weekly
        )
        self.assertEqual(code, 1)
        self.assertIn("weekly workflow must not set top-level 'env:'", stderr)


if __name__ == "__main__":
    unittest.main()
