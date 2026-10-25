#!/usr/bin/env bash
# Release metadata verification for the release workflow.
# Checks CHANGELOG/version-block alignment, [Unreleased] emptiness,
# analyzer release tracking, and the stamped build version. The manual links
# to Directory.Build.props instead of copying the version, so it is not a
# second version stamp.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

if [[ -z "${TAG_VERSION:-}" ]]; then
    echo "ERROR: TAG_VERSION is not set." >&2
    exit 2
fi

python3 - "$TAG_VERSION" <<'PY'
import datetime
import re
import sys

version = sys.argv[1]
if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?", version):
    print(
        f"ERROR: TAG_VERSION must be x.y.z or x.y.z-<suffix>; got '{version}'.",
        file=sys.stderr,
    )
    sys.exit(2)
if "-" in version:
    print(f"Release metadata verification SKIPPED for snapshot version {version}.")
    sys.exit(0)

numeric = version
parts = numeric.split(".")
assembly_version = ".".join(parts + ["0"])

failures = []

changelog = open("CHANGELOG.md", encoding="utf-8").read()
props = open("Directory.Build.props", encoding="utf-8").read()

def read_property(name):
    match = re.search(rf"<{name}>([^<]+)</{name}>", props)
    return match.group(1).strip() if match else None

if read_property("Version") != version:
    failures.append(
        f"Directory.Build.props <Version> is '{read_property('Version')}', expected '{version}'")
for element in ("AssemblyVersion", "FileVersion"):
    actual = read_property(element)
    if actual != assembly_version:
        failures.append(
            f"Directory.Build.props <{element}> is '{actual}', expected '{assembly_version}'")

version_heading_pattern = re.compile(
    rf"^## \[{re.escape(version)}\] - (\S+)\s*$", re.MULTILINE)
heading_match = version_heading_pattern.search(changelog)
if heading_match is None:
    failures.append(
        f"CHANGELOG.md has no version block heading '## [{version}] - YYYY-MM-DD'")
else:
    date_text = heading_match.group(1)
    if re.fullmatch(r"\d{4}-\d{2}-\d{2}", date_text) is None:
        failures.append(
            f"CHANGELOG.md version block heading '{heading_match.group(0).strip()}' "
            f"has an invalid date '{date_text}' (expected YYYY-MM-DD)")
    else:
        try:
            datetime.date.fromisoformat(date_text)
        except ValueError:
            failures.append(
                f"CHANGELOG.md version block heading '{heading_match.group(0).strip()}' "
                f"has an invalid date '{date_text}' (expected YYYY-MM-DD)")

unreleased = re.search(r"^## \[Unreleased\]\s*\n(.*?)(?=^## \[|\Z)", changelog, re.M | re.S)
if unreleased is None:
    failures.append("CHANGELOG.md has no [Unreleased] section")
elif [line for line in unreleased.group(1).splitlines() if line.strip()]:
    failures.append("CHANGELOG.md [Unreleased] section is not empty")

shipped = open("Origo.SourceGeneration/AnalyzerReleases.Shipped.md", encoding="utf-8").read()
if re.search(rf"^## Release {re.escape(version)}\s*$", shipped, re.MULTILINE) is None:
    failures.append(f"AnalyzerReleases.Shipped.md has no '## Release {version}' block")

unshipped = open("Origo.SourceGeneration/AnalyzerReleases.Unshipped.md", encoding="utf-8").read()
rules = [
    line for line in unshipped.splitlines()
    if re.match(r"^\s*\|?\s*ORIGOSG\d+\s*\|", line)
]
if rules:
    failures.append("AnalyzerReleases.Unshipped.md still contains unshipped rules")

if failures:
    print("Release metadata verification FAILED:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    sys.exit(1)

print(f"Release metadata verification PASSED for {version}.")
PY
