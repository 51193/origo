#!/usr/bin/env bash
# Origo 标准 CI：与 GitHub Actions 使用相同步骤（restore → build → test，含 Coverlet 行覆盖率门禁）。
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

print_coverage_banner() {
  echo ""
  echo "══════════════════════════════════════════════════════════════════════"
  echo " Origo LINE COVERAGE GATES (enforced in CI and local runs)"
  echo " ────────────────────────────────────────────────────────────────────"
  echo " • Tool: Coverlet (coverlet.msbuild) on all test projects"
  echo " • Origo.Core            : line >= 90% (excl. OrigoAutoInitializer.cs, FastNoiseLite.cs)"
  echo " • Origo.ConsoleBridge   : line >= 80% (testable server logic)"
  echo " • Origo.GodotAdapter    : line >= 85% (excl. engine-dependent & generated files)"
  echo " • Origo.SourceGeneration: line >= 85% (TypedData incremental generator)"
  echo " • If coverage is below threshold, 'dotnet test' fails with a Coverlet error."
  echo " • Below, after tests, Coverlet prints a summary table (Line / Branch / Method)."
  echo "══════════════════════════════════════════════════════════════════════"
  echo ""
}

print_coverage_banner

if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
  echo "::notice title=Origo line coverage::Coverlet enforces line coverage: Origo.Core >= 90%, ConsoleBridge >= 80%, GodotAdapter >= 85% (testable subset), SourceGeneration >= 85%. Summary table is printed after tests."
fi

dotnet restore Origo.sln
dotnet build Origo.sln --no-restore --configuration Release

echo ""
echo ">>> Running tests — Coverlet will fail the job if any project's LINE coverage is below its threshold."
echo ""

dotnet test Origo.sln --no-build --configuration Release --verbosity normal

echo ""
echo ">>> SourceGeneration performance benchmarks — generated TypedData vs unoptimized boxing."
echo ">>> (Already executed and asserted in the run above; re-run here only to print the comparison tables.)"
echo ""

dotnet test Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj \
  --no-build --configuration Release \
  --filter "FullyQualifiedName~TypedDataGeneratedBenchmarkTests" \
  --logger "console;verbosity=detailed" \
  -p:CollectCoverage=false
