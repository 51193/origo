#!/usr/bin/env bash
# Regression tests for the SDK bootstrap: scripts/dotnet-env.sh,
# scripts/install-dotnet.sh, and the repository-root ./dotnet wrapper.
#
# Everything is hermetic: fixture repositories use stub SDKs, a stub curl, and
# a stub installer, so the tests never download an SDK or touch the real
# .dotnet/. Runs from scripts/lint-scripts.sh so a stale-SDK regression cannot
# reach contributors again.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-dotnet-env.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

# Hermetic environment: fixtures must not inherit a real DOTNET_INSTALL_DIR, an
# SDK root override, or the user's ~/.dotnet (which is a selection candidate).
unset DOTNET_INSTALL_DIR DOTNET_ROOT DOTNET_CLI_HOME NUGET_PACKAGES
export HOME="$FIXTURE/home-default"
mkdir -p "$HOME"
export TMPDIR="$FIXTURE/tmp"
mkdir -p "$TMPDIR"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

# Creates a fixture repository with the real bootstrap scripts and a stubbed
# global.json, so the scripts resolve ROOT to the fixture.
make_repo() {
    local dir="$1"
    local version="$2"
    mkdir -p "$dir/scripts"
    cp "$ROOT/scripts/dotnet-env.sh" "$dir/scripts/dotnet-env.sh"
    cp "$ROOT/scripts/install-dotnet.sh" "$dir/scripts/install-dotnet.sh"
    cp "$ROOT/dotnet" "$dir/dotnet"
    chmod +x "$dir/dotnet"
    printf '{\n  "sdk": {\n    "version": "%s",\n    "rollForward": "latestMinor",\n    "allowPrerelease": false\n  }\n}\n' \
        "$version" > "$dir/global.json"
}

# Writes a stub SDK. `--version` prints VERSION and exits with EXIT_CODE;
# `--list-sdks` always succeeds so the failure path can report installed SDKs.
write_stub_dotnet() {
    local path="$1"
    local version="$2"
    local exit_code="$3"
    mkdir -p "$(dirname "$path")"
    cat > "$path" <<STUB
#!/usr/bin/env bash
if [[ -n "\${ORIGO_STUB_CWD_LOG:-}" ]]; then
    printf '%s\n' "\$PWD" >> "\$ORIGO_STUB_CWD_LOG"
fi
if [[ "\${1:-}" == "--list-sdks" ]]; then
    printf '%s [%s]\n' "$version" "\$(cd "\$(dirname "\$0")" && pwd)"
    exit 0
fi
if [[ "\${1:-}" == "--version" ]]; then
    printf '%s\n' "\${ORIGO_STUB_VERSION:-$version}"
    exit $exit_code
fi
printf '%s\n' "\${ORIGO_STUB_VERSION:-$version}"
exit $exit_code
STUB
    chmod +x "$path"
}

# Writes a stub curl that emits a stub dotnet-install which records the chosen
# --install-dir and creates a matching SDK stub there.
write_stub_curl() {
    local bin="$1"
    mkdir -p "$bin"
    cat > "$bin/curl" <<'CURL_STUB'
#!/usr/bin/env bash
out=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        -o)
            out="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done
[[ -n "$out" ]] || { echo "curl stub: missing -o" >&2; exit 1; }
cat > "$out" <<'INSTALLER_STUB'
#!/usr/bin/env bash
target=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --install-dir)
            target="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done
[[ -n "$target" ]] || { echo "installer stub: missing --install-dir" >&2; exit 1; }
printf 'target=%s\n' "$target" >> "${ORIGO_INSTALL_LOG:?}"
mkdir -p "$target"
cat > "$target/dotnet" <<'DOTNET_STUB'
#!/usr/bin/env bash
if [[ "${1:-}" == "--list-sdks" ]]; then
    printf '%s [%s]\n' "${ORIGO_STUB_VERSION:-10.0.402}" "$(cd "$(dirname "$0")" && pwd)"
    exit 0
fi
printf '%s\n' "${ORIGO_STUB_VERSION:-10.0.402}"
exit "${ORIGO_STUB_EXIT:-0}"
DOTNET_STUB
chmod +x "$target/dotnet"
INSTALLER_STUB
CURL_STUB
    chmod +x "$bin/curl"
}

last_install_target() {
    tail -1 "$ORIGO_INSTALL_LOG" | sed -nE 's/^target=(.*)$/\1/p'
}

# --- A: stale repository-local SDK fails fast -------------------------------

echo "=== A: stale repository-local SDK fails fast ==="
repo="$FIXTURE/repo-stale"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.401" 155

set +e
output=$( ( unset ROOT; cd "$repo" && source scripts/dotnet-env.sh && echo REACHED ) 2>&1 )
status=$?
set -e
[[ $status -ne 0 ]] || FAIL "A: a stale repository-local SDK did not fail the selection"
[[ "$output" != *"REACHED"* ]] || FAIL "A: sourcing continued after the failed selection"
[[ "$output" == *"10.0.402"* ]] || FAIL "A: the required version is missing from the message"
[[ "$output" == *"$repo/.dotnet"* ]] || FAIL "A: the selected SDK path is missing from the message"
[[ "$output" == *"installed SDKs:"* ]] || FAIL "A: the message does not list the installed SDKs"
[[ "$output" == *"install-dotnet.sh"* ]] || FAIL "A: the fix command is missing from the message"
[[ "$output" != *"update"*"global.json"* ]] || FAIL "A: the message tells the user to edit global.json"

# The validation must run from the repository root, not the caller's directory.
mkdir -p "$FIXTURE/elsewhere"
export ORIGO_STUB_CWD_LOG="$FIXTURE/stub-cwd.log"
set +e
( unset ROOT; cd "$FIXTURE/elsewhere" && source "$repo/scripts/dotnet-env.sh" ) >/dev/null 2>&1
set -e
unset ORIGO_STUB_CWD_LOG
[[ "$(head -1 "$FIXTURE/stub-cwd.log")" == "$repo" ]] \
    || FAIL "A: the SDK check did not run from the repository root"

# --- B: satisfied repository-local SDK is exported -------------------------

echo "=== B: satisfied repository-local SDK is exported ==="
repo="$FIXTURE/repo-ok"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.402" 0

output=$( ( unset ROOT; cd "$repo" && source scripts/dotnet-env.sh &&
    printf 'version=%s root=%s path=%s' \
        "$(dotnet --version)" "${DOTNET_ROOT:-unset}" "${PATH%%:*}" ) 2>&1 )
[[ "$output" == "version=10.0.402 root=$repo/.dotnet path=$repo/.dotnet" ]] \
    || FAIL "B: satisfying SDK was not exported as expected: $output"

# --- C: system fallback is validated ---------------------------------------

echo "=== C: system fallback is validated ==="
repo="$FIXTURE/repo-system"
make_repo "$repo" "10.0.402"
sysbin="$FIXTURE/sysbin"
write_stub_dotnet "$sysbin/dotnet" "10.0.402" 0

output=$( env -u DOTNET_ROOT PATH="$sysbin:$PATH" bash -c \
    'cd "$1" && source scripts/dotnet-env.sh && printf "version=%s root=%s" "$(dotnet --version)" "${DOTNET_ROOT:-unset}"' \
    _ "$repo" 2>&1 )
[[ "$output" == "version=10.0.402 root=unset" ]] \
    || FAIL "C: system dotnet fallback was not used as expected: $output"

sysbin_old="$FIXTURE/sysbin-old"
write_stub_dotnet "$sysbin_old/dotnet" "10.0.401" 155
set +e
output=$( env -u DOTNET_ROOT PATH="$sysbin_old:$PATH" bash -c \
    'cd "$1" && source scripts/dotnet-env.sh && echo REACHED' _ "$repo" 2>&1 )
status=$?
set -e
[[ $status -ne 0 ]] || FAIL "C: an incompatible system dotnet did not fail the selection"
[[ "$output" != *"REACHED"* ]] || FAIL "C: sourcing continued after the incompatible system dotnet"
[[ "$output" == *"install-dotnet.sh"* ]] || FAIL "C: the system fallback message is not actionable"

# A repository root on PATH must not make the wrapper select itself.
repo="$FIXTURE/repo-system-wrapper"
make_repo "$repo" "10.0.402"
output=$( env -u DOTNET_ROOT PATH="$repo:$sysbin:$PATH" bash -c \
    'cd "$1" && source scripts/dotnet-env.sh && printf "%s" "$ORIGO_DOTNET_EXE"' \
    _ "$repo" 2>&1 )
[[ "$output" == "$sysbin/dotnet" ]] \
    || FAIL "C: the repository wrapper was selected as the system dotnet: $output"
if command -v timeout >/dev/null 2>&1; then
    output=$( timeout 30 env -u DOTNET_ROOT PATH="$repo:$sysbin:$PATH" "$repo/dotnet" --version 2>&1 )
    [[ "$output" == "10.0.402" ]] \
        || FAIL "C: the wrapper did not run the selected system dotnet: $output"
fi

# --- G: the user default root is a candidate before PATH ------------------

echo "=== G: user default root before PATH ==="
repo="$FIXTURE/repo-home-candidate"
make_repo "$repo" "10.0.402"
home_sdk="$FIXTURE/home-with-sdk"
write_stub_dotnet "$home_sdk/.dotnet/dotnet" "10.0.402" 0
sysbin_stale="$FIXTURE/sysbin-stale"
write_stub_dotnet "$sysbin_stale/dotnet" "10.0.401" 155
output=$( env HOME="$home_sdk" PATH="$sysbin_stale:$PATH" bash -c \
    'cd "$1" && source scripts/dotnet-env.sh && printf "%s|%s" "$ORIGO_DOTNET_EXE" "$DOTNET_ROOT"' \
    _ "$repo" 2>&1 )
[[ "$output" == "$home_sdk/.dotnet/dotnet|$home_sdk/.dotnet" ]] \
    || FAIL "G: the user default root was not selected before PATH: $output"
if command -v timeout >/dev/null 2>&1; then
    output=$( timeout 30 env HOME="$home_sdk" PATH="$sysbin_stale:$PATH" bash -c \
        'cd "$1" && source scripts/dotnet-env.sh && dotnet --version' _ "$repo" 2>&1 )
    [[ "$output" == "10.0.402" ]] || FAIL "G: the selected user default SDK did not run: $output"
fi

# --- D: no dotnet at all ----------------------------------------------------

echo "=== D: no dotnet at all ==="
emptybin="$FIXTURE/emptybin"
mkdir -p "$emptybin"
for tool in sed head mkdir dirname cut; do
    tool_path="$(command -v "$tool" || true)"
    if [[ -n "$tool_path" ]]; then
        ln -sf "$tool_path" "$emptybin/$tool"
    fi
done
set +e
output=$( env -u DOTNET_ROOT -u DOTNET_INSTALL_DIR PATH="$emptybin" "$BASH" -c \
    'cd "$1" && source scripts/dotnet-env.sh && echo REACHED' _ "$repo" 2>&1 )
status=$?
set -e
[[ $status -ne 0 ]] || FAIL "D: a missing dotnet did not fail the selection"
[[ "$output" == *"no dotnet executable"* ]] || FAIL "D: the missing-SDK message is not actionable"

# --- E: installer target selection and verification -----------------------

echo "=== E: installer target selection ==="
curlbin="$FIXTURE/curlbin"
write_stub_curl "$curlbin"
export ORIGO_INSTALL_LOG="$FIXTURE/install.log"

run_installer() {
    local repo_dir="$1"
    local home_dir="$2"
    mkdir -p "$home_dir"
    : > "$ORIGO_INSTALL_LOG"
    env HOME="$home_dir" PATH="$curlbin:$PATH" bash "$repo_dir/scripts/install-dotnet.sh" \
        > "$FIXTURE/install.out" 2>&1
}

# E1: an existing repository-local SDK is refreshed even when HOME is writable.
repo="$FIXTURE/repo-install-local"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.401" 155
run_installer "$repo" "$FIXTURE/home-e1"
[[ "$(last_install_target)" == "$repo/.dotnet" ]] \
    || FAIL "E1: the installer did not refresh the repository-local SDK: $(last_install_target)"

# E2: without a local SDK the user default is used.
repo="$FIXTURE/repo-install-home"
make_repo "$repo" "10.0.402"
home_e2="$FIXTURE/home-e2"
run_installer "$repo" "$home_e2"
[[ "$(last_install_target)" == "$home_e2/.dotnet" ]] \
    || FAIL "E2: the installer did not use the user default: $(last_install_target)"

# E3: an explicit DOTNET_INSTALL_DIR wins when no local SDK exists.
repo="$FIXTURE/repo-install-custom"
make_repo "$repo" "10.0.402"
: > "$ORIGO_INSTALL_LOG"
env HOME="$FIXTURE/home-e3" DOTNET_INSTALL_DIR="$FIXTURE/custom-e3" \
    PATH="$curlbin:$PATH" bash "$repo/scripts/install-dotnet.sh" > "$FIXTURE/install.out" 2>&1
[[ "$(last_install_target)" == "$FIXTURE/custom-e3" ]] \
    || FAIL "E3: DOTNET_INSTALL_DIR was ignored: $(last_install_target)"

# E4: DOTNET_INSTALL_DIR wins over an existing local SDK, matching dotnet-env.
repo="$FIXTURE/repo-install-custom-local"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.401" 155
: > "$ORIGO_INSTALL_LOG"
env HOME="$FIXTURE/home-e4" DOTNET_INSTALL_DIR="$FIXTURE/custom-e4" \
    PATH="$curlbin:$PATH" bash "$repo/scripts/install-dotnet.sh" > "$FIXTURE/install.out" 2>&1
[[ "$(last_install_target)" == "$FIXTURE/custom-e4" ]] \
    || FAIL "E4: DOTNET_INSTALL_DIR did not take precedence: $(last_install_target)"

# E5: a post-install SDK that does not satisfy global.json fails the installer.
repo="$FIXTURE/repo-install-invalid"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.401" 155
set +e
env HOME="$FIXTURE/home-e5" PATH="$curlbin:$PATH" ORIGO_STUB_EXIT=155 \
    bash "$repo/scripts/install-dotnet.sh" > "$FIXTURE/install.out" 2>&1
status=$?
set -e
[[ $status -ne 0 ]] || FAIL "E5: the installer accepted an SDK that does not satisfy global.json"
grep -q "does not satisfy" "$FIXTURE/install.out" \
    || FAIL "E5: the installer did not report the unsatisfied SDK"

# E6: an already satisfying SDK needs no download.
repo="$FIXTURE/repo-install-satisfied"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.402" 0
: > "$ORIGO_INSTALL_LOG"
env HOME="$FIXTURE/home-e6" PATH="$curlbin:$PATH" \
    bash "$repo/scripts/install-dotnet.sh" > "$FIXTURE/install.out" 2>&1
[[ ! -s "$ORIGO_INSTALL_LOG" ]] \
    || FAIL "E6: the installer downloaded an SDK that already satisfied global.json"
grep -q "already satisfies" "$FIXTURE/install.out" \
    || FAIL "E6: the installer did not report the already satisfying SDK"

# E7: an empty repository-local directory is refreshed, not skipped.
repo="$FIXTURE/repo-install-empty"
make_repo "$repo" "10.0.402"
mkdir -p "$repo/.dotnet"
run_installer "$repo" "$FIXTURE/home-e7"
[[ "$(last_install_target)" == "$repo/.dotnet" ]] \
    || FAIL "E7: the empty local root was not refreshed: $(last_install_target)"

# --- I: an unwritable CLI home falls back to a temp directory -------------

echo "=== I: unwritable CLI home falls back to temp ==="
repo="$FIXTURE/repo-blocked-cli-home"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.402" 0
: > "$repo/.dotnet-cli-home"
output=$( ( unset ROOT; cd "$repo" && source scripts/dotnet-env.sh && dotnet --version ) 2>&1 )
[[ "$output" == "10.0.402" ]] \
    || FAIL "I: a file at the repository CLI home broke the selection: $output"

repo="$FIXTURE/repo-blocked-cli-home-install"
make_repo "$repo" "10.0.402"
: > "$repo/.dotnet-cli-home"
run_installer "$repo" "$FIXTURE/home-i"
[[ "$(last_install_target)" == "$FIXTURE/home-i/.dotnet" ]] \
    || FAIL "I: a file at the repository CLI home broke the installer: $(last_install_target)"

unset ORIGO_INSTALL_LOG

# --- H: invalid explicit roots and a missing pin fail fast ----------------

echo "=== H: invalid roots and a missing pin fail fast ==="
repo="$FIXTURE/repo-explicit-wrapper"
make_repo "$repo" "10.0.402"
set +e
output=$( env DOTNET_INSTALL_DIR="$repo" bash -c \
    'cd "$1" && source scripts/dotnet-env.sh && echo REACHED' _ "$repo" 2>&1 )
status=$?
set -e
[[ $status -ne 0 ]] || FAIL "H: the wrapper root was accepted as an SDK root"
[[ "$output" == *"must point to an SDK root"* ]] || FAIL "H: the wrapper-root message is not actionable"
if command -v timeout >/dev/null 2>&1; then
    set +e
    timeout 20 env DOTNET_INSTALL_DIR="$repo" bash -c 'cd "$1" && ./dotnet --version' _ "$repo" >/dev/null 2>&1
    status=$?
    set -e
    [[ $status -ne 0 && $status -ne 124 ]] \
        || FAIL "H: the wrapper recursed with the repository root as DOTNET_INSTALL_DIR"
fi

# A missing global.json fails fast instead of accepting any SDK.
repo="$FIXTURE/repo-no-pin"
make_repo "$repo" "10.0.402"
rm "$repo/global.json"
set +e
output=$( bash -c 'unset ROOT; cd "$1" && source scripts/dotnet-env.sh && echo REACHED' _ "$repo" 2>&1 )
status=$?
set -e
[[ $status -ne 0 ]] || FAIL "H: a missing global.json did not fail the selection"
[[ "$output" == *"global.json is missing"* ]] || FAIL "H: the missing-pin message is not actionable"
set +e
env HOME="$FIXTURE/home-h" PATH="$curlbin:$PATH" \
    bash "$repo/scripts/install-dotnet.sh" > "$FIXTURE/install.out" 2>&1
status=$?
set -e
[[ $status -ne 0 ]] || FAIL "H: the installer accepted a missing global.json"
grep -q "global.json is missing" "$FIXTURE/install.out" \
    || FAIL "H: the installer error is not actionable"

# --- F: the ./dotnet wrapper ----------------------------------------------

echo "=== F: ./dotnet wrapper ==="
repo="$FIXTURE/repo-wrapper-stale"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.401" 155
set +e
output=$( ( cd "$repo" && ./dotnet --version ) 2>&1 )
status=$?
set -e
[[ $status -ne 0 ]] || FAIL "F: the wrapper exited zero with a stale SDK"
[[ "$output" == *"install-dotnet.sh"* ]] || FAIL "F: the wrapper did not print the fix command"

repo="$FIXTURE/repo-wrapper-ok"
make_repo "$repo" "10.0.402"
write_stub_dotnet "$repo/.dotnet/dotnet" "10.0.402" 0
[[ "$( cd "$repo" && ./dotnet --version )" == "10.0.402" ]] \
    || FAIL "F: the wrapper did not run the satisfying SDK"

repo="$FIXTURE/repo-wrapper-system"
make_repo "$repo" "10.0.402"
[[ "$( env -u DOTNET_ROOT PATH="$sysbin:$PATH" bash -c 'cd "$1" && ./dotnet --version' _ "$repo" )" == "10.0.402" ]] \
    || FAIL "F: the wrapper did not fall back to the system dotnet"

echo "SDK bootstrap regression tests: OK"
