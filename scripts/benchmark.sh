#!/usr/bin/env bash
# Origo performance benchmarks.
# Runs TypedData micro-benchmarks (SG-generated inline vs boxing), core subsystem
# benchmarks (entity lifecycle, observer topology, DataSourceNode, Blackboard, save
# persistence, concurrent queue, random, strategy performance), and Godot adapter
# throughput benchmarks.
#
# Tagged [Trait("Category","Benchmark")], these run here only: they are excluded
# from the regular test run (scripts/test.sh filters them out) and executed once
# by this dedicated CI step, which scripts/ci.sh invokes.
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
  "SourceGeneration micro-benchmarks — TypedData inline vs boxing (value + reference types)" \
  "Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj"

run_benchmark \
  "Core real-world benchmarks — dictionary-backed, observer, serialization simulations" \
  "Origo.Core.Tests/Origo.Core.Tests.csproj"

run_benchmark \
  "GodotAdapter benchmarks — Godot-typed TypedData write/read/convert throughput" \
  "Origo.GodotAdapter.Tests/Origo.GodotAdapter.Tests.csproj"

exit $EXIT_CODE
