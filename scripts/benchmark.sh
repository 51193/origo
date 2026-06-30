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

echo ""
echo ">>> SourceGeneration micro-benchmarks — generated TypedData members vs unoptimized boxing"
echo ""

dotnet test Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj \
  --configuration Release \
  --filter "Category=Benchmark" \
  --logger "console;verbosity=detailed" \
  -p:CollectCoverage=false

echo ""
echo ">>> Core real-world benchmarks — dictionary-backed, observer, serialization simulations"
echo ""

dotnet test Origo.Core.Tests/Origo.Core.Tests.csproj \
  --configuration Release \
  --filter "Category=Benchmark" \
  --logger "console;verbosity=detailed" \
  -p:CollectCoverage=false

echo ""
echo ">>> GodotAdapter benchmarks — Godot-typed TypedData write/read/convert throughput"
echo ""

dotnet test Origo.GodotAdapter.Tests/Origo.GodotAdapter.Tests.csproj \
  --configuration Release \
  --filter "Category=Benchmark" \
  --logger "console;verbosity=detailed" \
  -p:CollectCoverage=false
