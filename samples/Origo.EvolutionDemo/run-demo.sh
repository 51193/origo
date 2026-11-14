#!/usr/bin/env bash
# Build and launch the playable Evolution Demo.
#
# Usage:
#   bash samples/Origo.EvolutionDemo/run-demo.sh
#   bash samples/Origo.EvolutionDemo/run-demo.sh --headless
#   bash samples/Origo.EvolutionDemo/run-demo.sh --headless --seconds 120
#   bash samples/Origo.EvolutionDemo/run-demo.sh --no-build
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
FAST_FORWARD_TARGET=""
BENCHMARK_SAVE_SLOT=""

usage() {
    cat <<'USAGE'
Usage:
  bash samples/Origo.EvolutionDemo/run-demo.sh
  bash samples/Origo.EvolutionDemo/run-demo.sh --headless
  bash samples/Origo.EvolutionDemo/run-demo.sh --headless --seconds 120
  bash samples/Origo.EvolutionDemo/run-demo.sh --no-build
  bash samples/Origo.EvolutionDemo/run-demo.sh --headless --fast-forward 3600 --save-slot bench_1h

--headless runs the demo acceptance smoke instead of opening a window.
--seconds sets the initial normal-speed smoke target.
--fast-forward runs headless fast-forward to an absolute simulation time.
--save-slot saves the benchmark result under the isolated smoke user home.
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
        --fast-forward)
            if (($# < 2)) || ! [[ "$2" =~ ^[1-9][0-9]*$ ]]; then
                echo "ERROR: --fast-forward requires a positive integer." >&2
                exit 1
            fi
            FAST_FORWARD_TARGET="$2"
            shift 2
            ;;
        --save-slot)
            if (($# < 2)) || [[ -z "$2" ]]; then
                echo "ERROR: --save-slot requires a slot id." >&2
                exit 1
            fi
            BENCHMARK_SAVE_SLOT="$2"
            shift 2
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
    if ((SMOKE_SECONDS != 60)) || [[ -n "$FAST_FORWARD_TARGET" ]] || [[ -n "$BENCHMARK_SAVE_SLOT" ]]; then
        echo "ERROR: --seconds/--fast-forward/--save-slot require --headless." >&2
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
# Windowed play and headless smoke use separate user-data roots so a smoke run
# can never overwrite or clear the player's real save slots.
if ((HEADLESS == 1)); then
    GODOT_HOME="${EVOLUTION_DEMO_SMOKE_HOME:-$ROOT/samples/.godot-home-smoke}"
else
    GODOT_HOME="${EVOLUTION_DEMO_HOME:-$ROOT/samples/.godot-home}"
fi
mkdir -p "$GODOT_HOME/.local/share" "$GODOT_HOME/.config" "$GODOT_HOME/.cache"
echo "Godot user data: $GODOT_HOME"

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
    if [[ -n "$FAST_FORWARD_TARGET" ]]; then
        export ORIGO_DEMO_SMOKE_FAST_FORWARD="$FAST_FORWARD_TARGET"
    fi
    if [[ -n "$BENCHMARK_SAVE_SLOT" ]]; then
        export ORIGO_DEMO_SMOKE_SAVE_SLOT="$BENCHMARK_SAVE_SLOT"
    fi
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
