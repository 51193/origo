#!/usr/bin/env bash
# Regression tests for scripts/bash/package-framework-artifacts.sh.
#
# The Release workflow invokes the bundler from the repository root with a
# relative "./artifacts" path, while the archive commands run from a temporary
# bundle directory. These fixtures pin the relative-path invocation (the
# release failure this self-test guards), both archive formats and their
# identical contents, the checksum manifest, a suffixed snapshot version, an
# absolute artifacts path from an unrelated directory, and the error paths.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-framework-bundle.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

BUNDLER="$ROOT/scripts/bash/package-framework-artifacts.sh"
VERSION="0.1.1"
SNAPSHOT_VERSION="0.1.1-nightly.20261115"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

package_names() {
    local version="$1"
    printf '%s\n' \
        "Origo.ConsoleBridge.${version}.nupkg" \
        "Origo.Core.${version}.nupkg" \
        "Origo.Core.Contracts.${version}.nupkg" \
        "Origo.Core.Kernel.${version}.nupkg" \
        "Origo.GodotAdapter.${version}.nupkg"
}

expected_members() {
    local version="$1"
    printf '%s\n' README.txt SHA256SUMS
    package_names "$version"
    printf '%s\n' nuget.config
}

reset_artifacts() {
    local version="$1"
    local name
    rm -rf "$FIXTURE/artifacts"
    mkdir -p "$FIXTURE/artifacts"
    while IFS= read -r name; do
        printf 'fixture %s\n' "$name" > "$FIXTURE/artifacts/$name"
    done < <(package_names "$version")
    printf 'fixture nuget.config\n' > "$FIXTURE/artifacts/nuget.config"
}

verify_manifest() {
    local label="$1"
    local directory="$2"
    if command -v sha256sum >/dev/null 2>&1; then
        ( cd "$directory" && sha256sum -c SHA256SUMS >/dev/null ) \
            || FAIL "$label: SHA256SUMS does not verify"
    elif command -v shasum >/dev/null 2>&1; then
        ( cd "$directory" && shasum -a 256 -c SHA256SUMS >/dev/null ) \
            || FAIL "$label: SHA256SUMS does not verify"
    else
        FAIL "$label: neither sha256sum nor shasum is available"
    fi
}

verify_bundles() {
    local label="$1"
    local version="$2"
    local output="$3"
    local zip_path="$FIXTURE/artifacts/origo-framework-${version}.zip"
    local tar_path="$FIXTURE/artifacts/origo-framework-${version}.tar.gz"
    local zip_dir="$FIXTURE/zip-bundle"
    local tar_dir="$FIXTURE/tar-bundle"
    local expected_member_list actual_member_list

    [[ -s "$zip_path" ]] || FAIL "$label: missing or empty ${zip_path}"
    [[ -s "$tar_path" ]] || FAIL "$label: missing or empty ${tar_path}"
    grep -qF "${zip_path}" <<<"$output" \
        || FAIL "$label: success output does not name the zip bundle:
$output"
    grep -qF "${tar_path}" <<<"$output" \
        || FAIL "$label: success output does not name the tar.gz bundle:
$output"

    expected_member_list="$(expected_members "$version" | LC_ALL=C sort)"
    for archive in "$zip_path" "$tar_path"; do
        if [[ "$archive" == *.zip ]]; then
            actual_member_list="$(unzip -Z1 "$archive" | LC_ALL=C sort)"
        else
            actual_member_list="$(tar -tzf "$archive" | LC_ALL=C sort)"
        fi
        [[ "$actual_member_list" == "$expected_member_list" ]] \
            || FAIL "$label: unexpected members in $(basename "$archive"):
$actual_member_list"
    done

    rm -rf "$zip_dir" "$tar_dir"
    mkdir -p "$zip_dir" "$tar_dir"
    unzip -q "$zip_path" -d "$zip_dir"
    tar -xzf "$tar_path" -C "$tar_dir"
    grep -qF "$version" "$zip_dir/README.txt" \
        || FAIL "$label: bundle README.txt does not name version ${version}"
    verify_manifest "$label (zip)" "$zip_dir"
    verify_manifest "$label (tar.gz)" "$tar_dir"
    diff -r "$zip_dir" "$tar_dir" >/dev/null \
        || FAIL "$label: zip and tar.gz bundle contents differ"
}

expect_relative_path_pass() {
    local label="$1"
    local version="$2"
    local output
    if ! output="$( cd "$FIXTURE" && bash "$BUNDLER" ./artifacts "$version" 2>&1 )"; then
        FAIL "$label: bundler failed with a relative artifacts path:
$output"
    fi
    verify_bundles "$label" "$version" "$output"
}

expect_absolute_path_pass() {
    local label="$1"
    local version="$2"
    local output
    mkdir -p "$FIXTURE/elsewhere"
    if ! output="$( cd "$FIXTURE/elsewhere" \
        && bash "$BUNDLER" "$FIXTURE/artifacts" "$version" 2>&1 )"; then
        FAIL "$label: bundler failed with an absolute artifacts path:
$output"
    fi
    verify_bundles "$label" "$version" "$output"
}

expect_failure() {
    local label="$1"
    local diagnostic="$2"
    local expected_code="$3"
    shift 3
    local output code
    set +e
    output="$( cd "$FIXTURE" && bash "$BUNDLER" "$@" 2>&1 )"
    code=$?
    set -e
    [[ $code -eq $expected_code ]] \
        || FAIL "$label: expected exit ${expected_code}, got ${code}:
$output"
    grep -qF "$diagnostic" <<<"$output" \
        || FAIL "$label: missing diagnostic '${diagnostic}':
$output"
}

# The Release workflow's invocation shape: relative directory, caller cwd is
# the artifacts directory's parent (the repository root in CI).
reset_artifacts "$VERSION"
expect_relative_path_pass "relative artifacts path" "$VERSION"

# The documented usage accepts an absolute directory from any cwd.
reset_artifacts "$VERSION"
expect_absolute_path_pass "absolute artifacts path" "$VERSION"

# Snapshot tags carry a suffix; the bundle names must follow the tag version.
reset_artifacts "$SNAPSHOT_VERSION"
expect_relative_path_pass "suffixed snapshot version" "$SNAPSHOT_VERSION"

# macOS provides shasum instead of GNU sha256sum; the bundler must still build
# a verifiable manifest. Exercise that branch on a host that has sha256sum.
if command -v sha256sum >/dev/null 2>&1; then
    HASH_BIN="$FIXTURE/hash-bin"
    mkdir -p "$HASH_BIN"
    for tool in bash cat cp gzip mktemp mkdir rm sort tar unzip zip; do
        tool_path="$(command -v "$tool" || true)"
        [[ -n "$tool_path" ]] || FAIL "required tool is missing: $tool"
        ln -s "$tool_path" "$HASH_BIN/$tool"
    done
    sha256sum_bin="$(command -v sha256sum)"
    {
        printf '#!/usr/bin/env bash\n'
        printf '# Minimal shasum -a 256 for the fallback path: delegate to sha256sum.\n'
        printf '[[ "$1" == "-a" && "$2" == "256" ]] || exit 2\n'
        printf 'shift 2\n'
        printf 'exec "%s" "$@"\n' "$sha256sum_bin"
    } > "$HASH_BIN/shasum"
    chmod +x "$HASH_BIN/shasum"
    reset_artifacts "$VERSION"
    if ! output="$( cd "$FIXTURE" && PATH="$HASH_BIN" bash "$BUNDLER" ./artifacts "$VERSION" 2>&1 )"; then
        FAIL "shasum fallback: bundler failed:
$output"
    fi
    verify_bundles "shasum fallback" "$VERSION" "$output"
fi

expect_failure "missing arguments" "ERROR: usage:" 2
reset_artifacts "$VERSION"
expect_failure "missing artifacts directory" \
    "ERROR: artifacts directory not found: ./missing-artifacts" 2 \
    ./missing-artifacts "$VERSION"
expect_failure "invalid version" "must be x.y.z" 2 \
    ./artifacts not-a-version
reset_artifacts "$VERSION"
rm -f "$FIXTURE/artifacts/Origo.Core.${VERSION}.nupkg"
expect_failure "missing framework package" \
    "ERROR: missing package for framework bundle: Origo.Core.${VERSION}.nupkg" 1 \
    ./artifacts "$VERSION"
reset_artifacts "$VERSION"
rm -f "$FIXTURE/artifacts/nuget.config"
expect_failure "missing nuget.config" \
    "ERROR: missing nuget.config for framework bundle." 1 \
    ./artifacts "$VERSION"

echo "Framework artifact bundler: OK"
