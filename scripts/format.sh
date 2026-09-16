#!/usr/bin/env bash
# CI step: format check (dotnet format --verify-no-changes --severity info).
# Mirrors the "Format check" step of the GitHub Actions build-and-test job.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
source "$ROOT/scripts/dotnet-env.sh"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " Format check (dotnet format --verify-no-changes --severity info)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
dotnet restore Origo.sln --verbosity quiet
dotnet format Origo.sln --verify-no-changes --severity info
dotnet format analyzers Origo.sln --verify-no-changes --severity info --diagnostics IDE0051 IDE0052
echo "Format: OK"
