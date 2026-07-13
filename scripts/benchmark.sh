#!/usr/bin/env bash
# Origo performance benchmarks.
# Runs both micro-benchmarks (SG-generated TypedData members vs boxing) and
# real-world-simulation benchmarks (dictionary-backed, observer, serialization).
#
# Tagged [Trait("Category","Benchmark")], these run here only (a dedicated CI step)
# and are excluded from scripts/ci.sh so they execute exactly once.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

EXIT_CODE=0

run_benchmark() {
  local label="$1"
  local project="$2"
  echo ""
  echo ">>> $label"
  echo ""
  dotnet test "$project" \
    --configuration Release \
    --filter "Category=Benchmark" \
    --logger "console;verbosity=detailed" \
    -p:CollectCoverage=false || EXIT_CODE=$?
}

run_benchmark \
  "SourceGeneration micro-benchmarks — generated TypedData members vs unoptimized boxing" \
  "Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj"

run_benchmark \
  "Core real-world benchmarks — dictionary-backed, observer, serialization simulations" \
  "Origo.Core.Tests/Origo.Core.Tests.csproj"

run_benchmark \
  "GodotAdapter benchmarks — Godot-typed TypedData write/read/convert throughput" \
  "Origo.GodotAdapter.Tests/Origo.GodotAdapter.Tests.csproj"

exit $EXIT_CODE
