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
  echo " • Thresholds are configured in each test project's .csproj file"
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

# Benchmarks are tagged [Trait("Category","Benchmark")] and run separately via
# scripts/benchmark.sh (a dedicated CI step) so they are not executed twice.
dotnet test Origo.sln --no-build --configuration Release --verbosity normal --filter "Category!=Benchmark"
