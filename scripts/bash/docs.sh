#!/usr/bin/env bash
# Generate or validate committed documentation artifacts locally.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

PYTHON_BIN="${ORIGO_PYTHON:-}"
if [[ -z "$PYTHON_BIN" ]]; then
    python_candidates=(python3 py python)
    case "$(uname -s)" in
        MINGW*|MSYS*|CYGWIN*) python_candidates=(py python python3) ;;
    esac
    for candidate in "${python_candidates[@]}"; do
        if command -v "$candidate" >/dev/null 2>&1 \
            && "$candidate" -c 'import sys; raise SystemExit(0 if sys.version_info >= (3, 10) else 1)' >/dev/null 2>&1; then
            PYTHON_BIN="$candidate"
            break
        fi
    done
fi

if [[ -z "$PYTHON_BIN" ]]; then
    echo "ERROR: Python 3.10+ is required (python3, py, or python)." >&2
    exit 1
fi

"$PYTHON_BIN" scripts/python/docs_generate.py "${1:-check}"
