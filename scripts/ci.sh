#!/usr/bin/env bash
# Origo local CI reproduction — runs the same single-platform gate steps as
# GitHub Actions in order:
#   0. scripts/lint-scripts.sh — shell + workflow lint
#   1. scripts/format.sh   — dotnet format verification
#   2. scripts/api-inventory.sh — shell API baseline gate (Roslyn inventory)
#   3. scripts/test.sh     — build + test + Coverlet line coverage gates
#   4. scripts/benchmark.sh— performance benchmarks ([Category=Benchmark])
#   5. scripts/godot-test.sh — Godot headless integration tests (downloads Godot)
#   6. scripts/package-consumer-smoke.sh — shell-only package restore/build/startup
#
# Each step is a standalone script mapped 1:1 to a CI step. Run this master
# script for a complete local reproduction of the single-platform gate set;
# GitHub Actions additionally runs the OS matrix. Commit-message lint runs
# locally through .githooks/pre-push. For fast dev iteration, run an individual
# step script directly.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

bash scripts/lint-scripts.sh
bash scripts/format.sh

bash scripts/api-inventory.sh

bash scripts/test.sh
bash scripts/benchmark.sh
bash scripts/godot-test.sh
bash scripts/package-consumer-smoke.sh

echo ""
echo "✔ All CI steps passed."
