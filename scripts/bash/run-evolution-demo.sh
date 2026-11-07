#!/usr/bin/env bash
# Build and launch the playable Evolution Demo.
#
# Usage:
#   bash scripts/bash/run-evolution-demo.sh
#   bash scripts/bash/run-evolution-demo.sh --headless
#   bash scripts/bash/run-evolution-demo.sh --headless --seconds 120
#   bash scripts/bash/run-evolution-demo.sh --no-build
#
# --headless runs the demo acceptance smoke instead of opening a window. It
# uses the same ORIGO_DEMO_SMOKE paths as CI and exits after its own checks.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

source "$ROOT/scripts/bash/dotnet-env.sh"

HEADLESS=0
RUN_BUILD=1
SMOKE_SECONDS=60

usage() {
    cat <<'USAGE'
Usage:
  bash scripts/bash/run-evolution-demo.sh
  bash scripts/bash/run-evolution-demo.sh --headless
  bash scripts/bash/run-evolution-demo.sh --headless --seconds 120
  bash scripts/bash/run-evolution-demo.sh --no-build

--headless runs the demo acceptance smoke instead of opening a window.
--seconds is only valid with --headless and sets its simulated target.
--no-build launches an already-built demo without rebuilding.
USAGE
}

while (($# > 0)); do
    case "$1" in
        --headless)
            HEADLESS=1
            shift
            ;;
        --no-build)
            RUN_BUILD=0
            shift
            ;;
        --seconds)
            if (($# < 2)) || ! [[ "$2" =~ ^[1-9][0-9]*$ ]]; then
                echo "ERROR: --seconds requires a positive integer." >&2
                exit 1
            fi
            SMOKE_SECONDS="$2"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "ERROR: unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

if ((HEADLESS == 0)); then
    # A window runner should not pretend to support a target duration.
    if ((SMOKE_SECONDS != 60)); then
        echo "ERROR: --seconds is only valid with --headless." >&2
        exit 1
    fi
fi

if ((RUN_BUILD == 1)); then
    echo "Building Evolution Demo..."
    dotnet build samples/Origo.EvolutionDemo/Origo.EvolutionDemo.csproj --configuration Debug --nologo -v minimal
fi

GODOT_BIN="$(bash scripts/bash/download-godot.sh)"

# Keep Godot's user:// data inside the repository so the demo runs in sandboxes
# and containers where the real home directory is not writable. Override this
# path with EVOLUTION_DEMO_HOME when persistent saves should live elsewhere.
GODOT_HOME="${EVOLUTION_DEMO_HOME:-$ROOT/.godot-home}"
mkdir -p "$GODOT_HOME/.local/share" "$GODOT_HOME/.config" "$GODOT_HOME/.cache"

run_godot() {
    HOME="$GODOT_HOME" \
    XDG_DATA_HOME="$GODOT_HOME/.local/share" \
    XDG_CONFIG_HOME="$GODOT_HOME/.config" \
    XDG_CACHE_HOME="$GODOT_HOME/.cache" \
        "$GODOT_BIN" "$@"
}

if ((HEADLESS == 1)); then
    echo "Running headless Evolution Demo smoke (${SMOKE_SECONDS}s simulated target)..."
    export ORIGO_DEMO_SMOKE=1
    export ORIGO_DEMO_SMOKE_SECONDS="$SMOKE_SECONDS"
    run_godot --headless --path samples/Origo.EvolutionDemo --quit-after 40000
    exit 0
fi

echo "Launching Evolution Demo..."
exec env \
    HOME="$GODOT_HOME" \
    XDG_DATA_HOME="$GODOT_HOME/.local/share" \
    XDG_CONFIG_HOME="$GODOT_HOME/.config" \
    XDG_CACHE_HOME="$GODOT_HOME/.cache" \
    "$GODOT_BIN" --path samples/Origo.EvolutionDemo
