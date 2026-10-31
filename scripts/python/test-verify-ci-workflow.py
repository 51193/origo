#!/usr/bin/env python3
"""Regression tests for scripts/python/verify-ci-workflow.py.

The fixtures start from the real CI workflow and apply the negative mutations the
guard must reject, so each rule is pinned against the document it protects. Run
from ``scripts/bash/lint-scripts.sh`` and ``scripts/powershell/lint-scripts.ps1``.
"""

from __future__ import annotations

import contextlib
import importlib.util
import io
import re
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = ROOT / "scripts" / "python" / "verify-ci-workflow.py"
_SPEC = importlib.util.spec_from_file_location("verify_ci_workflow", MODULE_PATH)
assert _SPEC is not None and _SPEC.loader is not None
guard = importlib.util.module_from_spec(_SPEC)
sys.modules["verify_ci_workflow"] = guard
_SPEC.loader.exec_module(guard)

WORKFLOW = ROOT / ".github" / "workflows" / "ci.yml"


class CiWorkflowGuardTests(unittest.TestCase):
    def run_guard(self, text: str) -> tuple[int, str, str]:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            workflow = root / ".github" / "workflows" / "ci.yml"
            workflow.parent.mkdir(parents=True)
            workflow.write_text(text, encoding="utf-8")
            old_root, old_workflow = guard.ROOT, guard.WORKFLOW
            guard.ROOT, guard.WORKFLOW = root, workflow
            try:
                stdout = io.StringIO()
                stderr = io.StringIO()
                with (
                    contextlib.redirect_stdout(stdout),
                    contextlib.redirect_stderr(stderr),
                ):
                    code = guard.main()
                return code, stdout.getvalue(), stderr.getvalue()
            finally:
                guard.ROOT, guard.WORKFLOW = old_root, old_workflow

    def mutated(self, old: str, new: str) -> str:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(old, text, f"fixture anchor not found: {old!r}")
        return text.replace(old, new, 1)

    def removed_job(self, job: str) -> str:
        text = WORKFLOW.read_text(encoding="utf-8")
        lines = text.splitlines(keepends=True)
        start = next(
            index for index, line in enumerate(lines) if line == f"  {job}:\n"
        )
        end = start + 1
        while end < len(lines):
            if re.match(r"^  [A-Za-z0-9_-]+:\s*$", lines[end]):
                break
            end += 1
        else:
            self.fail(f"could not find the next job after {job!r}")
        return "".join(lines[:start] + lines[end:])

    def assert_rejected(self, text: str, diagnostic: str) -> None:
        code, _, stderr = self.run_guard(text)
        self.assertEqual(code, 1, stderr)
        self.assertIn(diagnostic, stderr)

    def test_current_workflow_passes(self) -> None:
        code, stdout, stderr = self.run_guard(
            WORKFLOW.read_text(encoding="utf-8")
        )
        self.assertEqual(code, 0, stderr)
        self.assertIn("CI workflow guard: OK", stdout)

    def test_deleted_required_job_is_rejected(self) -> None:
        text = self.removed_job("bash-checks")
        self.assertNotIn("\n  bash-checks:\n", text)
        self.assert_rejected(
            text, "ci workflow is missing required job 'bash-checks'"
        )

    def test_deleted_required_step_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Format check\n"
            "        run: bash scripts/bash/format.sh\n\n",
            "",
        )
        self.assert_rejected(text, "must define exactly 8 reviewed steps")

    def test_echoed_required_command_is_rejected(self) -> None:
        text = self.mutated(
            "        run: bash scripts/bash/test.sh",
            "        run: echo bash scripts/bash/test.sh",
        )
        self.assert_rejected(
            text, "must run exactly the reviewed commands"
        )

    def test_masked_required_command_is_rejected(self) -> None:
        text = self.mutated(
            "        run: bash scripts/bash/test.sh",
            "        run: bash scripts/bash/test.sh || true",
        )
        self.assert_rejected(
            text, "must run exactly the reviewed commands"
        )

    def test_step_if_false_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Lint repository scripts\n",
            "      - name: Lint repository scripts\n        if: false\n",
        )
        self.assert_rejected(text, "must not set 'if:'")

    def test_step_continue_on_error_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Lint repository scripts\n",
            "      - name: Lint repository scripts\n"
            "        continue-on-error: true\n",
        )
        self.assert_rejected(text, "must not set 'continue-on-error:'")

    def test_job_if_false_is_rejected(self) -> None:
        text = self.mutated(
            "  bash-lint:\n    runs-on: ubuntu-latest\n",
            "  bash-lint:\n    runs-on: ubuntu-latest\n    if: false\n",
        )
        self.assert_rejected(text, "ci job 'bash-lint' must not set 'if:'")

    def test_job_continue_on_error_is_rejected(self) -> None:
        text = self.mutated(
            "  bash-lint:\n    runs-on: ubuntu-latest\n",
            "  bash-lint:\n    runs-on: ubuntu-latest\n"
            "    continue-on-error: true\n",
        )
        self.assert_rejected(
            text, "ci job 'bash-lint' must not set 'continue-on-error:'"
        )

    def test_step_env_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Lint repository scripts\n",
            "      - name: Lint repository scripts\n"
            "        env:\n"
            "          EXTRA: value\n",
        )
        self.assert_rejected(text, "must not set step 'env:'")

    def test_step_working_directory_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: Lint repository scripts\n",
            "      - name: Lint repository scripts\n"
            "        working-directory: elsewhere\n",
        )
        self.assert_rejected(text, "must not set 'working-directory:'")

    def test_wrong_shell_is_rejected(self) -> None:
        text = self.mutated(
            "      - name: CI \u2014 build, tests, line coverage gates (>= 90%)\n",
            "      - name: CI \u2014 build, tests, line coverage gates (>= 90%)\n"
            "        shell: sh\n",
        )
        self.assert_rejected(text, "must use the default shell")

    def test_job_defaults_is_rejected(self) -> None:
        text = self.mutated(
            "  bash-lint:\n    runs-on: ubuntu-latest\n",
            "  bash-lint:\n    runs-on: ubuntu-latest\n"
            "    defaults:\n"
            "      run:\n"
            "        shell: bash\n",
        )
        self.assert_rejected(text, "must not set job 'defaults:'")

    def test_workflow_write_permissions_are_rejected(self) -> None:
        text = self.mutated(
            "permissions:\n  contents: read\n",
            "permissions:\n  contents: write\n",
        )
        self.assert_rejected(text, "must set exactly top-level permissions")

    def test_missing_workflow_permissions_are_rejected(self) -> None:
        text = self.mutated("permissions:\n  contents: read\n", "")
        self.assert_rejected(text, "must set exactly top-level permissions")

    def test_workflow_defaults_is_rejected(self) -> None:
        text = self.mutated(
            "name: CI\n\n",
            "name: CI\n\ndefaults:\n  run:\n    shell: bash\n\n",
        )
        self.assert_rejected(text, "must not set top-level 'defaults:'")

    def test_trigger_drift_is_rejected(self) -> None:
        text = self.mutated(
            "  pull_request:\n    branches: [main]\n",
            "  workflow_dispatch:\n",
        )
        self.assert_rejected(text, "must use exactly the reviewed triggers")

    def test_matrix_drift_is_rejected(self) -> None:
        text = self.mutated(
            "        os: [ubuntu-latest, windows-latest, macos-latest]",
            "        os: [ubuntu-latest]",
        )
        self.assert_rejected(text, "must use exactly the reviewed matrix")

    def test_checkout_input_drift_is_rejected(self) -> None:
        text = self.mutated(
            "          fetch-depth: 0",
            "          fetch-depth: 1",
        )
        self.assert_rejected(text, "must use exactly the reviewed action inputs")

    def test_action_input_drift_is_rejected(self) -> None:
        text = self.mutated(
            "          global-json-file: global.json",
            "          global-json-file: other.json",
        )
        self.assert_rejected(text, "must use exactly the reviewed action inputs")

    def test_dynamic_action_reference_is_rejected(self) -> None:
        text = self.mutated(
            "      - uses: actions/checkout@v7\n",
            "      - uses: ${{ env.CHECKOUT_ACTION }}\n",
        )
        self.assert_rejected(text, "must not use a dynamic action reference")

    def test_duplicate_job_key_is_rejected(self) -> None:
        text = (
            WORKFLOW.read_text(encoding="utf-8").rstrip("\n")
            + "\n\n  bash-lint:\n"
            "    runs-on: ubuntu-latest\n"
            "    steps:\n"
            "      - uses: actions/checkout@v7\n"
        )
        self.assert_rejected(text, "found duplicate key")

    def test_unexpected_job_is_rejected(self) -> None:
        text = (
            WORKFLOW.read_text(encoding="utf-8").rstrip("\n")
            + "\n\n  extra-job:\n"
            "    runs-on: ubuntu-latest\n"
            "    steps:\n"
            "      - uses: actions/checkout@v7\n"
        )
        self.assert_rejected(text, "must not define job 'extra-job'")


if __name__ == "__main__":
    unittest.main()
