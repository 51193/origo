#!/usr/bin/env bash
# Regression tests for samples/Origo.EvolutionDemo/package-release.sh.
#
# The Release workflow invokes the demo packager from the repository root with a
# relative "./artifacts" path, while the platform archives are exported and
# zipped from temporary directories. The real packaging, archive layout, path
# handling, and .NET payload assertions run here; the .NET build and the Godot
# engine, which the release job supplies through the pinned SDK and the cached
# engine, are replaced with stubs so the regression stays inside the Bash lint
# gate.
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

# Export prerequisites: Godot publishes C# assemblies only for a solution that
# contains the project, and its macOS universal preset requires the ETC2/ASTC
# import flag. Both are plain project files, so pin them here instead of
# waiting for the Release job to fail.
DEMO_PROJECT="$ROOT/samples/Origo.EvolutionDemo"
[[ -f "$DEMO_PROJECT/Origo.EvolutionDemo.sln" ]] \
    || FAIL "demo project is missing the solution file Godot exports from"
grep -qF '"Origo.EvolutionDemo.csproj"' "$DEMO_PROJECT/Origo.EvolutionDemo.sln" \
    || FAIL "demo solution does not reference the demo project"
grep -qxF 'textures/vram_compression/import_etc2_astc=true' \
    "$DEMO_PROJECT/project.godot" \
    || FAIL "demo project does not enable the ETC2/ASTC import flag required by the macOS universal export"

# Under `set -o pipefail`, piping an archive listing into `grep -q` closes the
# pipe on the first match while tar/unzip is still writing and can fail a
# passing check with a spurious write error.
if grep -nE '\|[[:space:]]*grep[[:space:]]+-q' \
    "$DEMO_PROJECT/package-release.sh" >/dev/null; then
    FAIL "package-release.sh pipes an archive listing into grep -q"
fi

# A curated PATH: the tools the packager needs, without a real .NET SDK or Godot
# engine, so this test controls both external boundaries.
BIN="$FIXTURE/bin"
mkdir -p "$BIN"
for tool in bash cat chmod cp dirname find grep gzip head ls mktemp mkdir rm sed tar unzip zip; do
    tool_path="$(command -v "$tool" || true)"
    [[ -n "$tool_path" ]] || FAIL "required tool is missing: $tool"
    ln -s "$tool_path" "$BIN/$tool"
done
printf '#!/bin/sh\nexit 0\n' > "$BIN/dotnet"
chmod +x "$BIN/dotnet"

# Build a throwaway repository root carrying the packager, the Godot resolver,
# and an export stub. `omit_payload_platform` names one preset whose export
# omits the .NET publish payload; `omit_macos_executable` leaves the macOS app
# without its executable. Together they mirror the release failures this
# self-test pins.
write_fake_root() {
    local fake_root="$1"
    local omit_payload_platform="$2"
    local omit_macos_executable="$3"
    mkdir -p "$fake_root/samples/Origo.EvolutionDemo" "$fake_root/scripts/bash"
    cp "$ROOT/samples/Origo.EvolutionDemo/$PACKAGER_NAME" \
        "$fake_root/samples/Origo.EvolutionDemo/$PACKAGER_NAME"
    cat > "$fake_root/scripts/bash/download-godot.sh" <<EOF
#!/usr/bin/env bash
printf '%s\\n' "$fake_root/godot"
EOF
    chmod +x "$fake_root/scripts/bash/download-godot.sh"
    cat > "$fake_root/godot" <<EOF
#!/usr/bin/env bash
# Export stub: create the tree the packager asserts after each export.
set -euo pipefail
omit_payload_platform='$omit_payload_platform'
omit_macos_executable=$omit_macos_executable
preset=""
output=""
while ((\$# > 0)); do
    case "\$1" in
        --export-release)
            preset="\$2"
            output="\$3"
            shift 3
            ;;
        *)
            shift
            ;;
    esac
done
[[ -n "\$output" ]] || exit 0
add_payload() {
    local directory="\$1"
    mkdir -p "\$directory"
    printf 'fixture assembly %s\n' "\$preset" > "\$directory/Origo.EvolutionDemo.dll"
    printf 'fixture logic assembly\n' > "\$directory/Origo.EvolutionDemo.Logic.dll"
}
if [[ "\$preset" == "macOS" ]]; then
    mkdir -p "\$output/Contents/MacOS"
    if [[ "\$omit_macos_executable" != true ]]; then
        printf 'fixture macOS\n' > "\$output/Contents/MacOS/Origo Evolution Demo"
        chmod +x "\$output/Contents/MacOS/Origo Evolution Demo"
    fi
    if [[ "\$omit_payload_platform" != macOS ]]; then
        add_payload "\$output/Contents/Resources/data_Origo.EvolutionDemo_macos_arm64"
        add_payload "\$output/Contents/Resources/data_Origo.EvolutionDemo_macos_x86_64"
    fi
else
    mkdir -p "\$(dirname "\$output")"
    printf 'fixture %s\n' "\$preset" > "\$output"
    chmod +x "\$output"
    case "\$preset" in
        Linux) data_dir="\$(dirname "\$output")/data_Origo.EvolutionDemo_linuxbsd_x86_64" ;;
        Windows) data_dir="\$(dirname "\$output")/data_Origo.EvolutionDemo_windows_x86_64" ;;
        *) echo "unexpected preset: \$preset" >&2; exit 1 ;;
    esac
    if [[ "\$omit_payload_platform" != "\$preset" ]]; then
        add_payload "\$data_dir"
    fi
fi
EOF
    chmod +x "$fake_root/godot"
}

run_packager() {
    local label="$1"
    local fake_root="$2"
    local directory="$3"
    shift 3
    local output code
    set +e
    output="$( cd "$directory" \
        && PATH="$BIN" bash "$fake_root/samples/Origo.EvolutionDemo/$PACKAGER_NAME" "$@" 2>&1 )"
    code=$?
    set -e
    if [[ $code -ne 0 ]]; then
        FAIL "$label: demo packager failed (exit ${code}):
$output"
    fi
}

assert_no_archives() {
    local label="$1"
    local artifacts="$2"
    [[ -z "$(find "$artifacts" -mindepth 1 -maxdepth 1 -print -quit 2>/dev/null)" ]] \
        || FAIL "$label: broken demo package was archived:
$(find "$artifacts" -mindepth 1 -maxdepth 1 -print | sort)"
}

expect_export_failure() {
    local label="$1"
    local fake_root="$2"
    local diagnostic="$3"
    local output code
    set +e
    output="$( cd "$fake_root" \
        && PATH="$BIN" bash "$fake_root/samples/Origo.EvolutionDemo/$PACKAGER_NAME" \
            "$VERSION" ./artifacts 2>&1 )"
    code=$?
    set -e
    [[ $code -ne 0 ]] || FAIL "$label: expected the packager to fail:
$output"
    grep -qF "$diagnostic" <<<"$output" \
        || FAIL "$label: missing diagnostic '${diagnostic}':
$output"
    assert_no_archives "$label" "$fake_root/artifacts"
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

    tar -tzf "$linux_archive" | grep -E '(^|/)OrigoEvolutionDemo\.x86_64$' >/dev/null \
        || FAIL "$label: Linux archive does not carry the exported executable"
    unzip -Z1 "$windows_archive" | grep -E '(^|/)OrigoEvolutionDemo\.exe$' >/dev/null \
        || FAIL "$label: Windows archive does not carry the exported executable"
    unzip -Z1 "$macos_archive" \
        | grep -E 'OrigoEvolutionDemo\.app/Contents/MacOS/[^/]+$' >/dev/null \
        || FAIL "$label: macOS archive does not carry the app executable"
    tar -tzf "$linux_archive" | grep -E '(^|/)README\.txt$' >/dev/null \
        || FAIL "$label: Linux archive is missing README.txt"
    unzip -Z1 "$windows_archive" | grep -E '(^|/)README\.txt$' >/dev/null \
        || FAIL "$label: Windows archive is missing README.txt"
    unzip -Z1 "$macos_archive" | grep -E '(^|/)README\.txt$' >/dev/null \
        || FAIL "$label: macOS archive is missing README.txt"

    tar -tzf "$linux_archive" \
        | grep -E '(^|/)data_Origo\.EvolutionDemo_.*/Origo\.EvolutionDemo\.dll$' >/dev/null \
        || FAIL "$label: Linux archive is missing the .NET publish payload"
    unzip -Z1 "$windows_archive" \
        | grep -E '(^|/)data_Origo\.EvolutionDemo_.*/Origo\.EvolutionDemo\.dll$' >/dev/null \
        || FAIL "$label: Windows archive is missing the .NET publish payload"
    unzip -Z1 "$macos_archive" \
        | grep -E 'data_Origo\.EvolutionDemo_.*/Origo\.EvolutionDemo\.dll$' >/dev/null \
        || FAIL "$label: macOS archive is missing the .NET publish payload"
}

# The Release workflow invocation shape: relative directory, caller cwd is the
# artifacts directory's parent (the repository root in CI).
FAKE_ROOT="$FIXTURE/repo"
write_fake_root "$FAKE_ROOT" "" false
run_packager "relative artifacts path" "$FAKE_ROOT" "$FAKE_ROOT" "$VERSION" ./artifacts
assert_demo_archives "relative artifacts path" "$FAKE_ROOT/artifacts"

# The documented usage accepts an absolute directory from any cwd.
mkdir -p "$FIXTURE/elsewhere"
run_packager "absolute artifacts path" "$FAKE_ROOT" "$FIXTURE/elsewhere" \
    "$VERSION" "$FIXTURE/absolute-artifacts"
assert_demo_archives "absolute artifacts path" "$FIXTURE/absolute-artifacts"

# A platform export without the published .NET payload is not a playable
# package; the packager must fail for every platform instead of archiving it.
while read -r platform preset; do
    broken_root="$FIXTURE/broken-${platform}"
    write_fake_root "$broken_root" "$preset" false
    expect_export_failure "missing ${platform} .NET payload" "$broken_root" \
        "ERROR: ${platform} demo package is missing Origo.EvolutionDemo.dll."
done <<'PLATFORMS'
linux Linux
windows Windows
macos macOS
PLATFORMS

# A macOS app without its executable is not playable either.
NO_EXEC_ROOT="$FIXTURE/broken-macos-executable"
write_fake_root "$NO_EXEC_ROOT" "" true
expect_export_failure "missing macOS app executable" "$NO_EXEC_ROOT" \
    "ERROR: macOS demo export does not contain the app executable."

# Both arguments are required.
set +e
output="$( cd "$FAKE_ROOT" && PATH="$BIN" bash "$FAKE_ROOT/samples/Origo.EvolutionDemo/$PACKAGER_NAME" 2>&1 )"
code=$?
set -e
[[ $code -ne 0 ]] || FAIL "missing arguments must fail"
grep -qF "version required" <<<"$output" \
    || FAIL "missing arguments: missing version diagnostic:
$output"
set +e
output="$( cd "$FAKE_ROOT" \
    && PATH="$BIN" bash "$FAKE_ROOT/samples/Origo.EvolutionDemo/$PACKAGER_NAME" \
        "$VERSION" 2>&1 )"
code=$?
set -e
[[ $code -ne 0 ]] || FAIL "missing artifacts directory must fail"
grep -qF "artifacts directory required" <<<"$output" \
    || FAIL "missing artifacts directory: missing diagnostic:
$output"

echo "Demo release packager: OK"
