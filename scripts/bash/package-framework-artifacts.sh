#!/usr/bin/env bash
# Bundle the five framework NuGet packages and nuget.config into one zip.
# Individual packages remain attached to the release; this is the one-download
# convenience artifact for consumers that use the whole shell package set.
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
( cd "$WORK" && sha256sum "${packages[@]}" nuget.config > SHA256SUMS )
( cd "$WORK" && zip -q -r "${ARTIFACTS_DIR}/origo-framework-${VERSION}.zip" README.txt SHA256SUMS "${packages[@]}" nuget.config )
tar -C "$WORK" -czf "${ARTIFACTS_DIR}/origo-framework-${VERSION}.tar.gz" \
    README.txt SHA256SUMS "${packages[@]}" nuget.config

echo "Framework bundles:"
echo "  ${ARTIFACTS_DIR}/origo-framework-${VERSION}.zip"
echo "  ${ARTIFACTS_DIR}/origo-framework-${VERSION}.tar.gz"
unzip -Z1 "${ARTIFACTS_DIR}/origo-framework-${VERSION}.zip" | sort
