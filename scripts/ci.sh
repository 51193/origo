#!/usr/bin/env bash
# Origo full CI reproduction — runs every GitHub Actions step in order:
#   1. scripts/format.sh   — dotnet format verification
#   2. scripts/test.sh     — build + test + Coverlet line coverage gates
#   3. scripts/benchmark.sh— performance benchmarks ([Category=Benchmark])
#   4. scripts/godot-test.sh — Godot headless integration tests (downloads Godot)
#
# Each step is a standalone script mapped 1:1 to a CI step. Run this master
# script for a complete local reproduction of CI. For fast dev iteration, run
# an individual step script directly (e.g. `bash scripts/test.sh`).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

bash scripts/format.sh
bash scripts/test.sh
bash scripts/benchmark.sh
bash scripts/godot-test.sh

echo ""
echo "✔ All CI steps passed."
