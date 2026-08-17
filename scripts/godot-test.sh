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
GODOT_OUTPUT=$("$GODOT_BIN" --headless --path "$ROOT/Origo.GodotAdapter.Integration.Tests" 2>&1)
EXIT_CODE=$?
set -e

# Parse the runner's own result summary line. The runner fails itself
# (exit code 1) when zero tests are discovered; this parse is a second
# guard so CI cannot silently pass on "0 total".
TOTAL_TESTS=$(echo "$GODOT_OUTPUT" | sed -n 's/.*INTEGRATION_TEST_RESULTS: \([0-9][0-9]*\) total.*/\1/p' | tail -1)

if [[ -z "$TOTAL_TESTS" || "$TOTAL_TESTS" -eq 0 ]]; then
    echo ""
    echo "Integration tests FAILED: no results line with a positive test count was produced."
    echo "$GODOT_OUTPUT" | tail -20
    exit 1
fi

# Parse and display test results
if [[ $EXIT_CODE -eq 0 ]]; then
    echo ""
    echo "All $TOTAL_TESTS integration tests passed."
else
    echo ""
    echo "Integration tests FAILED (exit code: $EXIT_CODE, total: $TOTAL_TESTS)."
    echo "$GODOT_OUTPUT" | tail -30
fi

# Node-leak gate: Godot still exits 0 when ObjectDB instances leak, so the
# script must treat the leak warning as a failure. Test fixtures are expected
# to free every node they create.
if echo "$GODOT_OUTPUT" | grep -q "ObjectDB instances were leaked"; then
    echo ""
    echo "Integration tests FAILED: Godot reported leaked ObjectDB instances."
    echo "$GODOT_OUTPUT" | grep -A40 "Leaked instance" || true
    exit 1
fi

exit $EXIT_CODE
