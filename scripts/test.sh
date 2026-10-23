#!/usr/bin/env bash
# CI step: build + test with Coverlet line coverage gates.
# Mirrors the "CI — build, tests, line coverage gates" step of the GitHub
# Actions build-and-test job (restore -> build -> test, Coverlet enforced).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
source "$ROOT/scripts/dotnet-env.sh"

# Coverlet's instrumentation tracker creates a named Mutex during test-host
# unload. On Linux .NET backs named mutexes with files under XDG_RUNTIME_DIR;
# some sandboxes expose /run/user/<id> as EROFS, which makes the test host
# report an unhandled System.IO.IOException after every test has passed. Use a
# private writable runtime directory for this test invocation so the coverage
# gate stays clean and does not emit a false crash report.
ORIGO_TEST_RUNTIME_DIR="$(mktemp -d "${TMPDIR:-/tmp}/origo-test-runtime.XXXXXX")"
export XDG_RUNTIME_DIR="$ORIGO_TEST_RUNTIME_DIR"
trap 'rm -rf "$ORIGO_TEST_RUNTIME_DIR"' EXIT

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
#
# -m:1 runs the test projects sequentially instead of in parallel. Parallel
# test processes on multi-core Windows runners stall xUnit v3's assembly-info
# child process long enough (>1s) to hit upstream bug xunit/xunit#3576, where
# the "Waiting 10 seconds for foreground threads to exit..." message pollutes
# the assembly-info JSON and VSTest fails discovery with "Test process did not
# return valid JSON". Sequential runs keep each child process's exit fast
# enough to avoid the race (fixed upstream only in xunit.v3 4.0.0-pre.128+,
# which requires the Microsoft Testing Platform migration).
dotnet test Origo.sln --no-build --configuration Release --verbosity normal --filter "Category!=Benchmark" --logger "console;verbosity=detailed" -m:1
