#!/usr/bin/env bash
# CI step: build + test with Coverlet line coverage gates.
# Mirrors the "CI — build, tests, line coverage gates" step of the GitHub
# Actions build-and-test job (restore -> build -> test, Coverlet enforced).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

print_coverage_banner() {
  echo ""
  echo "══════════════════════════════════════════════════════════════════════"
  echo " Origo LINE COVERAGE GATES (enforced in CI and local runs)"
  echo " ────────────────────────────────────────────────────────────────────"
  echo " • Tool: Coverlet (coverlet.msbuild) on all test projects"
  echo " • Unified threshold: >= 90% line coverage across all projects"
  echo " • If coverage is below threshold, 'dotnet test' fails with a Coverlet error."
  echo " • Below, after tests, Coverlet prints a summary table (Line / Branch / Method)."
  echo "══════════════════════════════════════════════════════════════════════"
  echo ""
}

print_coverage_banner

if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
  echo "::notice title=Origo line coverage::Coverlet enforces >= 90% line coverage across all test projects. Summary table is printed after tests."
fi

dotnet restore Origo.sln
dotnet build Origo.sln --no-restore --configuration Release

echo ""
echo ">>> Running tests — Coverlet will fail the job if any project's LINE coverage is below its threshold."
echo ""

# Benchmarks are tagged [Trait("Category","Benchmark")] and run separately via
# scripts/benchmark.sh (a dedicated CI step) so they are not executed twice.
dotnet test Origo.sln --no-build --configuration Release --verbosity normal --filter "Category!=Benchmark" --logger "console;verbosity=detailed"
