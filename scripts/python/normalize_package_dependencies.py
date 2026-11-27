#!/usr/bin/env python3
"""Wrap Origo package dependencies in exact NuGet version ranges.

The release validator promises exact shell/kernel pairing, but NuGet's
project-to-project packing emits bare minimum versions. This helper rewrites
the packed nuspec dependencies for ``Origo.*`` to ``[x.y.z]`` so restore
cannot silently combine a package with a different Origo version. Non-Origo
dependencies (for example GodotSharp) keep their minimum-version semantics.
"""

from __future__ import annotations

import os
import re
import sys
import zipfile
from pathlib import Path

_DEPENDENCY = re.compile(
    r'(<dependency\s+id="(?P<id>Origo\.[^"]+)"\s+version=")(?P<version>[^"]+)(")'
)


def normalize_package(path: Path, expected_version: str | None) -> int:
    """Rewrite one .nupkg in place; return the number of dependencies wrapped."""
    with zipfile.ZipFile(path) as package:
        entries = [(info, package.read(info.filename)) for info in package.infolist()]

    nuspec_names = [info.filename for info, _ in entries if info.filename.endswith(".nuspec")]
    if len(nuspec_names) != 1:
        raise SystemExit(f"ERROR: {path} must contain exactly one .nuspec, found {len(nuspec_names)}")
    nuspec_name = nuspec_names[0]
    nuspec = next(data for info, data in entries if info.filename == nuspec_name).decode("utf-8")

    package_version_match = re.search(r"<version>([^<]+)</version>", nuspec)
    package_version = package_version_match.group(1).strip() if package_version_match else None
    if expected_version is None:
        expected_version = package_version

    replaced = 0

    def replace(match: re.Match[str]) -> str:
        nonlocal replaced
        version = match.group("version")
        if expected_version is not None and version not in (expected_version, f"[{expected_version}]"):
            raise SystemExit(
                f"ERROR: {path}: {match.group('id')} dependency version {version} "
                f"!= package version {expected_version}"
            )
        if version.startswith("[") and version.endswith("]"):
            return match.group(0)
        replaced += 1
        return f'{match.group(1)}[{version}]{match.group(4)}'

    rewritten = _DEPENDENCY.sub(replace, nuspec)
    if replaced == 0:
        return 0

    temporary = path.with_name(path.name + ".tmp")
    try:
        with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED) as package:
            for info, data in entries:
                package.writestr(info, rewritten.encode("utf-8") if info.filename == nuspec_name else data)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)
    return replaced


def main() -> int:
    if len(sys.argv) not in (2, 3):
        print("Usage: normalize_package_dependencies.py <artifacts-dir> [version]", file=sys.stderr)
        return 2
    artifacts = Path(sys.argv[1])
    if not artifacts.is_dir():
        print(f"ERROR: artifacts directory not found: {artifacts}", file=sys.stderr)
        return 2
    expected_version = sys.argv[2] if len(sys.argv) == 3 else None
    packages = sorted(artifacts.glob("*.nupkg"))
    if not packages:
        print(f"ERROR: no .nupkg files found in {artifacts}", file=sys.stderr)
        return 1
    total = 0
    for package in packages:
        if package.name.endswith(".symbols.nupkg"):
            continue
        wrapped = normalize_package(package, expected_version)
        total += wrapped
        print(f"{package.name}: {wrapped} Origo dependency range(s) normalized")
    print(f"Normalized {total} Origo dependency range(s) in {len(packages)} package(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
