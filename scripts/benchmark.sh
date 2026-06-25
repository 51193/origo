#!/usr/bin/env bash
# Origo source-generation performance benchmarks.
#
# Compares the generated TypedData (inline storage + Kind-based dispatch) against
# an unoptimized boxing implementation across several value types and a reference
# type. Lenient: the generated path only has to stay within a generous multiple of
# the baseline and within a per-benchmark time cap — it is not required to be
# faster. Comparison tables are printed and the budget is asserted on every run.
#
# Tagged [Trait("Category","Benchmark")], these run here only (a dedicated CI step)
# and are excluded from scripts/ci.sh so they execute exactly once.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo ""
echo ">>> SourceGeneration performance benchmarks — generated TypedData vs unoptimized boxing"
echo ""

dotnet test Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj \
  --configuration Release \
  --filter "Category=Benchmark" \
  --logger "console;verbosity=detailed" \
  -p:CollectCoverage=false
