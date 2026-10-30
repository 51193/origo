#!/usr/bin/env bash
# CI step: build + test with Coverlet line coverage gates.
# Mirrors the "CI — build, tests, line coverage gates" step of the GitHub
# Actions bash-build-and-test job (restore -> build -> test, Coverlet enforced).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
source "$ROOT/scripts/bash/dotnet-env.sh"

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
  echo " • Tool: Coverlet (coverlet.msbuild) on the five measured test projects"
  echo " • Measured-project configuration (CollectCoverage/Threshold/Include) is pinned below"
  echo " • Unified threshold: >= 90% line coverage across the measured projects"
  echo " • If coverage is below threshold, 'dotnet test' fails with a Coverlet error."
  echo " • Below, after tests, Coverlet prints a summary table (Line / Branch / Method)."
  echo "══════════════════════════════════════════════════════════════════════"
  echo ""
}

print_coverage_banner

if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
  echo "::notice title=Origo line coverage::Coverlet enforces >= 90% line coverage across the measured test projects. Summary table is printed after tests."
fi

# --- coverage gate configuration pin ---
# The percentage gate is only as strong as the per-project Coverlet settings: a
# measured project that loses CollectCoverage, the threshold, or its assembly
# Include filter silently stops being measured while CI stays green. Pin the
# expected configuration here so drift fails before any test runs.
# Entry format: <test project path>|<assembly Include filter token>
MEASURED_COVERAGE_PROJECTS=(
  "Origo.Core.Tests/Origo.Core.Tests.csproj|[Origo.Core]"
  "Origo.GodotAdapter.Tests/Origo.GodotAdapter.Tests.csproj|[Origo.GodotAdapter]"
  "Origo.ConsoleBridge.Tests/Origo.ConsoleBridge.Tests.csproj|[Origo.ConsoleBridge]"
  "Origo.SourceGeneration.Tests/Origo.SourceGeneration.Tests.csproj|[Origo.SourceGeneration]"
  "tools/ApiInventoryTool.Tests/ApiInventoryTool.Tests.csproj|[ApiInventoryTool]"
)

COVERAGE_CONFIG_FAILURES=0

coverage_config_fail() {
  echo "ERROR: coverage gate configuration: $*" >&2
  COVERAGE_CONFIG_FAILURES=$((COVERAGE_CONFIG_FAILURES + 1))
}

require_coverage_setting() {
  local project="$1"
  local expected="$2"
  if ! grep -Fq -- "$expected" "$project"; then
    coverage_config_fail "'$expected' is missing from $project"
  fi
}

verify_coverage_configuration() {
  local entry project include_filter
  for entry in "${MEASURED_COVERAGE_PROJECTS[@]}"; do
    project="${entry%%|*}"
    include_filter="${entry##*|}"
    if [[ ! -f "$project" ]]; then
      coverage_config_fail "measured test project is missing: $project"
      continue
    fi

    require_coverage_setting "$project" "<CollectCoverage>true</CollectCoverage>"
    require_coverage_setting "$project" "<CoverletOutputFormat>cobertura</CoverletOutputFormat>"
    require_coverage_setting "$project" "<ThresholdType>line</ThresholdType>"
    require_coverage_setting "$project" "<ThresholdStat>total</ThresholdStat>"
    require_coverage_setting "$project" "coverlet.msbuild"
    if ! grep -Fq -- "$include_filter" "$project"; then
      coverage_config_fail "Include filter '$include_filter' is missing from $project"
    fi

    local solution_entry
    solution_entry="$(basename "$project")"
    if ! grep -Fq -- "$solution_entry" Origo.sln; then
      coverage_config_fail "measured test project is not listed in Origo.sln: $project"
    fi

    local threshold
    threshold="$(sed -n -E 's:.*<Threshold>([0-9]+)</Threshold>.*:\1:p' "$project")"
    if [[ ! "$threshold" =~ ^[0-9]+$ ]]; then
      coverage_config_fail "no numeric <Threshold> element is configured in $project"
    elif [[ "$threshold" -lt 90 ]]; then
      coverage_config_fail "coverage threshold $threshold in $project is below the documented 90% line gate"
    fi
  done

  if [[ "$COVERAGE_CONFIG_FAILURES" -ne 0 ]]; then
    echo "ERROR: coverage gate configuration drifted ($COVERAGE_CONFIG_FAILURES problem(s)); refusing to run the test gate." >&2
    exit 1
  fi

  echo "Coverage configuration: OK (${#MEASURED_COVERAGE_PROJECTS[@]} measured projects, >= 90% line threshold)."
}

verify_coverage_configuration
# --- end coverage gate configuration pin ---

dotnet restore Origo.sln
dotnet build Origo.sln --no-restore --configuration Release

echo ""
echo ">>> Running tests — Coverlet will fail the job if any measured project's LINE coverage is below its threshold."
echo ""

# Benchmarks are tagged [Trait("Category","Benchmark")] and run separately via
# scripts/bash/benchmark.sh (a dedicated CI step) so they are not executed twice.
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
