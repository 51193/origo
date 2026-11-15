#!/usr/bin/env bash
# Bundle the five framework NuGet packages and nuget.config into a zip and a
# tar.gz. Individual packages remain attached to the release; this is the
# one-download convenience artifact for consumers that use the whole shell
# package set.
set -euo pipefail

ARTIFACTS_DIR="${1:-}"
VERSION="${2:-}"

if [[ -z "$ARTIFACTS_DIR" || -z "$VERSION" ]]; then
    echo "ERROR: usage: $0 <artifacts-dir> <version>" >&2
    exit 2
fi
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "ERROR: version '$VERSION' must be x.y.z or x.y.z-<suffix>." >&2
    exit 2
fi
if [[ ! -d "$ARTIFACTS_DIR" ]]; then
    echo "ERROR: artifacts directory not found: $ARTIFACTS_DIR" >&2
    exit 2
fi
# Resolve the caller's path before the archive commands run: they change the
# working directory to the temporary bundle directory, so a relative artifacts
# path would resolve there and no archive could be created.
ARTIFACTS_DIR="$(cd "$ARTIFACTS_DIR" && pwd)"

packages=(
    "Origo.ConsoleBridge.${VERSION}.nupkg"
    "Origo.Core.${VERSION}.nupkg"
    "Origo.Core.Contracts.${VERSION}.nupkg"
    "Origo.Core.Kernel.${VERSION}.nupkg"
    "Origo.GodotAdapter.${VERSION}.nupkg"
)
for package in "${packages[@]}"; do
    if [[ ! -f "${ARTIFACTS_DIR}/${package}" ]]; then
        echo "ERROR: missing package for framework bundle: ${package}" >&2
        exit 1
    fi
done
if [[ ! -f "${ARTIFACTS_DIR}/nuget.config" ]]; then
    echo "ERROR: missing nuget.config for framework bundle." >&2
    exit 1
fi

# GNU coreutils ships sha256sum; macOS ships shasum. Both write the standard
# "<hash>  <name>" manifest, so consumers can verify it with either tool.
if command -v sha256sum >/dev/null 2>&1; then
    SHA256=(sha256sum)
elif command -v shasum >/dev/null 2>&1; then
    SHA256=(shasum -a 256)
else
    echo "ERROR: sha256sum or shasum is required to build SHA256SUMS." >&2
    exit 1
fi

WORK="$(mktemp -d "${TMPDIR:-/tmp}/origo-framework-bundle.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT
for package in "${packages[@]}"; do
    cp "${ARTIFACTS_DIR}/${package}" "${WORK}/"
done
cp "${ARTIFACTS_DIR}/nuget.config" "${WORK}/"
cat > "${WORK}/README.txt" <<README
Origo framework packages ${VERSION}

This archive contains the complete framework package set used together:
$(printf '  %s\n' "${packages[@]}")
  nuget.config

Use nuget.config when restoring packages from a local or private feed.
SHA256SUMS lists the archive contents.
README
( cd "$WORK" && "${SHA256[@]}" "${packages[@]}" nuget.config > SHA256SUMS )
( cd "$WORK" && zip -q -r "${ARTIFACTS_DIR}/origo-framework-${VERSION}.zip" README.txt SHA256SUMS "${packages[@]}" nuget.config )
tar -C "$WORK" -czf "${ARTIFACTS_DIR}/origo-framework-${VERSION}.tar.gz" \
    README.txt SHA256SUMS "${packages[@]}" nuget.config

echo "Framework bundles:"
echo "  ${ARTIFACTS_DIR}/origo-framework-${VERSION}.zip"
echo "  ${ARTIFACTS_DIR}/origo-framework-${VERSION}.tar.gz"
unzip -Z1 "${ARTIFACTS_DIR}/origo-framework-${VERSION}.zip" | sort
