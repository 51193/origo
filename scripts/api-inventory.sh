#!/usr/bin/env bash
# Shell API baseline gate.
#
# 1. Builds the shell projects in Release with the repository's warning-as-error
#    settings, so compiler or source-generator diagnostics stop the gate before
#    any inventory is produced.
# 2. Runs the tracked Roslyn inventory over the real Release assemblies and
#    either verifies it against the committed JSON baseline or regenerates the
#    baseline for an approved API change.
# 3. Runs previous-package validation. An explicit
#    ORIGO_PREVIOUS_API_BASELINE overrides auto-detection; otherwise the newest
#    formal release tag reachable from HEAD (excluding the current commit) is
#    used when it contains the tracked baseline. Releases before the baseline
#    tool exists skip this comparison with an explicit message.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
source "$ROOT/scripts/dotnet-env.sh"

BASELINE="$ROOT/tools/ApiInventoryTool/shell-api-baseline.json"
MODE="${1:-verify}"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " Shell API inventory: $MODE"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

dotnet build Origo.Core.Contracts/Origo.Core.Contracts.csproj --configuration Release
dotnet build Origo.Core/Origo.Core.csproj --configuration Release
dotnet build Origo.GodotAdapter/Origo.GodotAdapter.csproj --configuration Release
dotnet build Origo.ConsoleBridge/Origo.ConsoleBridge.csproj --configuration Release

target_dir() {
  dotnet msbuild "$1" -getProperty:TargetDir -p:Configuration=Release -nologo | tr -d '\r'
}

CONTRACTS_DIR="$(target_dir Origo.Core.Contracts/Origo.Core.Contracts.csproj)"
CORE_DIR="$(target_dir Origo.Core/Origo.Core.csproj)"
ADAPTER_DIR="$(target_dir Origo.GodotAdapter/Origo.GodotAdapter.csproj)"
BRIDGE_DIR="$(target_dir Origo.ConsoleBridge/Origo.ConsoleBridge.csproj)"

ASSEMBLY_ARGS=(
  --assembly "Origo.Core.Contracts=${CONTRACTS_DIR}Origo.Core.Contracts.dll"
  --assembly "Origo.Core=${CORE_DIR}Origo.Core.dll"
  --assembly "Origo.GodotAdapter=${ADAPTER_DIR}Origo.GodotAdapter.dll"
  --assembly "Origo.ConsoleBridge=${BRIDGE_DIR}Origo.ConsoleBridge.dll"
)
REFERENCE_ARGS=(
  --reference-dir "$CONTRACTS_DIR"
  --reference-dir "$CORE_DIR"
  --reference-dir "$ADAPTER_DIR"
  --reference-dir "$BRIDGE_DIR"
)

if [[ "$MODE" == "generate" ]]; then
  dotnet run --project tools/ApiInventoryTool -- generate \
    "${ASSEMBLY_ARGS[@]}" "${REFERENCE_ARGS[@]}" --output "$BASELINE"
else
  dotnet run --project tools/ApiInventoryTool -- verify \
    "${ASSEMBLY_ARGS[@]}" "${REFERENCE_ARGS[@]}" --baseline "$BASELINE"
fi

CURRENT=""
AUTO_PREVIOUS=""
cleanup() {
  if [[ -n "$CURRENT" ]]; then
    rm -f "$CURRENT"
  fi
  if [[ -n "$AUTO_PREVIOUS" ]]; then
    rm -f "$AUTO_PREVIOUS"
  fi
}
trap cleanup EXIT

PREVIOUS="${ORIGO_PREVIOUS_API_BASELINE:-}"
if [[ -z "$PREVIOUS" ]]; then
  PREVIOUS="$(bash scripts/find-previous-api-baseline.sh)"
  AUTO_PREVIOUS="$PREVIOUS"
fi

if [[ -n "$PREVIOUS" ]]; then
  if [[ ! -f "$PREVIOUS" ]]; then
    echo "ERROR: previous shell API baseline does not exist: $PREVIOUS" >&2
    exit 1
  fi
  SOURCE_KIND="auto-detected"
  if [[ -n "${ORIGO_PREVIOUS_API_BASELINE:-}" ]]; then
    SOURCE_KIND="explicit"
  fi
  echo ""
  echo "Previous-package validation: using ${SOURCE_KIND} shell API baseline."
  CURRENT="$(mktemp)"
  dotnet run --project tools/ApiInventoryTool -- generate \
    "${ASSEMBLY_ARGS[@]}" "${REFERENCE_ARGS[@]}" --output "$CURRENT"
  dotnet run --project tools/ApiInventoryTool -- compare --previous "$PREVIOUS" --current "$CURRENT"
else
  echo ""
  echo "Previous-package validation: no previous formal release shell API baseline"
  echo "was found (releases before the baseline tool was introduced skip this"
  echo "gate). Set ORIGO_PREVIOUS_API_BASELINE to override auto-detection."
fi
echo "Shell API inventory: OK"
