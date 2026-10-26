#!/usr/bin/env bash
# Self-test for committed documentation generation and local links.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
bash "$ROOT/scripts/bash/docs.sh" check
