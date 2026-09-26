#!/usr/bin/env bash
# Focused regression tests for scripts/package-consumer-smoke-lib.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
source "$ROOT/scripts/package-consumer-smoke-lib.sh"

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

# A stub unzip proves the archive check does not regress to `unzip | grep -q`.
# It reports a matching entry and then keeps writing; with the old pipeline grep
# exits after the first line, the stub gets SIGPIPE, and pipefail turns the
# successful match into a failure.
STUB="$WORK/stub"
mkdir -p "$STUB/bin"
cat > "$STUB/bin/unzip" <<'STUB_UNZIP'
#!/usr/bin/env bash
printf 'analyzers/dotnet/cs/Origo.SourceGeneration.dll\n'
sleep 0.1
for ((i = 0; i < 10000; i++)); do
    printf 'padding-%d\n' "$i"
done
STUB_UNZIP
chmod +x "$STUB/bin/unzip"
if ! PATH="$STUB/bin:$PATH" package_contains_zip_entry \
    "$WORK/fake.nupkg" "analyzers/dotnet/cs/Origo.SourceGeneration.dll"; then
    FAIL "package_contains_zip_entry rejected a matching archive stream."
fi

cat > "$STUB/bin/unzip" <<'STUB_UNZIP'
#!/usr/bin/env bash
exit 11
STUB_UNZIP
chmod +x "$STUB/bin/unzip"
if PATH="$STUB/bin:$PATH" package_contains_zip_entry "$WORK/fake.nupkg" "missing.dll"; then
    FAIL "package_contains_zip_entry accepted a missing archive entry."
fi

echo "Package consumer smoke lib: OK"
