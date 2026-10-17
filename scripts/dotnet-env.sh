# shellcheck shell=bash
# Source-only environment bootstrap for repository scripts.
#
# Selects the SDK root the same way scripts/install-dotnet.sh installs it: an
# explicit DOTNET_INSTALL_DIR, then a repository-local .dotnet/, then the user
# default $HOME/.dotnet, and finally the dotnet on PATH (CI uses
# actions/setup-dotnet). The selected SDK is validated against global.json; a
# stale selection fails fast with the fix command instead of letting the host
# suggest editing global.json.
#
# This file is sourced by other scripts; it never exports anything into the
# caller's interactive shell.

if [[ -z "${ROOT:-}" ]]; then
    ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fi

if [[ ! -f "$ROOT/global.json" ]]; then
    echo "ERROR: $ROOT/global.json is missing; repository scripts need its SDK pin." >&2
    return 1
fi

# The CLI home must be writable for the validation probe and the SDK run; a
# read-only checkout falls back to the temp directory instead of failing here.
_origo_cli_home="$ROOT/.dotnet-cli-home"
if ! mkdir -p "$_origo_cli_home" 2>/dev/null; then
    _origo_cli_home="${TMPDIR:-/tmp}/origo-dotnet-cli-home"
    mkdir -p "$_origo_cli_home" 2>/dev/null || true
fi
_origo_sdk_root=""
_origo_dotnet=""

if [[ -n "${DOTNET_INSTALL_DIR:-}" ]]; then
    # Resolve a relative root against the repository, matching the installer
    # (which runs from the repository root before installing).
    case "$DOTNET_INSTALL_DIR" in
        /* | [A-Za-z]:/* | [A-Za-z]:\\*) _origo_explicit_root="$DOTNET_INSTALL_DIR" ;;
        *) _origo_explicit_root="$ROOT/$DOTNET_INSTALL_DIR" ;;
    esac
    _origo_sdk_root="$_origo_explicit_root"
    _origo_dotnet="$_origo_explicit_root/dotnet"
    if [[ "$(cd "$(dirname "$_origo_dotnet")" 2>/dev/null && pwd)/dotnet" == "$ROOT/dotnet" ]]; then
        echo "ERROR: DOTNET_INSTALL_DIR must point to an SDK root, not the repository wrapper." >&2
        echo "Run: bash \"$ROOT/scripts/install-dotnet.sh\"" >&2
        return 1
    fi
elif [[ -e "$ROOT/.dotnet" ]]; then
    _origo_sdk_root="$ROOT/.dotnet"
    _origo_dotnet="$ROOT/.dotnet/dotnet"
elif [[ -n "${HOME:-}" && -x "$HOME/.dotnet/dotnet" ]]; then
    _origo_sdk_root="$HOME/.dotnet"
    _origo_dotnet="$HOME/.dotnet/dotnet"
else
    _origo_dotnet="$(command -v dotnet 2>/dev/null || true)"
    # Skip the repository wrapper itself (a PATH entry may contain the
    # repository root); running it from here would recurse into this script.
    if [[ -n "$_origo_dotnet" ]] &&
        [[ "$(cd "$(dirname "$_origo_dotnet")" 2>/dev/null && pwd)/$(basename "$_origo_dotnet")" == "$ROOT/dotnet" ]]; then
        _origo_dotnet=""
        IFS=':' read -r -a _origo_path_entries <<<"$PATH"
        for _origo_dir in "${_origo_path_entries[@]}"; do
            [[ -n "$_origo_dir" ]] || continue
            [[ "$(cd "$_origo_dir" 2>/dev/null && pwd)/dotnet" == "$ROOT/dotnet" ]] && continue
            if [[ -x "$_origo_dir/dotnet" ]]; then
                _origo_dotnet="$_origo_dir/dotnet"
                break
            fi
        done
        unset _origo_path_entries _origo_dir
    fi
fi

if [[ -z "$_origo_dotnet" ]]; then
    echo "ERROR: no dotnet executable is available." >&2
    echo "The repository-local SDK is missing ($ROOT/.dotnet/dotnet)" >&2
    echo "and no system dotnet was found on PATH." >&2
    echo "Run: bash \"$ROOT/scripts/install-dotnet.sh\"" >&2
    return 1
fi

if [[ ! -x "$_origo_dotnet" ]]; then
    if [[ -n "${DOTNET_INSTALL_DIR:-}" ]]; then
        echo "ERROR: DOTNET_INSTALL_DIR is set to '$DOTNET_INSTALL_DIR', but it contains no executable dotnet." >&2
    else
        echo "ERROR: '$_origo_dotnet' is not executable." >&2
    fi
    echo "Run: bash \"$ROOT/scripts/install-dotnet.sh\"" >&2
    return 1
fi

# Ask the selected SDK itself whether global.json is satisfied: the host
# applies rollForward and allowPrerelease, so this check cannot drift from
# the real resolution rules. Validate before exporting anything, so a failed
# selection leaves the caller's environment untouched (the repository CLI home
# is created only for an explicit or repository-local SDK root).
if ! (
    if [[ -n "${DOTNET_CLI_HOME:-}" ]]; then
        export DOTNET_CLI_HOME="$DOTNET_CLI_HOME"
    elif [[ -n "$_origo_sdk_root" ]]; then
        export DOTNET_CLI_HOME="$_origo_cli_home"
    fi
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    if [[ -n "$_origo_sdk_root" ]]; then
        export DOTNET_ROOT="$_origo_sdk_root"
    fi
    cd "$ROOT" && "$_origo_dotnet" --version >/dev/null 2>&1
); then
    _origo_required="$(sed -nE 's/.*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$ROOT/global.json" | head -1)"
    echo "ERROR: the selected .NET SDK does not satisfy $ROOT/global.json." >&2
    echo "  selected SDK:  $_origo_dotnet" >&2
    echo "  required SDK:  ${_origo_required:-unknown}" >&2
    echo "  installed SDKs:" >&2
    "$_origo_dotnet" --list-sdks 2>/dev/null | sed 's/^/    /' >&2 || true
    echo "Do not edit global.json to match an installed SDK." >&2
    echo "Run: bash \"$ROOT/scripts/install-dotnet.sh\"" >&2
    unset _origo_cli_home _origo_sdk_root _origo_dotnet _origo_required _origo_explicit_root
    return 1
fi

# A system SDK keeps the caller's CLI/NuGet environment unchanged; only a
# selected SDK root gets the repository-local CLI home and cache.
if [[ -n "$_origo_sdk_root" ]]; then
    export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$_origo_cli_home}"
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
    mkdir -p "$DOTNET_CLI_HOME"
    export DOTNET_ROOT="$_origo_sdk_root"
    export PATH="$_origo_sdk_root:$PATH"

    # Use the repository-local NuGet cache only when the default user cache
    # is not writable (common in container/read-only-home environments).
    if [[ -n "${HOME:-}" ]]; then
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
        mkdir -p "${NUGET_PACKAGES:-${DEFAULT_CACHE:-$HOME/.nuget/packages}}"
    fi
fi
# The validated SDK executable, so the ./dotnet wrapper can run exactly the
# SDK this script selected instead of re-resolving PATH.
export ORIGO_DOTNET_EXE="$_origo_dotnet"

unset _origo_cli_home _origo_sdk_root _origo_dotnet _origo_required _origo_explicit_root
return 0
