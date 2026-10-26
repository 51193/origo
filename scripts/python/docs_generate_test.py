#!/usr/bin/env python3
"""Focused tests for the local documentation generator/formatter."""

from __future__ import annotations

import importlib.util
import json
import re
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("origo_docs_generate", ROOT / "docs_generate.py")
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class DocsGenerateTests(unittest.TestCase):
    def test_type_declaration_normalization(self):
        self.assertEqual(
            MODULE.format_declaration("public sealed Class Origo.Core.Host :"),
            "public sealed class Origo.Core.Host",
        )
        self.assertEqual(
            MODULE.format_declaration("public Interface Origo.Core.ISndContext :"),
            "public interface Origo.Core.ISndContext",
        )

    def test_member_normalization(self):
        self.assertEqual(
            MODULE.format_member(
                "Origo.Core.Options.public int Port get:public set:public",
                "Origo.Core.Options",
            ),
            "public int Port { get; set; }",
        )
        self.assertEqual(
            MODULE.format_member(
                "Origo.Core.ISndContext.ISndSaveOperations Save get:public",
                "Origo.Core.ISndContext",
            ),
            "ISndSaveOperations Save { get; }",
        )

    def test_type_declaration_handles_readonly_and_record(self):
        self.assertEqual(
            MODULE.format_declaration("public readonly Struct Origo.Core.GridPos :"),
            "public readonly struct Origo.Core.GridPos",
        )
        self.assertEqual(
            MODULE.format_declaration("public sealed Record Origo.Core.OrigoMeta :"),
            "public sealed record Origo.Core.OrigoMeta",
        )

    def test_member_normalization_handles_init_and_protected(self):
        self.assertEqual(
            MODULE.format_member(
                "Origo.Core.X.public int X get:public init:public",
                "Origo.Core.X",
            ),
            "public int X { get; init; }",
        )
        self.assertEqual(
            MODULE.format_member(
                "Origo.Core.X.int X get:protected",
                "Origo.Core.X",
            ),
            "int X { get; }",
        )
        self.assertEqual(
            MODULE.format_member(
                "Origo.Core.X.T Get<T>(); where T : class",
                "Origo.Core.X",
            ),
            "T Get<T>() where T : class",
        )

    def test_contract_api_is_generated_from_baseline(self):
        rendered = MODULE.render_contract_api()
        self.assertTrue(rendered.startswith(MODULE.GENERATED_HEADER))
        self.assertIn("Origo.Core.Snd.ISndContext", rendered)
        self.assertIn("Origo.Core.Abstractions.Snd.ISndSaveOperations", rendered)

    def test_render_rejects_unmatched_baseline_entry(self):
        original = MODULE.BASELINE
        with tempfile.TemporaryDirectory() as directory:
            baseline = Path(directory) / "baseline.json"
            baseline.write_text(
                json.dumps(
                    {
                        "assemblies": [
                            {
                                "name": "Fixture",
                                "api": [
                                    "T:public sealed Class Origo.Fixture.Type : ",
                                    "M:Origo.Fixture.Unrelated.public void Missing()",
                                ],
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )
            MODULE.BASELINE = baseline
            try:
                with self.assertRaises(ValueError):
                    MODULE.render_contract_api()
            finally:
                MODULE.BASELINE = original

    def test_contract_api_covers_every_baseline_type_and_member(self):
        data = json.loads(MODULE.BASELINE.read_text(encoding="utf-8"))
        rendered = MODULE.render_contract_api()
        # Parse the raw inventory independently of the generator's regex/helpers.
        raw_type_re = re.compile(r"(Origo\.[A-Za-z0-9_.]+(?:<[^>]+>)?)(?: :|$)")
        expected_types: set[str] = set()
        expected_type_entries = 0
        expected_member_counts: dict[str, int] = {}
        for assembly in data["assemblies"]:
            entries = assembly["api"]
            for entry in entries:
                if entry.startswith("T:"):
                    match = raw_type_re.search(entry)
                    if match:
                        expected_types.add(match.group(1))
                        expected_type_entries += 1
            for entry in entries:
                if entry.startswith("T:"):
                    continue
                body = entry.split(":", 1)[-1]
                matches = [
                    type_name
                    for type_name in expected_types
                    if body.startswith(type_name + ".")
                ]
                if not matches or "<Clone>$" in body:
                    continue
                owner = max(matches, key=len)
                expected_member_counts[owner] = expected_member_counts.get(owner, 0) + 1
        sections = {
            type_name: body
            for type_name, body in re.findall(
                r"### `([^`]+)`\n\n```csharp\n(.*?)\n```", rendered, re.DOTALL
            )
        }
        self.assertEqual(set(sections), expected_types)
        self.assertEqual(len(sections), expected_type_entries)
        self.assertEqual(
            sum(len(body.splitlines()) - 1 for body in sections.values()),
            sum(expected_member_counts.values()),
        )
        for type_name in expected_types:
            section_lines = sections[type_name].splitlines()
            self.assertGreaterEqual(len(section_lines), 1)
            rendered_member_count = len(section_lines) - 1  # first line is declaration
            self.assertEqual(
                rendered_member_count,
                expected_member_counts.get(type_name, 0),
                type_name,
            )

    def test_summary_content_rejects_quick_start_reference(self):
        original = MODULE.SUMMARY_DOCS
        with tempfile.TemporaryDirectory(dir=MODULE.ROOT) as directory:
            path = Path(directory) / "summary.md"
            path.write_text("placeholder quick start\n", encoding="utf-8")
            MODULE.SUMMARY_DOCS = [path]
            try:
                errors = MODULE.validate_summary_content()
                self.assertTrue(any("quick-start" in error for error in errors))
            finally:
                MODULE.SUMMARY_DOCS = original

    def test_current_generated_content_and_links(self):
        self.assertEqual(MODULE.check_generated(), [])
        self.assertEqual(MODULE.validate_generated_registry(), [])
        self.assertEqual(MODULE.validate_links(), [])

    def test_current_contractual_counts(self):
        self.assertEqual(MODULE.validate_counts(), [])

    def test_validate_counts_rejects_missing_sources(self):
        original = MODULE.ROOT
        with tempfile.TemporaryDirectory() as directory:
            MODULE.ROOT = Path(directory)
            try:
                errors = MODULE.validate_counts()
                self.assertTrue(any("missing" in error for error in errors))
            finally:
                MODULE.ROOT = original

    def test_check_generated_rejects_stale_content(self):
        original_generated = MODULE.GENERATED
        original_artifacts = MODULE.GENERATED_ARTIFACTS
        with tempfile.TemporaryDirectory() as directory:
            stale = Path(directory) / "stale.md"
            stale.write_text("stale\n", encoding="utf-8")
            MODULE.GENERATED = stale
            MODULE.GENERATED_ARTIFACTS = {stale: lambda: "expected\n"}
            try:
                self.assertTrue(MODULE.check_generated())
            finally:
                MODULE.GENERATED = original_generated
                MODULE.GENERATED_ARTIFACTS = original_artifacts

    def test_validate_links_rejects_missing_repository_target(self):
        original = MODULE.ROOT
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "docs").mkdir()
            (root / "README.md").write_text(
                "[missing](https://github.com/51193/origo/blob/main/docs/missing.md)\n",
                encoding="utf-8",
            )
            MODULE.ROOT = root
            try:
                errors = MODULE.validate_links()
                self.assertTrue(any("repository link target" in error for error in errors))
            finally:
                MODULE.ROOT = original

    def test_generated_registry_rejects_unregistered_file(self):
        original_generated = MODULE.GENERATED
        original_artifacts = MODULE.GENERATED_ARTIFACTS
        with tempfile.TemporaryDirectory() as directory:
            generated_dir = Path(directory)
            (generated_dir / "unregistered.md").write_text("x\n", encoding="utf-8")
            MODULE.GENERATED = generated_dir / "registered.md"
            MODULE.GENERATED_ARTIFACTS = {}
            try:
                errors = MODULE.validate_generated_registry()
                self.assertTrue(any("not registered" in error for error in errors))
            finally:
                MODULE.GENERATED = original_generated
                MODULE.GENERATED_ARTIFACTS = original_artifacts

    def test_check_staged_rejects_untracked_registered_file(self):
        original_generated = MODULE.GENERATED
        original_artifacts = MODULE.GENERATED_ARTIFACTS
        with tempfile.TemporaryDirectory() as directory:
            artifact = Path(directory) / "artifact.md"
            artifact.write_text("expected\n", encoding="utf-8")
            MODULE.GENERATED = artifact
            MODULE.GENERATED_ARTIFACTS = {artifact: lambda: "expected\n"}
            completed = subprocess.CompletedProcess([], 1, "", "")
            try:
                with patch.object(MODULE.subprocess, "run", return_value=completed):
                    errors = MODULE.check_staged_generated()
                self.assertTrue(any("not staged or tracked" in error for error in errors))
            finally:
                MODULE.GENERATED = original_generated
                MODULE.GENERATED_ARTIFACTS = original_artifacts

    def test_check_staged_rejects_missing_registered_file(self):
        original_artifacts = MODULE.GENERATED_ARTIFACTS
        with tempfile.TemporaryDirectory() as directory:
            artifact = Path(directory) / "missing.md"
            MODULE.GENERATED_ARTIFACTS = {artifact: lambda: "expected\n"}
            completed = subprocess.CompletedProcess([], 1, "", "")
            try:
                with patch.object(MODULE.subprocess, "run", return_value=completed):
                    errors = MODULE.check_staged_generated()
                self.assertTrue(any("not staged or tracked" in error for error in errors))
            finally:
                MODULE.GENERATED_ARTIFACTS = original_artifacts

    def test_validate_links_rejects_missing_local_target(self):
        original = MODULE.ROOT
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "docs").mkdir()
            (root / "README.md").write_text("[missing](docs/missing.md)\n", encoding="utf-8")
            MODULE.ROOT = root
            try:
                errors = MODULE.validate_links()
                self.assertTrue(any("missing link target" in error for error in errors))
            finally:
                MODULE.ROOT = original


if __name__ == "__main__":
    unittest.main()
