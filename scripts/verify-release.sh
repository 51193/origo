#!/usr/bin/env bash
# Release metadata verification for the release workflow.
# Checks CHANGELOG/version-block alignment, [Unreleased] emptiness,
# analyzer release tracking, and the manual version stamps.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [[ -z "${TAG_VERSION:-}" ]]; then
    echo "ERROR: TAG_VERSION is not set." >&2
    exit 2
fi

python3 - "$TAG_VERSION" <<'PY'
import re
import sys

version = sys.argv[1]
if "-" in version:
    print(f"Release metadata verification SKIPPED for snapshot version {version}.")
    sys.exit(0)

failures = []

changelog = open("CHANGELOG.md", encoding="utf-8").read()
version_heading = f"## [{version}] - "
if version_heading not in changelog:
    failures.append(f"CHANGELOG.md has no version block heading '{version_heading.strip()}'")

unreleased = re.search(r"^## \[Unreleased\]\s*\n(.*?)(?=^## \[|\Z)", changelog, re.M | re.S)
if unreleased is None:
    failures.append("CHANGELOG.md has no [Unreleased] section")
elif [line for line in unreleased.group(1).splitlines() if line.strip()]:
    failures.append("CHANGELOG.md [Unreleased] section is not empty")

shipped = open("Origo.SourceGeneration/AnalyzerReleases.Shipped.md", encoding="utf-8").read()
if f"## Release {version}" not in shipped:
    failures.append(f"AnalyzerReleases.Shipped.md has no '## Release {version}' block")

unshipped = open("Origo.SourceGeneration/AnalyzerReleases.Unshipped.md", encoding="utf-8").read()
rules = [line for line in unshipped.splitlines() if line.strip().startswith("| ORIGOSG")]
if rules:
    failures.append("AnalyzerReleases.Unshipped.md still contains unshipped rules")

for path in ("docs/README.zh.md", "docs/README.en.md"):
    text = open(path, encoding="utf-8").read()
    if version not in text:
        failures.append(f"{path} does not mention version {version}")

if failures:
    print("Release metadata verification FAILED:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    sys.exit(1)

print(f"Release metadata verification PASSED for {version}.")
PY
