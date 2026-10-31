#!/usr/bin/env bash
# Focused regression tests for scripts/bash/package-consumer-smoke-lib.sh and
# the Core-only package-consumer fixture contract.
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

# The Core-only fixture is deliberately a single-package console project. The
# full smoke verifies this after pack/restore; these text checks fail fast if
# the fixture grows a ProjectReference, a second package, or a Godot SDK
# without a full package run.
CORE_CONSUMER_CSPROJ="$ROOT/tools/CorePackageConsumer/OrigoCorePackageConsumer.csproj"
[[ -f "$CORE_CONSUMER_CSPROJ" ]] \
    || FAIL "Core-only consumer fixture is missing: $CORE_CONSUMER_CSPROJ"
CORE_PACKAGE_REFERENCES=$(grep -Fc "<PackageReference" "$CORE_CONSUMER_CSPROJ" || true)
[[ "$CORE_PACKAGE_REFERENCES" -eq 1 ]] \
    || FAIL "Core-only consumer must contain exactly one PackageReference: found $CORE_PACKAGE_REFERENCES"
grep -Fq 'Include="Origo.Core"' "$CORE_CONSUMER_CSPROJ" \
    || FAIL "Core-only consumer must reference Origo.Core."
grep -Fq "OrigoShellPackageVersion" "$CORE_CONSUMER_CSPROJ" \
    || FAIL "Core-only consumer must use the OrigoShellPackageVersion property."
grep -Fq "<TargetFramework>net10.0</TargetFramework>" "$CORE_CONSUMER_CSPROJ" \
    || FAIL "Core-only consumer must target net10.0."
grep -Fq "<Nullable>enable</Nullable>" "$CORE_CONSUMER_CSPROJ" \
    || FAIL "Core-only consumer must enable nullable reference types."
grep -Fq "<OutputType>Exe</OutputType>" "$CORE_CONSUMER_CSPROJ" \
    || FAIL "Core-only consumer must be a console executable."
if grep -Fq "<ProjectReference" "$CORE_CONSUMER_CSPROJ"; then
    FAIL "Core-only consumer must not contain a ProjectReference."
fi
if grep -Fq "Godot.NET.Sdk" "$CORE_CONSUMER_CSPROJ"; then
    FAIL "Core-only consumer must not use a Godot SDK."
fi

CORE_LEAK_PROBE="$ROOT/tools/CorePackageConsumer/KernelLeakProbe.cs.template"
[[ -f "$CORE_LEAK_PROBE" ]] \
    || FAIL "Core-only kernel leak probe template is missing: $CORE_LEAK_PROBE"
grep -Fq "Origo.Core.Runtime" "$CORE_LEAK_PROBE" \
    || FAIL "Core-only kernel leak probe must reference the kernel namespace."
grep -Fq "OrigoRuntime" "$CORE_LEAK_PROBE" \
    || FAIL "Core-only kernel leak probe must reference OrigoRuntime."

PS_SMOKE="$ROOT/scripts/powershell/package-consumer-smoke.ps1"
[[ -f "$PS_SMOKE" ]] || FAIL "PowerShell package-consumer smoke is missing."
grep -Fq 'tools\CorePackageConsumer' "$PS_SMOKE" \
    || FAIL "PowerShell smoke must include the Core-only consumer fixture."
grep -Fq 'OrigoCorePackageConsumer.csproj' "$PS_SMOKE" \
    || FAIL "PowerShell smoke must restore/build the Core-only consumer."
grep -Fq 'CORE_CONSUMER_STARTUP_OK' "$PS_SMOKE" \
    || FAIL "PowerShell smoke must verify the Core-only consumer marker."
grep -Fq 'Core kernel leak probe' "$PS_SMOKE" \
    || FAIL "PowerShell smoke must run the Core-only kernel leak probe."

echo "Package consumer smoke lib: OK"
