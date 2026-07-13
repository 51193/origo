#!/usr/bin/env bash
# Origo pre-push validation — mirrors CI pipeline locally.
# Usage: bash scripts/validate.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " Phase 1: Format check (dotnet format --severity info)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
dotnet format Origo.sln --verify-no-changes --severity info
echo "Format: OK"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " Phase 2: Build + Test + Coverage gates (scripts/ci.sh)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
bash scripts/ci.sh

echo ""
echo "✔ All checks passed. Ready to push."
