#!/usr/bin/env python3
"""Focused tests for the local documentation generator/formatter."""

from __future__ import annotations

import importlib.util
import re
import unittest
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

    def test_contract_api_is_generated_from_baseline(self):
        rendered = MODULE.render_contract_api()
        self.assertTrue(rendered.startswith(MODULE.GENERATED_HEADER))
        self.assertIn("Origo.Core.Snd.ISndContext", rendered)
        self.assertIn("Origo.Core.Abstractions.Snd.ISndSaveOperations", rendered)

    def test_brittle_summary_patterns_are_rejected(self):
        for text in ("16 built-in commands", "10 companion properties", "101 tests", "5 minutes"):
            self.assertIsNotNone(
                next(
                    (match for pattern in MODULE.BANNED_SUMMARY_PATTERNS if (match := re.search(pattern, text))),
                    None,
                ),
                text,
            )

    def test_current_generated_content_and_links(self):
        self.assertEqual(MODULE.check_generated(), [])
        self.assertEqual(MODULE.validate_links(), [])

    def test_current_contractual_counts(self):
        self.assertEqual(MODULE.validate_counts(), [])


if __name__ == "__main__":
    unittest.main()
