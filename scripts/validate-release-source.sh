#!/usr/bin/env bash
# Validate the release source archive attached to the GitHub Release.
#
# The archive is generated with `git archive` from the stamped release commit.
# This script proves that the version stamps survived archiving and, for a
# formal release, that the extracted tree passes the same metadata verifier as
# the working checkout.
set -euo pipefail
ARCHIVE="${1:-}"
VERSION="${2:-}"

VERIFY_SCRIPT="${3:-}"

if [[ -z "$ARCHIVE" || -z "$VERSION" ]]; then
    echo "ERROR: usage: $0 <source-archive> <version> [trusted-verify-script]" >&2
    exit 2
fi
if [[ ! -f "$ARCHIVE" ]]; then
    echo "ERROR: release source archive not found: $ARCHIVE" >&2
    exit 2
fi

TMP="$(mktemp -d "${TMPDIR:-/tmp}/origo-release-source.XXXXXX")"
cleanup() {
    rm -rf "$TMP"
}
trap cleanup EXIT

tar -xzf "$ARCHIVE" -C "$TMP"

props_files=()
while IFS= read -r path; do
    props_files+=("$path")
done < <(find "$TMP" -type f -name Directory.Build.props -print)

if [[ ${#props_files[@]} -ne 1 ]]; then
    echo "ERROR: source archive must contain exactly one Directory.Build.props; found ${#props_files[@]}." >&2
    exit 1
fi

PROPS="${props_files[0]}"
EXPECTED_ROOT="$TMP/origo-${VERSION}"
if [[ ! -d "$EXPECTED_ROOT" || "$PROPS" != "$EXPECTED_ROOT/Directory.Build.props" ]]; then
    echo "ERROR: source archive root must be 'origo-${VERSION}/'; got '${PROPS#"$TMP"/}'." >&2
    exit 1
fi
SOURCE_ROOT="$EXPECTED_ROOT"

python3 - "$PROPS" "$VERSION" <<'PY'
import re
import sys

props_path, version = sys.argv[1], sys.argv[2]
numeric = re.split(r"[-+]", version, maxsplit=1)[0]
parts = numeric.split(".")
if not parts or not all(part.isdigit() for part in parts) or len(parts) > 4:
    print(f"ERROR: version '{version}' has no usable numeric form.", file=sys.stderr)
    sys.exit(2)
assembly_version = ".".join((parts + ["0"] * 4)[:4])

content = open(props_path, encoding="utf-8").read()

def read_property(name):
    match = re.search(rf"<{name}>([^<]+)</{name}>", content)
    return match.group(1).strip() if match else None

failures = []
if read_property("Version") != version:
    failures.append(
        f"source archive <Version> is '{read_property('Version')}', expected '{version}'")
for element in ("AssemblyVersion", "FileVersion"):
    actual = read_property(element)
    if actual != assembly_version:
        failures.append(
            f"source archive <{element}> is '{actual}', expected '{assembly_version}'")

if failures:
    print("Release source archive validation FAILED:", file=sys.stderr)
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    sys.exit(1)

print(f"Release source archive version stamps OK for {version}.")
PY

if [[ "$VERSION" != *-* ]]; then
    if [[ -n "$VERIFY_SCRIPT" ]]; then
        cp "$VERIFY_SCRIPT" "$SOURCE_ROOT/scripts/verify-release.sh"
    fi
    (
        cd "$SOURCE_ROOT"
        TAG_VERSION="$VERSION" bash scripts/verify-release.sh
    )
else
    echo "Snapshot source archive ${VERSION}: formal metadata verification skipped."
fi

echo "Release source archive validation passed for $VERSION."
