#!/usr/bin/env bash
# Focused regression tests for scripts/bash/package-consumer-smoke-lib.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
source "$ROOT/scripts/bash/package-consumer-smoke-lib.sh"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

WORK=$(mktemp -d "${TMPDIR:-/tmp}/origo-smoke-lib.XXXXXX")
cleanup() {
    rm -rf "$WORK"
}
trap cleanup EXIT

# The smoke resolves its isolated NuGet cache to the physical path before
# comparing it with project.assets.json. NuGet writes the physical path on
# macOS, where the temporary directory is reached through the /var symlink.
mkdir -p "$WORK/cache-real/sub"
ln -s "$WORK/cache-real" "$WORK/cache-link"
WORK_REAL=$(cd "$WORK" && pwd -P)
RESOLVED=$(canonicalize_directory "$WORK/cache-link/sub")
[[ "$RESOLVED" == "$WORK_REAL/cache-real/sub" ]] \
    || FAIL "canonicalize_directory did not resolve a symlinked path: $RESOLVED"

# File-presence checks use find's own -quit instead of piping into grep -q.
mkdir -p "$WORK/tree/nested"
printf 'x' > "$WORK/tree/nested/found.dll"
FOUND=$(find_first_file "$WORK/tree" -type f -name "found.dll")
[[ "$FOUND" == "$WORK/tree/nested/found.dll" ]] \
    || FAIL "find_first_file did not return the matching file: $FOUND"
[[ -z "$(find_first_file "$WORK/tree" -type f -name "missing.dll")" ]] \
    || FAIL "find_first_file returned a path for a missing file."

echo "Package consumer smoke lib: OK"
