#!/usr/bin/env bash
# Layered verification: plan, quick, affected, or full.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
source "$ROOT/scripts/dotnet-env.sh"
exec python3 "$ROOT/scripts/check.py" "$@"
