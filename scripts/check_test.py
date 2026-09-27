#!/usr/bin/env python3
"""Focused tests for the change planner's pure classification rules."""

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("origo_check", ROOT / "check.py")
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules["origo_check"] = MODULE
SPEC.loader.exec_module(MODULE)


class PlannerTests(unittest.TestCase):
    def test_parse_name_status_keeps_both_rename_paths(self):
        changes = MODULE.parse_name_status("R100\tdocs/old.en.md\tdocs/new.en.md\n")
        self.assertEqual([change.path for change in changes], ["docs/old.en.md", "docs/new.en.md"])

    def test_reverse_test_closure_uses_evaluated_edges(self):
        graph = {"a": set(), "b.Tests.csproj": {"a"}, "c.Tests.csproj": {"b.Tests.csproj"}, "d": set()}
        self.assertEqual(MODULE.reverse_test_closure(graph, {"a"}), {"b.Tests.csproj", "c.Tests.csproj"})

    def test_full_contract_path_requires_full(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            projects = {
                "A/A.csproj": root / "A/A.csproj",
                "A.Tests/A.Tests.csproj": root / "A.Tests/A.Tests.csproj",
            }
            original_project_files = MODULE.project_files
            original_evaluated = MODULE.evaluated_references
            original_fingerprint = MODULE.fingerprint
            try:
                MODULE.project_files = lambda _root: projects
                MODULE.evaluated_references = lambda _root, _projects: {name: set() for name in projects}
                MODULE.fingerprint = lambda *_args: "test"
                plan = MODULE.classify(root, [MODULE.Change("M", "global.json")], "base")
            finally:
                MODULE.project_files = original_project_files
                MODULE.evaluated_references = original_evaluated
                MODULE.fingerprint = original_fingerprint
            self.assertTrue(plan["requiresFull"])

    def test_docs_require_full_without_doc_gate(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            projects = {"A.Tests/A.Tests.csproj": root / "A.Tests.csproj"}
            original_project_files = MODULE.project_files
            original_evaluated = MODULE.evaluated_references
            original_fingerprint = MODULE.fingerprint
            try:
                MODULE.project_files = lambda _root: projects
                MODULE.evaluated_references = lambda _root, _projects: {name: set() for name in projects}
                MODULE.fingerprint = lambda *_args: "test"
                plan = MODULE.classify(root, [MODULE.Change("M", "docs/META.en.md")], "base")
            finally:
                MODULE.project_files = original_project_files
                MODULE.evaluated_references = original_evaluated
                MODULE.fingerprint = original_fingerprint
            self.assertTrue(plan["requiresFull"])
            self.assertEqual(plan["additionalGates"], [])
            self.assertEqual(plan["selectedProjects"], [])
            self.assertNotIn("docs gate", " ".join(plan["reasons"]))

    def test_integration_project_is_run_by_godot_gate_not_vstest(self):
        self.assertFalse(MODULE.test_project("Origo.GodotAdapter.Integration.Tests/Origo.GodotAdapter.Integration.Tests.csproj"))

    def test_core_contracts_and_kernel_source_changes_select_consumer_tests(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contracts = "Origo.Core.Contracts/Origo.Core.Contracts.csproj"
            kernel = "Origo.Core.Kernel/Origo.Core.Kernel.csproj"
            core_tests = "Origo.Core.Tests/Origo.Core.Tests.csproj"
            projects = {
                contracts: root / "contracts.csproj",
                kernel: root / "kernel.csproj",
                core_tests: root / "core.tests.csproj",
            }
            graph = {contracts: set(), kernel: set(), core_tests: {contracts, kernel}}
            original_project_files = MODULE.project_files
            original_evaluated = MODULE.evaluated_references
            original_fingerprint = MODULE.fingerprint
            try:
                MODULE.project_files = lambda _root: projects
                MODULE.evaluated_references = lambda _root, _projects: graph
                MODULE.fingerprint = lambda *_args: "test"
                plan = MODULE.classify(
                    root,
                    [
                        MODULE.Change("A", "Origo.Core.Contracts/New.cs"),
                        MODULE.Change("D", "Origo.Core.Kernel/Old.cs"),
                    ],
                    "base",
                )
            finally:
                MODULE.project_files = original_project_files
                MODULE.evaluated_references = original_evaluated
                MODULE.fingerprint = original_fingerprint
            self.assertNotIn("docs gate", plan["additionalGates"])
            self.assertFalse(any(project.startswith("tools/") for project in plan["selectedProjects"]))
            self.assertIn(core_tests, plan["selectedProjects"])

    def test_empty_plan_requires_full(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            original_project_files = MODULE.project_files
            original_evaluated = MODULE.evaluated_references
            original_fingerprint = MODULE.fingerprint
            try:
                MODULE.project_files = lambda _root: {}
                MODULE.evaluated_references = lambda _root, _projects: {}
                MODULE.fingerprint = lambda *_args: "test"
                plan = MODULE.classify(root, [], "base")
            finally:
                MODULE.project_files = original_project_files
                MODULE.evaluated_references = original_evaluated
                MODULE.fingerprint = original_fingerprint
            self.assertTrue(plan["requiresFull"])
            self.assertIn("full", plan["reasons"][0])

    def test_quick_contract_rejects_benchmark_and_external_project(self):
        with self.assertRaises(RuntimeError):
            MODULE.validate_quick_filter("Category=Benchmark")
        with self.assertRaises(RuntimeError):
            MODULE.validate_quick_project("/tmp/foreign.csproj", {})

    def test_saved_plan_fingerprint_must_match(self):
        current = {"baseCommit": "base", "worktreeFingerprint": "new"}
        MODULE.validate_saved_plan(current, current)
        with self.assertRaises(RuntimeError):
            MODULE.validate_saved_plan(
                {"baseCommit": "base", "worktreeFingerprint": "old"}, current
            )

    def test_contracts_select_godot_and_full(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            projects = {
                "Origo.GodotAdapter/Origo.GodotAdapter.csproj": root / "adapter.csproj",
                "Origo.SourceGeneration/Origo.SourceGeneration.csproj": root / "source.csproj",
                "Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj": root / "source.tests.csproj",
                "Origo.Core.Tests/Origo.Core.Tests.csproj": root / "core.tests.csproj",
                "Origo.GodotAdapter.Tests/Origo.GodotAdapter.Tests.csproj": root / "adapter.tests.csproj",
            }
            original_project_files = MODULE.project_files
            original_evaluated = MODULE.evaluated_references
            original_fingerprint = MODULE.fingerprint
            try:
                MODULE.project_files = lambda _root: projects
                MODULE.evaluated_references = lambda _root, _projects: {name: set() for name in projects}
                MODULE.fingerprint = lambda *_args: "test"
                plan = MODULE.classify(
                    root,
                    [
                        MODULE.Change("A", "Origo.Core/NewThing.cs"),
                        MODULE.Change("M", "Origo.GodotAdapter/Bootstrap/Host.cs"),
                        MODULE.Change("M", "Origo.SourceGeneration/Generator.cs"),
                        MODULE.Change("M", "scripts/format.sh"),
                    ],
                    "base",
                )
            finally:
                MODULE.project_files = original_project_files
                MODULE.evaluated_references = original_evaluated
                MODULE.fingerprint = original_fingerprint
            self.assertTrue(plan["requiresFull"])
            self.assertEqual(plan["additionalGates"], ["godot"])
            self.assertIn("Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj", plan["selectedProjects"])
            self.assertIn("Origo.Core.Tests/Origo.Core.Tests.csproj", plan["selectedProjects"])
            self.assertIn("Origo.GodotAdapter.Tests/Origo.GodotAdapter.Tests.csproj", plan["selectedProjects"])


if __name__ == "__main__":
    unittest.main()
