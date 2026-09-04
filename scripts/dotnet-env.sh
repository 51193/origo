# shellcheck shell=bash
# Source-only environment bootstrap for repository scripts.
#
# Prefer the repository-local SDK installed under .dotnet/ (created by
# scripts/install-dotnet.sh) so scripts run even when the host shell has no
# dotnet-specific PATH entry. When the local SDK is absent, fall back to the
# system dotnet (CI uses actions/setup-dotnet).
#
# This file is sourced by other scripts; it never exports anything into the
# caller's interactive shell.

if [[ -z "${ROOT:-}" ]]; then
    ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fi

LOCAL_DOTNET="$ROOT/.dotnet/dotnet"

if [[ -x "$LOCAL_DOTNET" ]]; then
    export DOTNET_ROOT="$ROOT/.dotnet"
    export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT/.dotnet-cli-home}"
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
    mkdir -p "$DOTNET_CLI_HOME"
    export PATH="$ROOT/.dotnet:$PATH"

    # Use the repository-local NuGet cache only when the default user cache
    # is not writable (common in container/read-only-home environments).
    if [[ -z "${NUGET_PACKAGES:-}" ]]; then
        DEFAULT_CACHE="$HOME/.nuget/packages"
        if [[ -e "$DEFAULT_CACHE" ]]; then
            if [[ ! -w "$DEFAULT_CACHE" ]]; then
                export NUGET_PACKAGES="$ROOT/.nuget"
            fi
        elif [[ ! -w "$HOME" && ! -w "$HOME/.nuget" ]]; then
            export NUGET_PACKAGES="$ROOT/.nuget"
        fi
    fi
    mkdir -p "${NUGET_PACKAGES:-$DEFAULT_CACHE}"
    return 0
fi

if command -v dotnet >/dev/null 2>&1; then
    return 0
fi

echo "ERROR: no dotnet executable is available." >&2
echo "The repository-local SDK is missing ($LOCAL_DOTNET)" >&2
echo "and no system dotnet was found on PATH." >&2
echo "Run: bash scripts/install-dotnet.sh" >&2
return 1
