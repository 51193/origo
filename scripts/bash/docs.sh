#!/usr/bin/env bash
# Generate or validate committed documentation artifacts locally.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required for documentation generation/validation." >&2
    exit 1
fi

python3 scripts/python/docs_generate.py "${1:-check}"
