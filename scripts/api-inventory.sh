#!/usr/bin/env bash
# Shell API baseline gate.
#
# 1. Builds the shell projects in Release with the repository's warning-as-error
#    settings, so compiler or source-generator diagnostics stop the gate before
#    any inventory is produced.
# 2. Runs the tracked Roslyn inventory over the real Release assemblies and
#    either verifies it against the committed JSON baseline or regenerates the
#    baseline for an approved API change.
# 3. Runs previous-package validation when ORIGO_PREVIOUS_API_BASELINE points
#    at the previous stable shell baseline; the first 0.1.0 release has no
#    previous package, so that comparison is explicitly skipped with a message.
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

target_dir() {
  dotnet msbuild "$1" -getProperty:TargetDir -p:Configuration=Release -nologo | tr -d '\r'
}

CONTRACTS_DIR="$(target_dir Origo.Core.Contracts/Origo.Core.Contracts.csproj)"
CORE_DIR="$(target_dir Origo.Core/Origo.Core.csproj)"
ADAPTER_DIR="$(target_dir Origo.GodotAdapter/Origo.GodotAdapter.csproj)"

ASSEMBLY_ARGS=(
  --assembly "Origo.Core.Contracts=${CONTRACTS_DIR}Origo.Core.Contracts.dll"
  --assembly "Origo.Core=${CORE_DIR}Origo.Core.dll"
  --assembly "Origo.GodotAdapter=${ADAPTER_DIR}Origo.GodotAdapter.dll"
)
REFERENCE_ARGS=(
  --reference-dir "$CONTRACTS_DIR"
  --reference-dir "$CORE_DIR"
  --reference-dir "$ADAPTER_DIR"
)

if [[ "$MODE" == "generate" ]]; then
  dotnet run --project tools/ApiInventoryTool -- generate \
    "${ASSEMBLY_ARGS[@]}" "${REFERENCE_ARGS[@]}" --output "$BASELINE"
else
  dotnet run --project tools/ApiInventoryTool -- verify \
    "${ASSEMBLY_ARGS[@]}" "${REFERENCE_ARGS[@]}" --baseline "$BASELINE"
fi

PREVIOUS="${ORIGO_PREVIOUS_API_BASELINE:-}"
if [[ -n "$PREVIOUS" ]]; then
  if [[ ! -f "$PREVIOUS" ]]; then
    echo "ERROR: ORIGO_PREVIOUS_API_BASELINE does not exist: $PREVIOUS" >&2
    exit 1
  fi
  CURRENT="$(mktemp)"
  trap 'rm -f "$CURRENT"' EXIT
  dotnet run --project tools/ApiInventoryTool -- generate \
    "${ASSEMBLY_ARGS[@]}" "${REFERENCE_ARGS[@]}" --output "$CURRENT"
  dotnet run --project tools/ApiInventoryTool -- compare --previous "$PREVIOUS" --current "$CURRENT"
else
  echo ""
  echo "Previous-package validation: no previous stable shell package baseline"
  echo "is configured. 0.1.0 is the first release, so this gate is explicitly"
  echo "not applicable yet; set ORIGO_PREVIOUS_API_BASELINE to the released"
  echo "shell-api-baseline.json to enable removal/signature validation."
fi

echo ""
echo "Shell API inventory: OK"
