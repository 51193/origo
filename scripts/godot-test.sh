#!/usr/bin/env bash
# Run Godot headless integration tests.
# Downloads the matching Godot binary if needed, then runs the test project.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

GODOT_BIN=$(bash scripts/download-godot.sh)
echo "Using Godot binary: $GODOT_BIN"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " Godot Headless Integration Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Clean stale Godot build caches to ensure a fresh build.
# This mirrors CI behavior (fresh checkout = no .godot/ residue).
# .godot_binary/ (downloaded Godot engine binary) is NOT cleaned — only
# compilation caches are removed.
echo "Cleaning Godot build caches..."
rm -rf "$ROOT/Origo.GodotAdapter/.godot/"
rm -rf "$ROOT/Origo.GodotAdapter.Integration.Tests/.godot/"

# Rebuild .NET projects after cache clean. Godot.NET.Sdk projects output to
# .godot/mono/temp/bin/ even when built with dotnet build, so Godot will find
# the compiled assemblies.
echo "Building integration test project..."
dotnet build "$ROOT/Origo.GodotAdapter.Integration.Tests/Origo.GodotAdapter.Integration.Tests.csproj" --verbosity quiet
echo ""

set +e
"$GODOT_BIN" --headless --path "$ROOT/Origo.GodotAdapter.Integration.Tests" 2>&1
EXIT_CODE=$?
set -e

# Parse and display test results
if [[ $EXIT_CODE -eq 0 ]]; then
    echo ""
    echo "All integration tests passed."
else
    echo ""
    echo "Integration tests FAILED (exit code: $EXIT_CODE)."
fi

exit $EXIT_CODE
