#!/usr/bin/env bash
# Validate the exact asset set attached to a release.
#
# The Release workflow publishes `./artifacts/*`; this validator proves the
# directory contains exactly the documented nine assets for the version before
# the tag moves or the GitHub Release is created. It is the formal-release
# counterpart of the weekly snapshot completeness check.
set -euo pipefail

ARTIFACTS_DIR="${1:-}"
VERSION="${2:-}"

if [[ -z "$ARTIFACTS_DIR" || -z "$VERSION" ]]; then
    echo "ERROR: usage: $0 <artifacts-dir> <version>" >&2
    exit 2
fi
if [[ ! -d "$ARTIFACTS_DIR" ]]; then
    echo "ERROR: release artifacts directory not found: $ARTIFACTS_DIR" >&2
    exit 2
fi
if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "ERROR: version '$VERSION' must be x.y.z or x.y.z-<suffix>." >&2
    exit 2
fi

expected=(
    "Origo.ConsoleBridge.${VERSION}.nupkg"
    "Origo.Core.${VERSION}.nupkg"
    "Origo.Core.Contracts.${VERSION}.nupkg"
    "Origo.Core.Kernel.${VERSION}.nupkg"
    "Origo.GodotAdapter.${VERSION}.nupkg"
    "origo-docs-${VERSION}.tar.gz"
    "origo-docs-${VERSION}.zip"
    "origo-source-${VERSION}.tar.gz"
    "nuget.config"
)

failures=()

is_expected() {
    local name="$1"
    local entry
    for entry in "${expected[@]}"; do
        if [[ "$name" == "$entry" ]]; then
            return 0
        fi
    done
    return 1
}

# Iterate only direct children of the artifacts directory: the release upload
# glob is ./artifacts/*, so an extra or nested entry must fail here instead of
# being attached silently.
while IFS= read -r path; do
    [[ -n "$path" ]] || continue
    name="${path##*/}"
    if [[ ! -f "$path" ]]; then
        failures+=("unexpected non-file artifact entry: $name")
        continue
    fi
    if ! is_expected "$name"; then
        failures+=("unexpected artifact: $name")
    fi
done < <(find "$ARTIFACTS_DIR" -mindepth 1 -maxdepth 1 -print)

for entry in "${expected[@]}"; do
    if [[ ! -f "$ARTIFACTS_DIR/$entry" ]]; then
        failures+=("missing artifact: $entry")
    fi
done

if [[ ${#failures[@]} -gt 0 ]]; then
    echo "Release artifact validation FAILED:" >&2
    for failure in "${failures[@]}"; do
        echo "  - $failure" >&2
    done
    exit 1
fi

echo "Release artifact set OK: ${#expected[@]} artifacts at version ${VERSION}."
