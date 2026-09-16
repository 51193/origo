#!/usr/bin/env bash
# Download the Godot mono binary matching the Godot.NET.Sdk version
# pinned in Origo.GodotAdapter.csproj.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

CSPROJ="$ROOT/Origo.GodotAdapter/Origo.GodotAdapter.csproj"
GODOT_CACHE_DIR="$ROOT/.godot_binary"

# Parse Godot.NET.Sdk version from csproj, e.g. "4.7.2".
# sed -E is used instead of grep -P because macOS ships BSD grep without PCRE.
VERSION=$(sed -nE 's#.*Godot\.NET\.Sdk/([0-9]+\.[0-9]+\.[0-9]+).*#\1#p' "$CSPROJ" | head -1)
if [[ -z "$VERSION" ]]; then
    echo "ERROR: Could not parse Godot.NET.Sdk version from $CSPROJ" >&2
    exit 1
fi

if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "ERROR: Parsed version '$VERSION' does not match expected N.N.N format." >&2
    exit 1
fi

echo "Godot.NET.Sdk version: $VERSION" >&2

# Determine platform-specific download
case "$(uname -s)" in
    Linux)
        ARCH=$(uname -m)
        if [[ "$ARCH" == "x86_64" ]]; then
            PLATFORM="linux_x86_64"
        elif [[ "$ARCH" == "aarch64" ]]; then
            PLATFORM="linux_arm64"
        else
            echo "ERROR: Unsupported Linux architecture: $ARCH" >&2
            exit 1
        fi
        ;;
    Darwin)
        PLATFORM="macos.universal"
        ;;
    CYGWIN*|MINGW*|MSYS*)
        PLATFORM="win64"
        ;;
    *)
        echo "ERROR: Unsupported OS: $(uname -s)" >&2
        exit 1
        ;;
esac

ARCHIVE="Godot_v${VERSION}-stable_mono_${PLATFORM}.zip"
DOWNLOAD_URL="https://github.com/godotengine/godot-builds/releases/download/${VERSION}-stable/${ARCHIVE}"
EXTRACT_DIR="${GODOT_CACHE_DIR}/${VERSION}"

if [[ -d "$EXTRACT_DIR" ]]; then
    GODOT_BIN=$(find "$EXTRACT_DIR" -maxdepth 2 -type f -name "Godot*" ! -name "*.pdb" ! -name "*.xml" ! -name "*.dll" ! -name "*.zip" | head -1)
    if [[ -n "$GODOT_BIN" ]] && [[ -x "$GODOT_BIN" ]]; then
        echo "Godot binary cached: $GODOT_BIN" >&2
        echo "$GODOT_BIN"
        exit 0
    fi
fi

echo "Downloading Godot $VERSION for $PLATFORM..." >&2
echo "URL: $DOWNLOAD_URL" >&2

mkdir -p "$EXTRACT_DIR"

if command -v curl &> /dev/null; then
    curl -fSL --progress-bar -o "$EXTRACT_DIR/$ARCHIVE" "$DOWNLOAD_URL"
elif command -v wget &> /dev/null; then
    wget -q --show-progress -O "$EXTRACT_DIR/$ARCHIVE" "$DOWNLOAD_URL"
else
    echo "ERROR: Neither curl nor wget found." >&2
    exit 1
fi

echo "Extracting..." >&2
unzip -qo "$EXTRACT_DIR/$ARCHIVE" -d "$EXTRACT_DIR"

GODOT_BIN=$(find "$EXTRACT_DIR" -maxdepth 2 -type f -name "Godot*" ! -name "*.pdb" ! -name "*.xml" ! -name "*.dll" ! -name "*.zip" | head -1)
if [[ -z "$GODOT_BIN" ]]; then
    echo "ERROR: Could not find Godot binary in extracted files." >&2
    ls -la "$EXTRACT_DIR" >&2
    exit 1
fi

chmod +x "$GODOT_BIN"
echo "Godot binary ready: $GODOT_BIN" >&2
echo "$GODOT_BIN"
