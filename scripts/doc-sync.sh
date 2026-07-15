#!/usr/bin/env bash
# CI step: DocSync validation and generation.
# 1. Runs DocSyncTool generate to update navigation hubs and status file.
# 2. Runs DocSyncTool validate to check revision consistency and link correctness.
# 3. Checks that generated files match what's committed.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " DocSync: generate + validate"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo "::group::DocSyncTool generate"
dotnet run --project tools/DocSyncTool -- generate
echo "::endgroup::"

echo "::group::DocSyncTool validate"
dotnet run --project tools/DocSyncTool -- validate
echo "::endgroup::"

echo ""
echo "::group::Checking generated files are up-to-date"
if ! git diff --quiet -- docs/; then
  echo "ERROR: Generated doc files (README.md hubs, .sync-status.json) are out of date."
  echo ""
  echo "Run the following command locally and commit the results:"
  echo "  dotnet run --project tools/DocSyncTool -- generate"
  echo ""
  exit 1
fi
echo "DocSync: OK (generated files up-to-date)"
echo "::endgroup::"
