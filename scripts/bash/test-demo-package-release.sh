#!/usr/bin/env bash
# Regression tests for samples/Origo.EvolutionDemo/package-release.sh.
#
# The Release workflow invokes the demo packager from the repository root with a
# relative "./artifacts" path, while the platform archives are zipped from
# temporary export directories. The real packaging, archive layout, and path
# handling run here; the .NET build and the Godot engine, which the release job
# supplies through the pinned SDK and the cached engine, are replaced with
# stubs so the regression stays inside the Bash lint gate.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-demo-release.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

VERSION="0.1.1"
PACKAGER_NAME="package-release.sh"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

# Copy the packager into a throwaway repository root so its own root and demo
# project resolution stay real.
FAKE_ROOT="$FIXTURE/repo"
PACKAGER="$FAKE_ROOT/samples/Origo.EvolutionDemo/$PACKAGER_NAME"
mkdir -p "$FAKE_ROOT/samples/Origo.EvolutionDemo" "$FAKE_ROOT/scripts/bash"
cp "$ROOT/samples/Origo.EvolutionDemo/$PACKAGER_NAME" "$PACKAGER"

# A curated PATH: the tools the packager needs, without a real .NET SDK or Godot
# engine, so this test controls both external boundaries.
BIN="$FIXTURE/bin"
mkdir -p "$BIN"
for tool in bash cat chmod cp dirname grep gzip head ls mktemp mkdir rm sed tar unzip zip; do
    tool_path="$(command -v "$tool" || true)"
    [[ -n "$tool_path" ]] || FAIL "required tool is missing: $tool"
    ln -s "$tool_path" "$BIN/$tool"
done
printf '#!/bin/sh\nexit 0\n' > "$BIN/dotnet"
chmod +x "$BIN/dotnet"

# The Godot resolver prints the export stub the packager must call.
GODOT_STUB="$FIXTURE/godot"
cat > "$FAKE_ROOT/scripts/bash/download-godot.sh" <<EOF
#!/usr/bin/env bash
printf '%s\\n' "$GODOT_STUB"
EOF
chmod +x "$FAKE_ROOT/scripts/bash/download-godot.sh"
cat > "$GODOT_STUB" <<'GODOT_EOF'
#!/usr/bin/env bash
# Export stub: create the tree the packager asserts after each export.
set -euo pipefail
preset=""
output=""
while (($# > 0)); do
    case "$1" in
        --export-release)
            preset="$2"
            output="$3"
            shift 3
            ;;
        *)
            shift
            ;;
    esac
done
[[ -n "$output" ]] || exit 0
mkdir -p "$(dirname "$output")"
if [[ "$preset" == "macOS" ]]; then
    mkdir -p "$output/Contents/MacOS"
    printf 'fixture macOS\n' > "$output/Contents/MacOS/OrigoEvolutionDemo"
else
    printf 'fixture %s\n' "$preset" > "$output"
    chmod +x "$output"
fi
GODOT_EOF
chmod +x "$GODOT_STUB"

run_packager() {
    local label="$1"
    local directory="$2"
    shift 2
    local output code
    set +e
    output="$( cd "$directory" && PATH="$BIN" bash "$PACKAGER" "$@" 2>&1 )"
    code=$?
    set -e
    if [[ $code -ne 0 ]]; then
        FAIL "$label: demo packager failed (exit ${code}):
$output"
    fi
}

assert_demo_archives() {
    local label="$1"
    local artifacts="$2"
    local linux_archive="$artifacts/origo-evolution-demo-${VERSION}-linux-x86_64.tar.gz"
    local windows_archive="$artifacts/origo-evolution-demo-${VERSION}-windows-x86_64.zip"
    local macos_archive="$artifacts/origo-evolution-demo-${VERSION}-macos-universal.zip"
    local archive

    for archive in "$linux_archive" "$windows_archive" "$macos_archive"; do
        [[ -s "$archive" ]] || FAIL "$label: missing or empty ${archive}"
    done
    [[ "$(find "$artifacts" -mindepth 1 -maxdepth 1 -type f | wc -l)" -eq 3 ]] \
        || FAIL "$label: expected exactly three demo archives in ${artifacts}:
$(find "$artifacts" -mindepth 1 -maxdepth 1 -type f | sort)"

    tar -tzf "$linux_archive" | grep -qE '(^|/)OrigoEvolutionDemo\.x86_64$' \
        || FAIL "$label: Linux archive does not carry the exported executable"
    unzip -Z1 "$windows_archive" | grep -qE '(^|/)OrigoEvolutionDemo\.exe$' \
        || FAIL "$label: Windows archive does not carry the exported executable"
    unzip -Z1 "$macos_archive" \
        | grep -q 'OrigoEvolutionDemo\.app/Contents/MacOS/OrigoEvolutionDemo$' \
        || FAIL "$label: macOS archive does not carry the app bundle layout"
    tar -tzf "$linux_archive" | grep -qE '(^|/)README\.txt$' \
        || FAIL "$label: Linux archive is missing README.txt"
    unzip -Z1 "$windows_archive" | grep -qE '(^|/)README\.txt$' \
        || FAIL "$label: Windows archive is missing README.txt"
    unzip -Z1 "$macos_archive" | grep -qE '(^|/)README\.txt$' \
        || FAIL "$label: macOS archive is missing README.txt"
}

# The Release workflow invocation shape: relative directory, caller cwd is the
# artifacts directory's parent (the repository root in CI).
run_packager "relative artifacts path" "$FAKE_ROOT" "$VERSION" ./artifacts
assert_demo_archives "relative artifacts path" "$FAKE_ROOT/artifacts"

# The documented usage accepts an absolute directory from any cwd.
mkdir -p "$FIXTURE/elsewhere"
run_packager "absolute artifacts path" "$FIXTURE/elsewhere" \
    "$VERSION" "$FIXTURE/absolute-artifacts"
assert_demo_archives "absolute artifacts path" "$FIXTURE/absolute-artifacts"

# Both arguments are required.
set +e
output="$( cd "$FAKE_ROOT" && PATH="$BIN" bash "$PACKAGER" 2>&1 )"
code=$?
set -e
[[ $code -ne 0 ]] || FAIL "missing arguments must fail"
grep -qF "version required" <<<"$output" \
    || FAIL "missing arguments: missing version diagnostic:
$output"
set +e
output="$( cd "$FAKE_ROOT" && PATH="$BIN" bash "$PACKAGER" "$VERSION" 2>&1 )"
code=$?
set -e
[[ $code -ne 0 ]] || FAIL "missing artifacts directory must fail"
grep -qF "artifacts directory required" <<<"$output" \
    || FAIL "missing artifacts directory: missing diagnostic:
$output"

echo "Demo release packager: OK"
