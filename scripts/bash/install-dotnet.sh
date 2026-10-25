#!/usr/bin/env bash
# Install the .NET SDK version required by global.json.
#
# Policy (AGENTS.md, Environment Bootstrap): never lower the requested
# version to match an already-installed SDK. The install target is the SDK
# root that scripts/bash/dotnet-env.sh selects, in the same order: an explicit
# DOTNET_INSTALL_DIR, an existing repository-local .dotnet/, then the user
# default $HOME/.dotnet (the repository-local directory is the fallback when
# the user default is not installed or not writable); the environment script
# falls back to the dotnet on PATH when no SDK root is installed. An existing
# repository-local SDK is
# refreshed instead of leaving a stale one in place; the installed SDK is
# verified against global.json before the script reports success.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

if [[ ! -f global.json ]]; then
    echo "ERROR: global.json is missing; install-dotnet.sh needs its SDK pin." >&2
    exit 1
fi

REQUIRED_SDK=$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' global.json | head -1)
if [[ -z "$REQUIRED_SDK" ]]; then
    echo "ERROR: could not parse sdk.version from global.json." >&2
    exit 1
fi
if ! [[ "$REQUIRED_SDK" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "ERROR: parsed SDK version '$REQUIRED_SDK' is not N.N.N." >&2
    exit 1
fi

if [[ -n "${DOTNET_INSTALL_DIR:-}" ]]; then
    TARGET_DIR="$DOTNET_INSTALL_DIR"
    TARGET_MODE="custom"
elif [[ -e "$ROOT/.dotnet" ]]; then
    TARGET_DIR="$ROOT/.dotnet"
    TARGET_MODE="local"
elif [[ -n "${HOME:-}" && -e "$HOME/.dotnet" ]]; then
    TARGET_DIR="$HOME/.dotnet"
    TARGET_MODE="home"
elif [[ -n "${HOME:-}" && -w "$HOME" ]]; then
    TARGET_DIR="$HOME/.dotnet"
    TARGET_MODE="home"
else
    TARGET_DIR="$ROOT/.dotnet"
    TARGET_MODE="local"
fi

if [[ -e "$TARGET_DIR" && "$(cd "$TARGET_DIR" 2>/dev/null && pwd)" == "$ROOT" ]]; then
    echo "ERROR: the SDK target must be an SDK root, not the repository root." >&2
    echo "Set DOTNET_INSTALL_DIR to a directory that holds the SDK, or unset it." >&2
    exit 1
fi

CLI_HOME="${DOTNET_CLI_HOME:-$ROOT/.dotnet-cli-home}"
if ! mkdir -p "$CLI_HOME" 2>/dev/null; then
    CLI_HOME="${TMPDIR:-/tmp}/origo-dotnet-cli-home"
    mkdir -p "$CLI_HOME" 2>/dev/null || true
fi

if [[ -x "$TARGET_DIR/dotnet" ]] && (
    export DOTNET_ROOT="$TARGET_DIR"
    export DOTNET_CLI_HOME="$CLI_HOME"
    cd "$ROOT" && "$TARGET_DIR/dotnet" --version >/dev/null 2>&1
); then
    echo "The .NET SDK at $TARGET_DIR already satisfies $ROOT/global.json."
    echo "Nothing to install."
    exit 0
fi

if [[ -e "$TARGET_DIR" && ! -w "$TARGET_DIR" ]]; then
    if [[ "$TARGET_MODE" == "home" ]]; then
        echo "WARNING: '$TARGET_DIR' is not writable; using the repository-local SDK root instead." >&2
        TARGET_DIR="$ROOT/.dotnet"
        TARGET_MODE="local"
        if [[ -e "$TARGET_DIR" && ! -w "$TARGET_DIR" ]]; then
            echo "ERROR: the SDK target '$TARGET_DIR' is not writable." >&2
            echo "Fix its permissions, or choose another root with DOTNET_INSTALL_DIR." >&2
            exit 1
        fi
    else
        echo "ERROR: the SDK target '$TARGET_DIR' is not writable." >&2
        echo "Fix its permissions, or choose another root with DOTNET_INSTALL_DIR." >&2
        exit 1
    fi
fi

if ! command -v curl >/dev/null 2>&1; then
    echo "ERROR: curl is required to download the .NET installer." >&2
    exit 1
fi

INSTALLER="$ROOT/.dotnet-install.sh"
curl -fSL --retry 3 -o "$INSTALLER" https://dot.net/v1/dotnet-install.sh
trap 'rm -f "$INSTALLER"' EXIT

bash "$INSTALLER" --version "$REQUIRED_SDK" --install-dir "$TARGET_DIR" --no-path

if ! (
    export DOTNET_ROOT="$TARGET_DIR"
    export DOTNET_CLI_HOME="$CLI_HOME"
    cd "$ROOT" && "$TARGET_DIR/dotnet" --version >/dev/null 2>&1
); then
    echo "ERROR: the SDK installed at '$TARGET_DIR' does not satisfy $ROOT/global.json." >&2
    echo "Required SDK: $REQUIRED_SDK" >&2
    exit 1
fi

echo ""
echo "Installed .NET SDK $REQUIRED_SDK to: $TARGET_DIR"
case "$TARGET_MODE" in
    local)
        echo "Repository-local mode: repository scripts source"
        echo "scripts/bash/dotnet-env.sh and use this SDK automatically; './dotnet'"
        echo "from the repository root runs it too."
        ;;
    custom)
        echo "DOTNET_INSTALL_DIR mode: installed to $TARGET_DIR."
        echo "Scripts use this root only while DOTNET_INSTALL_DIR is exported;"
        echo "otherwise they select the repository-local or user default root."
        ;;
    home)
        echo "Default mode: repository scripts select $TARGET_DIR directly"
        echo "(scripts/bash/dotnet-env.sh); no PATH export is required."
        ;;
esac
