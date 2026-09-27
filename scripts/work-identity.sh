#!/usr/bin/env bash
# Validate and create human-readable branch/worktree identities.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
exec python3 scripts/work_identity.py "$@"
