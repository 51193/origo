#!/usr/bin/env bash
# Regression tests for the Evolution Demo export-template downloader.
#
# The downloader must reject a template archive whose SHA-512 does not match
# the repository-pinned digest before unpacking it. The network and Godot
# version resolver are replaced with fixtures so this test stays deterministic.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-demo-templates.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

for tool in bash cp dirname grep mkdir mktemp rm unzip zip awk; do
    command -v "$tool" >/dev/null 2>&1 || FAIL "required tool is missing: $tool"
done
command -v sha512sum >/dev/null 2>&1 || command -v shasum >/dev/null 2>&1 \
    || FAIL "sha512sum or shasum is required"

CHECKSUM_FILE="$ROOT/samples/Origo.EvolutionDemo/export-templates.sha512"
grep -Eq '^[0-9a-f]{128}  Godot_v[0-9.]+-stable_mono_export_templates\.tpz$' "$CHECKSUM_FILE" \
    || FAIL "pinned export-template checksum file is malformed"
CURRENT_VERSION="$(bash "$ROOT/scripts/bash/download-godot.sh" --print-version)"
grep -Fq "  Godot_v${CURRENT_VERSION}-stable_mono_export_templates.tpz" "$CHECKSUM_FILE" \
    || FAIL "no pinned export-template digest for the current Godot version: $CURRENT_VERSION"

FAKE="$FIXTURE/repo"
DEMO="$FAKE/samples/Origo.EvolutionDemo"
mkdir -p "$DEMO" "$FAKE/scripts/bash" "$FIXTURE/bin" "$FIXTURE/home"
cp "$ROOT/samples/Origo.EvolutionDemo/install-export-templates.sh" "$DEMO/"
cp "$ROOT/samples/Origo.EvolutionDemo/export-templates.sha512" "$DEMO/"
printf '#!/usr/bin/env bash\nprintf "%%s\\n" "4.7.2"\n' > "$FAKE/scripts/bash/download-godot.sh"

cat > "$FIXTURE/bin/curl" <<'CURL'
#!/usr/bin/env bash
set -euo pipefail
output=""
while (($# > 0)); do
    case "$1" in
        -o)
            output="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done
[[ -n "$output" ]] || exit 2
cp "$SERVE_ARCHIVE" "$output"
CURL
chmod +x "$FIXTURE/bin/curl"

ARCHIVE="Godot_v4.7.2-stable_mono_export_templates.tpz"
make_archive() {
    local destination="$1"
    local marker="$2"
    local staging="$FIXTURE/staging-$marker"
    mkdir -p "$staging/templates"
    printf 'fixture linux %s\n' "$marker" > "$staging/templates/linux_release.x86_64"
    printf 'fixture windows %s\n' "$marker" > "$staging/templates/windows_release_x86_64.exe"
    printf 'fixture macos %s\n' "$marker" > "$staging/templates/macos.zip"
    (cd "$staging" && zip -q -r "$destination" templates)
}

file_sha512() {
    if command -v sha512sum >/dev/null 2>&1; then
        sha512sum "$1" | awk '{ print $1 }'
    else
        shasum -a 512 "$1" | awk '{ print $1 }'
    fi
}

write_checksums() {
    local archive_name="$1"
    local digest="$2"
    printf '%s  %s\n' "$digest" "$archive_name" > "$DEMO/export-templates.sha512"
}

run_install() {
    local archive="$1"
    HOME="$FIXTURE/home" \
    SERVE_ARCHIVE="$archive" \
    PATH="$FIXTURE/bin:$PATH" \
        bash "$DEMO/install-export-templates.sh"
}

GOOD="$FIXTURE/good.tpz"
TAMPERED="$FIXTURE/tampered.tpz"
make_archive "$GOOD" good
make_archive "$TAMPERED" tampered
GOOD_SHA512="$(file_sha512 "$GOOD")"
[[ "$GOOD_SHA512" != "$(file_sha512 "$TAMPERED")" ]] || FAIL "test archives are identical"

# A correctly pinned archive installs all three runtime templates.
write_checksums "$ARCHIVE" "$GOOD_SHA512"
run_install "$GOOD" >/dev/null
TEMPLATE_DIR="$FIXTURE/home/.local/share/godot/export_templates/4.7.2.stable.mono"
for template in linux_release.x86_64 windows_release_x86_64.exe macos.zip; do
    [[ -f "$TEMPLATE_DIR/$template" ]] || FAIL "valid archive did not install $template"
done

# A tampered archive with the pinned digest's name must fail before unpacking.
rm -rf "${FIXTURE:?}/home"
write_checksums "$ARCHIVE" "$GOOD_SHA512"
if run_install "$TAMPERED" >"$FIXTURE/mismatch.log" 2>&1; then
    FAIL "tampered export-template archive was accepted"
fi
grep -F 'SHA-512 mismatch' "$FIXTURE/mismatch.log" >/dev/null \
    || FAIL "tampered archive did not report a SHA-512 mismatch"
[[ ! -e "$TEMPLATE_DIR" ]] || FAIL "tampered archive was unpacked"

# A Godot version without a pinned entry fails before downloading.
rm -rf "${FIXTURE:?}/home"
write_checksums "Godot_v0.0.0-stable_mono_export_templates.tpz" "$GOOD_SHA512"
if run_install "$GOOD" >"$FIXTURE/unpinned.log" 2>&1; then
    FAIL "unpinned export-template version was accepted"
fi
grep -F 'no pinned SHA-512' "$FIXTURE/unpinned.log" >/dev/null \
    || FAIL "unpinned version did not report the missing checksum"

echo "Demo export-template checksum: OK"
