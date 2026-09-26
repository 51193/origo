#!/usr/bin/env python3
"""Regression tests for scripts/validate-release-packages.sh.

Build synthetic but structurally real .nupkg fixtures, then exercise the
release artifact validator's happy path and its failure modes for missing
packages, version mismatches, dependency mismatches, analyzer loss, embedded
kernel assemblies, and unexpected packages. Run from scripts/lint-scripts.sh
so the release gate is covered by normal CI and local script lint.
"""

from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = ROOT / "scripts" / "validate-release-packages.sh"
VERSION = "1.2.3"
PACKAGE_IDS = (
    "Origo.Core.Contracts",
    "Origo.Core.Kernel",
    "Origo.Core",
    "Origo.GodotAdapter",
    "Origo.ConsoleBridge",
)
ANALYZER_ASSET = "analyzers/dotnet/cs/Origo.SourceGeneration.dll"


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def run_validator(artifacts: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["bash", str(VALIDATOR), str(artifacts), VERSION],
        text=True,
        capture_output=True,
        check=False,
    )


def write_package(
    directory: Path,
    package_id: str,
    version: str = VERSION,
    dependencies: tuple[tuple[str, str], ...] = (),
    extra_entries: tuple[str, ...] = (),
    include_analyzer: bool = False,
) -> None:
    dependency_xml = "".join(
        f'<dependency id="{dependency_id}" version="{dependency_version}"/>'
        for dependency_id, dependency_version in dependencies
    )
    nuspec = (
        '<?xml version="1.0" encoding="utf-8"?>'
        '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
        "<metadata>"
        f"<id>{package_id}</id>"
        f"<version>{version}</version>"
        f"<dependencies><group targetFramework=\"net10.0\">{dependency_xml}</group></dependencies>"
        "</metadata>"
        "</package>"
    )
    path = directory / f"{package_id}.{version}.nupkg"
    with zipfile.ZipFile(path, "w") as package:
        package.writestr(f"{package_id}.nuspec", nuspec)
        if include_analyzer:
            package.writestr(ANALYZER_ASSET, b"fixture")
        for entry in extra_entries:
            package.writestr(entry, b"fixture")


def write_valid_packages(directory: Path) -> None:
    write_package(directory, "Origo.Core.Contracts", include_analyzer=True)
    write_package(
        directory,
        "Origo.Core.Kernel",
        dependencies=(("Origo.Core.Contracts", VERSION),),
    )
    write_package(
        directory,
        "Origo.Core",
        dependencies=(
            ("Origo.Core.Contracts", VERSION),
            ("Origo.Core.Kernel", VERSION),
        ),
    )
    write_package(
        directory,
        "Origo.GodotAdapter",
        dependencies=(("Origo.Core", VERSION), ("GodotSharp", "4.7.2")),
    )
    write_package(
        directory,
        "Origo.ConsoleBridge",
        dependencies=(("Origo.Core.Contracts", VERSION),),
    )


def expect_success(artifacts: Path, label: str) -> None:
    result = run_validator(artifacts)
    if result.returncode != 0 or "Release package set OK: 5 packages" not in result.stdout:
        fail(f"{label} should pass:\nstdout={result.stdout}\nstderr={result.stderr}")


def expect_failure(artifacts: Path, label: str, message: str) -> None:
    result = run_validator(artifacts)
    if result.returncode != 1 or message not in result.stderr:
        fail(
            f"{label} should fail with '{message}':\n"
            f"stdout={result.stdout}\nstderr={result.stderr}"
        )


def main() -> int:
    if not VALIDATOR.is_file():
        fail(f"validator not found: {VALIDATOR}")

    with tempfile.TemporaryDirectory(prefix="origo-release-validator-tests.") as temp:
        base = Path(temp) / "valid"
        base.mkdir()
        write_valid_packages(base)
        expect_success(base, "valid package set")

        cases: list[tuple[str, str, callable]] = [
            (
                "missing package",
                "missing required package: Origo.ConsoleBridge",
                lambda case: (case / f"Origo.ConsoleBridge.{VERSION}.nupkg").unlink(),
            ),
            (
                "version mismatch",
                f"Origo.Core: package version 9.9.9 != release version {VERSION}",
                lambda case: (
                    (case / f"Origo.Core.{VERSION}.nupkg").unlink(),
                    write_package(case, "Origo.Core", version="9.9.9", dependencies=(
                        ("Origo.Core.Contracts", VERSION),
                        ("Origo.Core.Kernel", VERSION),
                    )),
                ),
            ),
            (
                "dependency mismatch",
                "Origo.Core.Kernel: dependency Origo.Core.Contracts version 9.9.9",
                lambda case: (
                    (case / f"Origo.Core.Kernel.{VERSION}.nupkg").unlink(),
                    write_package(case, "Origo.Core.Kernel", dependencies=(
                        ("Origo.Core.Contracts", "9.9.9"),
                    )),
                ),
            ),
            (
                "ConsoleBridge dependency direction",
                "Origo.ConsoleBridge: Origo dependency set ['Origo.Core'] "
                "!= expected ['Origo.Core.Contracts']",
                lambda case: (
                    (case / f"Origo.ConsoleBridge.{VERSION}.nupkg").unlink(),
                    write_package(
                        case,
                        "Origo.ConsoleBridge",
                        dependencies=(("Origo.Core", VERSION),),
                    ),
                ),
            ),
            (
                "missing analyzer",
                "Origo.Core.Contracts is missing analyzers/dotnet/cs/Origo.SourceGeneration.dll",
                lambda case: (
                    (case / f"Origo.Core.Contracts.{VERSION}.nupkg").unlink(),
                    write_package(case, "Origo.Core.Contracts", include_analyzer=False),
                ),
            ),
            (
                "embedded kernel assembly",
                "Origo.Core: kernel implementation assembly embedded",
                lambda case: (
                    (case / f"Origo.Core.{VERSION}.nupkg").unlink(),
                    write_package(
                        case,
                        "Origo.Core",
                        dependencies=(
                            ("Origo.Core.Contracts", VERSION),
                            ("Origo.Core.Kernel", VERSION),
                        ),
                        extra_entries=("lib/net10.0/Origo.Core.Kernel.dll",),
                    ),
                ),
            ),
            (
                "unexpected package",
                "unexpected package in release artifact set: Origo.Extra",
                lambda case: write_package(case, "Origo.Extra"),
            ),
        ]

        for case_name, expected_message, mutate in cases:
            case = Path(temp) / case_name.replace(" ", "-")
            shutil.copytree(base, case)
            mutate(case)
            expect_failure(case, case_name, expected_message)

    print("Release package validator: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
