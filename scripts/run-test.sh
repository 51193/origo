#!/usr/bin/env bash
# 仅执行测试（假设已 restore/build）。与 CI 完全一致请使用：bash scripts/ci.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet test Origo.sln --no-build "$@"
