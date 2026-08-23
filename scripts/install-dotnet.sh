#!/usr/bin/env bash
# Install the exact .NET SDK version required by global.json.
#
# Policy (AGENTS.md §1.10): never lower the requested version to match an
# already-installed SDK. Prefer the user default install root ($HOME/.dotnet,
# dotnet-install's default) so no per-session PATH export is needed on a
# normally configured machine. When the default root is not writable, fall
# back to the repository-local .dotnet/ and use the repository root ./dotnet
# wrapper instead.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

REQUIRED_SDK=$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' global.json | head -1)
if [[ -z "$REQUIRED_SDK" ]]; then
    echo "ERROR: could not parse sdk.version from global.json." >&2
    exit 1
fi
if ! [[ "$REQUIRED_SDK" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "ERROR: parsed SDK version '$REQUIRED_SDK' is not N.N.N." >&2
    exit 1
fi

DEFAULT_INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
TARGET_DIR="$DEFAULT_INSTALL_DIR"
LOCAL_MODE=0

if [[ -e "$TARGET_DIR" ]]; then
    if [[ ! -w "$TARGET_DIR" ]]; then
        LOCAL_MODE=1
        TARGET_DIR="$ROOT/.dotnet"
    fi
elif [[ ! -w "$HOME" ]]; then
    LOCAL_MODE=1
    TARGET_DIR="$ROOT/.dotnet"
fi

INSTALLER="$ROOT/.dotnet-install.sh"
curl -fSL --retry 3 -o "$INSTALLER" https://dot.net/v1/dotnet-install.sh
trap 'rm -f "$INSTALLER"' EXIT

bash "$INSTALLER" --version "$REQUIRED_SDK" --install-dir "$TARGET_DIR" --no-path

echo ""
echo "Installed .NET SDK $REQUIRED_SDK to: $TARGET_DIR"
if [[ "$LOCAL_MODE" == "1" ]]; then
    echo "Repository-local mode: use './dotnet' from the repository root."
    echo "The repository scripts source scripts/dotnet-env.sh automatically."
else
    echo "Default mode: ensure 'dotnet' resolves from $TARGET_DIR on PATH"
    echo "through the system profile/package configuration; no per-session"
    echo "export is required."
fi
