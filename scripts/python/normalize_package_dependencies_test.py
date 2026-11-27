#!/usr/bin/env python3
"""Tests for the exact-range package normalizer."""

from __future__ import annotations

import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import normalize_package_dependencies as normalizer


def make_package(directory: Path, package_id: str, dependencies: str) -> Path:
    path = directory / f"{package_id}.1.2.3.nupkg"
    nuspec = f"""<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>{package_id}</id>
    <version>1.2.3</version>
    <dependencies>
      <group targetFramework="net10.0">
        {dependencies}
      </group>
    </dependencies>
  </metadata>
</package>"""
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as package:
        package.writestr(f"{package_id}.nuspec", nuspec)
        package.writestr("lib/net10.0/sample.dll", b"binary")
    return path


class NormalizePackageDependenciesTests(unittest.TestCase):
    def test_wraps_only_origo_dependencies(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = make_package(
                Path(temp),
                "Origo.Core",
                '<dependency id="Origo.Core.Contracts" version="1.2.3" />'
                '<dependency id="Origo.Core.Kernel" version="1.2.3" />'
                '<dependency id="GodotSharp" version="4.7.2" />',
            )
            self.assertEqual(2, normalizer.normalize_package(path, "1.2.3"))
            with zipfile.ZipFile(path) as package:
                nuspec = package.read("Origo.Core.nuspec").decode()
            self.assertIn('id="Origo.Core.Contracts" version="[1.2.3]"', nuspec)
            self.assertIn('id="Origo.Core.Kernel" version="[1.2.3]"', nuspec)
            self.assertIn('id="GodotSharp" version="4.7.2"', nuspec)
            self.assertEqual(0, normalizer.normalize_package(path, "1.2.3"))

    def test_version_mismatch_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = make_package(
                Path(temp),
                "Origo.Core",
                '<dependency id="Origo.Core.Contracts" version="1.2.2" />',
            )
            with self.assertRaises(SystemExit):
                normalizer.normalize_package(path, "1.2.3")

    def test_without_expected_version_uses_package_version(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = make_package(
                Path(temp),
                "Origo.Core",
                '<dependency id="Origo.Core.Contracts" version="1.2.2" />',
            )
            with self.assertRaises(SystemExit):
                normalizer.normalize_package(path, None)

    def test_preserves_non_nuspec_entries(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = make_package(
                Path(temp),
                "Origo.Core",
                '<dependency id="Origo.Core.Contracts" version="1.2.3" />',
            )
            normalizer.normalize_package(path, "1.2.3")
            with zipfile.ZipFile(path) as package:
                self.assertEqual(b"binary", package.read("lib/net10.0/sample.dll"))


if __name__ == "__main__":
    unittest.main()
