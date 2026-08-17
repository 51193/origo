#!/usr/bin/env bash
# Origo full CI reproduction — runs every GitHub Actions step in order:
#   1. scripts/format.sh   — dotnet format verification
#   2. scripts/doc-sync.sh — doc sync validate + generation
#   3. scripts/test.sh     — build + test + Coverlet line coverage gates
#   4. scripts/benchmark.sh— performance benchmarks ([Category=Benchmark])
#   5. scripts/godot-test.sh — Godot headless integration tests (downloads Godot)
#
# Each step is a standalone script mapped 1:1 to a CI step. Run this master
# script for a complete local reproduction of CI. For fast dev iteration, run
# an individual step script directly (e.g. `bash scripts/test.sh`).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

bash scripts/format.sh
bash scripts/doc-sync.sh

# Committed-hubs check (the CI PR gate): generated README.md hubs and
# .sync-status.json must be committed together with documentation changes.
# This is what makes local scripts/ci.sh equivalent to CI.
if [[ -n $(git status --porcelain -- docs/) ]]; then
  echo "" >&2
  echo "ERROR: generated doc files (README.md hubs, .sync-status.json) are not committed." >&2
  echo "Run 'git add docs/ && git commit --amend --no-edit' (or make a docs commit) and re-run." >&2
  git status --short -- docs/ >&2
  exit 1
fi

bash scripts/test.sh
bash scripts/benchmark.sh
bash scripts/godot-test.sh

echo ""
echo "✔ All CI steps passed."
