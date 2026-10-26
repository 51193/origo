#!/usr/bin/env bash
# Self-test for committed documentation generation and local links.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PYTHONDONTWRITEBYTECODE=1 python3 "$ROOT/scripts/python/docs_generate_test.py"
bash "$ROOT/scripts/bash/docs.sh" check
