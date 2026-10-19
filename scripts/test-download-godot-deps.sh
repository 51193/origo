#!/usr/bin/env bash
# Regression tests for the dependency handling in scripts/download-godot.sh.
#
# A cold cache unpacks the engine archive, so the script must report a missing
# unzip before it starts downloading instead of failing inside the extraction
# with a bare "command not found"; a warm cache must keep working without
# unzip and without a download tool.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

WORK=$(mktemp -d "${TMPDIR:-/tmp}/origo-download-godot.XXXXXX")
cleanup() {
    rm -rf "$WORK"
}
trap cleanup EXIT

# The script resolves its project root from its own location and parses the
# adapter csproj, so give it a throwaway root that carries the same pin.
mkdir -p "$WORK/root/scripts" "$WORK/root/Origo.GodotAdapter" "$WORK/bin"
cp "$ROOT/scripts/download-godot.sh" "$WORK/root/scripts/download-godot.sh"
printf '<Project Sdk="Godot.NET.Sdk/4.7.2"></Project>\n' \
    > "$WORK/root/Origo.GodotAdapter/Origo.GodotAdapter.csproj"

# A curated PATH: the platform tools the script needs, but no unzip, curl, or
# wget, so each dependency is controlled by this test.
for tool in bash dirname uname sed head find mkdir chmod; do
    ln -s "$(command -v "$tool")" "$WORK/bin/$tool"
done

run_script() {
    PATH="$WORK/bin" bash "$WORK/root/scripts/download-godot.sh" 2>&1
}

# Cold cache without unzip: the dependency is reported up front and the
# download never starts.
set +e
OUTPUT=$(run_script)
STATUS=$?
set -e
[[ $STATUS -ne 0 ]] || FAIL "a cold cache without unzip must fail: $OUTPUT"
grep -q "unzip is required" <<<"$OUTPUT" \
    || FAIL "missing the unzip dependency message: $OUTPUT"
if grep -q "Downloading Godot" <<<"$OUTPUT"; then
    FAIL "the download started before the unzip check: $OUTPUT"
fi

# Cold cache with unzip but without curl/wget: the download-tool message is the
# next failure, which shows the unzip check runs first and does not mask it.
printf '#!/bin/sh\nexit 0\n' > "$WORK/bin/unzip"
chmod +x "$WORK/bin/unzip"
set +e
OUTPUT=$(run_script)
STATUS=$?
set -e
[[ $STATUS -ne 0 ]] || FAIL "a cold cache without curl/wget must fail: $OUTPUT"
grep -q "Neither curl nor wget" <<<"$OUTPUT" \
    || FAIL "missing the download-tool message: $OUTPUT"

# Warm cache: the cached engine is reused without unzip or a download tool.
CACHED="$WORK/root/.godot_binary/4.7.2/engine"
mkdir -p "$CACHED"
printf '#!/bin/sh\nexit 0\n' > "$CACHED/Godot_v4.7.2-stable_mono_linux.x86_64"
chmod +x "$CACHED/Godot_v4.7.2-stable_mono_linux.x86_64"
set +e
OUTPUT=$(run_script)
STATUS=$?
set -e
[[ $STATUS -eq 0 ]] || FAIL "a warm cache must be used without unzip: $OUTPUT"
grep -q "Godot binary cached" <<<"$OUTPUT" \
    || FAIL "the warm cache path did not report the cached binary: $OUTPUT"

echo "Download-godot dependency handling: OK"
