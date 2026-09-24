#!/usr/bin/env bash
# Validate the packed release artifact set: identities, exact shell/kernel
# pairing, dependency direction, analyzer delivery, and kernel isolation.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

ARTIFACTS_DIR="${1:-./artifacts}"
VERSION="${2:-}"
[[ -n "$VERSION" ]] || { echo "ERROR: usage: $0 <artifacts-dir> <version>" >&2; exit 2; }
[[ -d "$ARTIFACTS_DIR" ]] || { echo "ERROR: artifacts directory not found: $ARTIFACTS_DIR" >&2; exit 2; }

python3 - "$ARTIFACTS_DIR" "$VERSION" <<'PY'
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

artifacts_dir, version = sys.argv[1], sys.argv[2]
expected = {
    "Origo.Core.Contracts",
    "Origo.Core.Kernel",
    "Origo.Core",
    "Origo.GodotAdapter",
    "Origo.ConsoleBridge",
}
failures = []


def nuspec(path):
    with zipfile.ZipFile(path) as package:
        name = next(n for n in package.namelist() if n.endswith(".nuspec"))
        return ET.fromstring(package.read(name))


def dependency_map(root):
    ns = {"n": root.tag.split("}")[0].strip("{")}
    result = {}
    for dep in root.findall(".//n:dependency", ns):
        dep_version = (dep.get("version") or "").strip()
        if dep_version.startswith("[") and dep_version.endswith("]"):
            dep_version = dep_version[1:-1]
        result[dep.get("id")] = {"version": dep_version, "exclude": dep.get("exclude") or ""}
    return result


packages = {}
for filename in os.listdir(artifacts_dir):
    if not filename.endswith(".nupkg") or filename.endswith(".symbols.nupkg"):
        continue
    path = os.path.join(artifacts_dir, filename)
    root = nuspec(path)
    package_id = root.find(".//{*}id").text
    package_version = root.find(".//{*}version").text
    if package_id in packages:
        failures.append(f"duplicate package id: {package_id}")
    packages[package_id] = (path, package_version, dependency_map(root))

for package_id in sorted(expected - set(packages)):
    failures.append(f"missing required package: {package_id}")
for package_id in sorted(set(packages) - expected):
    failures.append(f"unexpected package in release artifact set: {package_id}")

for package_id, (_, package_version, deps) in packages.items():
    if package_version != version:
        failures.append(f"{package_id}: package version {package_version} != release version {version}")

if "Origo.GodotAdapter.Kernel" in packages:
    failures.append("Origo.GodotAdapter.Kernel must not exist; Adapter is one shell package.")

pairing = {
    "Origo.ConsoleBridge": {"Origo.Core": version},
    "Origo.Core": {"Origo.Core.Contracts": version, "Origo.Core.Kernel": version},
    "Origo.Core.Kernel": {"Origo.Core.Contracts": version},
    "Origo.GodotAdapter": {"Origo.Core": version, "GodotSharp": "4.7.2"},
}
for package_id, requirements in pairing.items():
    if package_id not in packages:
        continue
    deps = packages[package_id][2]
    for dependency_id, required_version in requirements.items():
        if dependency_id not in deps:
            failures.append(f"{package_id}: missing dependency {dependency_id} {required_version}")
        elif deps[dependency_id]["version"] != required_version:
            failures.append(
                f"{package_id}: dependency {dependency_id} version "
                f"{deps[dependency_id]['version']} != {required_version}")

if "Origo.Core.Contracts" in packages:
    deps = packages["Origo.Core.Contracts"][2]
    if any(dep.startswith("Origo.") for dep in deps):
        failures.append(f"Origo.Core.Contracts must not depend on Origo packages: {sorted(deps)}")
    with zipfile.ZipFile(packages["Origo.Core.Contracts"][0]) as package:
        if not any(name == "analyzers/dotnet/cs/Origo.SourceGeneration.dll" for name in package.namelist()):
            failures.append("Origo.Core.Contracts is missing analyzers/dotnet/cs/Origo.SourceGeneration.dll")

for shell_package in ("Origo.Core", "Origo.GodotAdapter"):
    if shell_package not in packages:
        continue
    with zipfile.ZipFile(packages[shell_package][0]) as package:
        leaked = [n for n in package.namelist() if n.endswith("/Origo.Core.Kernel.dll")]
        if leaked:
            failures.append(f"{shell_package}: kernel implementation assembly embedded: {leaked}")

if failures:
    print("Release package validation FAILED:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    sys.exit(1)

print(f"Release package set OK: {len(packages)} packages at exact version {version}.")
PY
