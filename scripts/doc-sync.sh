#!/usr/bin/env bash
# DocSync step: generation and validation.
# 1. Runs DocSyncTool generate to update navigation hubs and status file.
# 2. Runs DocSyncTool validate to check revision consistency and link correctness.
# The committed-hubs check runs inline in ci.yml (auto-commit on push, fail
# with instructions on pull_request), not here; this script mirrors the
# generate+validate part for local pre-commit runs.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
source "$ROOT/scripts/dotnet-env.sh"

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
echo "Local run: the committed-files check runs in CI (ci.yml), which"
echo "auto-commits generated files on push and fails PRs with stale docs."
echo "Run 'git status' to confirm the generated files are committed."
echo "DocSync: OK"
