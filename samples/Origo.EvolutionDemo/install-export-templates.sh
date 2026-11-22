#!/usr/bin/env bash
# Install the mono export templates for the pinned Godot version into the
# user-level Godot template directory. Release jobs call this before export.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

VERSION="$(bash scripts/bash/download-godot.sh --print-version)"
TEMPLATE_VERSION="${VERSION}.stable.mono"
TEMPLATE_DIR="${HOME}/.local/share/godot/export_templates/${TEMPLATE_VERSION}"
if [[ -f "${TEMPLATE_DIR}/linux_release.x86_64" \
    && -f "${TEMPLATE_DIR}/windows_release_x86_64.exe" \
    && -f "${TEMPLATE_DIR}/macos.zip" ]]; then
    echo "Godot export templates already installed: ${TEMPLATE_DIR}"
    exit 0
fi

ARCHIVE="Godot_v${VERSION}-stable_mono_export_templates.tpz"
CHECKSUMS="${ROOT}/samples/Origo.EvolutionDemo/export-templates.sha512"
if [[ ! -f "$CHECKSUMS" ]]; then
    echo "ERROR: missing pinned export-template checksums: $CHECKSUMS" >&2
    exit 1
fi
EXPECTED_SHA512="$(awk -v archive="$ARCHIVE" '$2 == archive { print $1; exit }' "$CHECKSUMS")"
if [[ -z "$EXPECTED_SHA512" ]]; then
    echo "ERROR: no pinned SHA-512 for $ARCHIVE in $CHECKSUMS" >&2
    echo "Update export-templates.sha512 with the Godot.NET.Sdk pin." >&2
    exit 1
fi
URL="https://github.com/godotengine/godot-builds/releases/download/${VERSION}-stable/${ARCHIVE}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
echo "Downloading Godot mono export templates: ${URL}"
if command -v curl >/dev/null 2>&1; then
    curl -fSL --retry 3 -o "${WORK}/${ARCHIVE}" "$URL"
elif command -v wget >/dev/null 2>&1; then
    wget -q -O "${WORK}/${ARCHIVE}" "$URL"
else
    echo "ERROR: curl or wget is required." >&2
    exit 1
fi
if command -v sha512sum >/dev/null 2>&1; then
    if ! printf '%s  %s\n' "$EXPECTED_SHA512" "${WORK}/${ARCHIVE}" | sha512sum -c -; then
        echo "ERROR: export-template SHA-512 mismatch for ${ARCHIVE}" >&2
        exit 1
    fi
elif command -v shasum >/dev/null 2>&1; then
    if ! printf '%s  %s\n' "$EXPECTED_SHA512" "${WORK}/${ARCHIVE}" | shasum -a 512 -c -; then
        echo "ERROR: export-template SHA-512 mismatch for ${ARCHIVE}" >&2
        exit 1
    fi
else
    echo "ERROR: sha512sum or shasum is required to verify export templates." >&2
    exit 1
fi
unzip -q "${WORK}/${ARCHIVE}" -d "${WORK}/extract"
mkdir -p "${TEMPLATE_DIR}"
cp -a "${WORK}/extract/templates/." "${TEMPLATE_DIR}/"
test -f "${TEMPLATE_DIR}/linux_release.x86_64"
test -f "${TEMPLATE_DIR}/windows_release_x86_64.exe"
test -f "${TEMPLATE_DIR}/macos.zip"
echo "Godot export templates installed: ${TEMPLATE_DIR}"
