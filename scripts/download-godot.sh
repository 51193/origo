#!/usr/bin/env bash
# Download the Godot mono binary matching the Godot.NET.Sdk version
# pinned in Origo.GodotAdapter.csproj.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

CSPROJ="$ROOT/Origo.GodotAdapter/Origo.GodotAdapter.csproj"
GODOT_CACHE_DIR="$ROOT/.godot_binary"

# Parse Godot.NET.Sdk version from csproj, e.g. "4.6.3"
VERSION=$(grep -oP 'Godot\.NET\.Sdk/\K[0-9]+\.[0-9]+\.[0-9]+' "$CSPROJ" | head -1)
if [[ -z "$VERSION" ]]; then
    echo "ERROR: Could not parse Godot.NET.Sdk version from $CSPROJ"
    exit 1
fi

echo "Godot.NET.Sdk version: $VERSION"

# Determine platform-specific download
case "$(uname -s)" in
    Linux)
        ARCH=$(uname -m)
        if [[ "$ARCH" == "x86_64" ]]; then
            PLATFORM="linux_x86_64"
            EXE_NAME="Godot_v${VERSION}-stable_mono_linux.x86_64"
        elif [[ "$ARCH" == "aarch64" ]]; then
            PLATFORM="linux_arm64"
            EXE_NAME="Godot_v${VERSION}-stable_mono_linux.arm64"
        else
            echo "ERROR: Unsupported Linux architecture: $ARCH"
            exit 1
        fi
        ;;
    Darwin)
        PLATFORM="macos.universal"
        EXE_NAME="Godot_mono.app/Contents/MacOS/Godot"
        ;;
    CYGWIN*|MINGW*|MSYS*)
        PLATFORM="win64"
        EXE_NAME="Godot_v${VERSION}-stable_mono_win64.exe"
        ;;
    *)
        echo "ERROR: Unsupported OS: $(uname -s)"
        exit 1
        ;;
esac

ARCHIVE="Godot_v${VERSION}-stable_mono_${PLATFORM}.zip"
DOWNLOAD_URL="https://github.com/godotengine/godot-builds/releases/download/${VERSION}-stable/${ARCHIVE}"
EXTRACT_DIR="${GODOT_CACHE_DIR}/${VERSION}"

if [[ -d "$EXTRACT_DIR" ]]; then
    GODOT_BIN=$(find "$EXTRACT_DIR" -type f -name "Godot*" ! -name "*.pdb" ! -name "*.xml" | head -1)
    if [[ -n "$GODOT_BIN" ]] && [[ -x "$GODOT_BIN" ]]; then
        echo "Godot binary cached: $GODOT_BIN"
        echo "$GODOT_BIN"
        exit 0
    fi
fi

echo "Downloading Godot $VERSION for $PLATFORM..."
echo "URL: $DOWNLOAD_URL"

mkdir -p "$EXTRACT_DIR"

if command -v curl &> /dev/null; then
    curl -fSL --progress-bar -o "$EXTRACT_DIR/$ARCHIVE" "$DOWNLOAD_URL"
elif command -v wget &> /dev/null; then
    wget -q --show-progress -O "$EXTRACT_DIR/$ARCHIVE" "$DOWNLOAD_URL"
else
    echo "ERROR: Neither curl nor wget found."
    exit 1
fi

echo "Extracting..."
unzip -qo "$EXTRACT_DIR/$ARCHIVE" -d "$EXTRACT_DIR"

# Find the Godot binary inside the extracted directory
GODOT_BIN=$(find "$EXTRACT_DIR" -type f -name "Godot*" ! -name "*.pdb" ! -name "*.xml" ! -name "*.zip" | head -1)
if [[ -z "$GODOT_BIN" ]]; then
    echo "ERROR: Could not find Godot binary in extracted files."
    ls -la "$EXTRACT_DIR"
    exit 1
fi

chmod +x "$GODOT_BIN"
echo "Godot binary ready: $GODOT_BIN"
echo "$GODOT_BIN"
