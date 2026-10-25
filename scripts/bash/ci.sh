#!/usr/bin/env bash
# Origo local CI reproduction — runs the same single-platform gate steps as
# GitHub Actions in order:
#   0. scripts/bash/lint-scripts.sh — shell + workflow lint
#   1. scripts/bash/format.sh   — dotnet format verification
#   2. scripts/bash/api-inventory.sh — shell API baseline gate (Roslyn inventory)
#   3. scripts/bash/test.sh     — build + test + Coverlet line coverage gates
#   4. scripts/bash/benchmark.sh— performance benchmarks ([Category=Benchmark])
#   5. scripts/bash/godot-test.sh — Godot headless integration tests (downloads Godot)
#   6. scripts/bash/package-consumer-smoke.sh — shell-only package restore/build/startup
#
# Each step is a standalone script mapped 1:1 to a CI step. Run this master
# script for a complete local reproduction of the single-platform gate set;
# GitHub Actions additionally runs the OS matrix. Commit-message lint runs
# locally through .githooks/pre-push. For fast dev iteration, run an individual
# step script directly.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

bash scripts/bash/lint-scripts.sh
bash scripts/bash/format.sh

bash scripts/bash/api-inventory.sh

bash scripts/bash/test.sh
bash scripts/bash/benchmark.sh
bash scripts/bash/godot-test.sh
bash scripts/bash/package-consumer-smoke.sh

echo ""
echo "✔ All CI steps passed."
