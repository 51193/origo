#!/usr/bin/env bash
# Shell-only package consumer smoke test.
#
# Packs the five Origo shell/kernel packages into a fresh local feed, copies
# the tracked fixtures to isolated temporary directories with no repository
# project references, restores them through package restore only against an
# isolated NuGet package cache, builds with warnings as errors, proves kernel
# compile assets are inaccessible with negative probes, runs the Core-only
# OrigoHost and public ConsoleBridge paths, and starts the Godot consumer
# headlessly through the public OrigoDefaultEntry path.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

# Keep the caller's NuGet package cache when HOME is redirected for Godot.
HOST_NUGET_PACKAGES="${NUGET_PACKAGES:-${HOME:-}/.nuget/packages}"

# Godot writes logs/caches under the XDG user directories. In containers and
# restricted sandboxes those directories are often read-only; redirect them to
# a repository-local (git-ignored) home before any dotnet or Godot process runs.
GODOT_DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}"
GODOT_CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}"
GODOT_CACHE_DIR="${XDG_CACHE_HOME:-$HOME/.cache}"
if [[ ! -w "$GODOT_DATA_DIR" || ! -w "$GODOT_CONFIG_DIR" || ! -w "$GODOT_CACHE_DIR" ]]; then
    mkdir -p "$ROOT/.godot-home/.local/share" "$ROOT/.godot-home/.config" "$ROOT/.godot-home/.cache"
    export HOME="$ROOT/.godot-home"
    export XDG_DATA_HOME="$ROOT/.godot-home/.local/share"
    export XDG_CONFIG_HOME="$ROOT/.godot-home/.config"
    export XDG_CACHE_HOME="$ROOT/.godot-home/.cache"
fi

if [[ -z "${NUGET_PACKAGES:-}" && -d "$HOST_NUGET_PACKAGES" ]]; then
    export NUGET_PACKAGES="$HOST_NUGET_PACKAGES"
fi

source "$ROOT/scripts/bash/dotnet-env.sh"
source "$ROOT/scripts/bash/package-consumer-smoke-lib.sh"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

VERSION=$(dotnet msbuild Origo.GodotAdapter/Origo.GodotAdapter.csproj -getProperty:Version -nologo | tr -d '\r' | tail -1)
[[ -n "$VERSION" ]] || FAIL "could not resolve the repository package version."
# Pin parsing lives in download-godot.sh so the adapter authority, the
# integration-test pin, and this consumer pin cannot be parsed by divergent
# rules; the mismatch assertion below keeps its own message.
SDK_VERSION=$(bash scripts/bash/download-godot.sh --print-version)
CONSUMER_SDK_VERSION=$(bash scripts/bash/download-godot.sh --print-version "$ROOT/tools/ShellPackageConsumer/OrigoShellPackageConsumer.csproj")
[[ "$SDK_VERSION" == "$CONSUMER_SDK_VERSION" ]] || FAIL "Godot.NET.Sdk pin mismatch: adapter=$SDK_VERSION consumer=$CONSUMER_SDK_VERSION"

WORK=$(mktemp -d "${TMPDIR:-/tmp}/origo-package-consumer.XXXXXX")
FEED="$WORK/feed"
CONSUMER_DIR="$WORK/consumer"
PROBE_DIR="$WORK/kernel-probe"
CORE_CONSUMER_DIR="$WORK/core-consumer"
CONSOLE_CONSUMER_DIR="$WORK/console-consumer"
ISOLATED_PACKAGES="$WORK/packages"
cleanup() {
    rm -rf "$WORK"
}
trap cleanup EXIT

mkdir -p "$FEED" "$CONSUMER_DIR" "$PROBE_DIR" "$CORE_CONSUMER_DIR" "$CONSOLE_CONSUMER_DIR" "$ISOLATED_PACKAGES"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " Shell-only package consumer smoke"
echo " package version: $VERSION"
echo " Godot.NET.Sdk:   $SDK_VERSION"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

dotnet pack Origo.Core.Contracts/Origo.Core.Contracts.csproj --configuration Release --output "$FEED" >/dev/null
dotnet pack Origo.Core.Kernel/Origo.Core.Kernel.csproj --configuration Release --output "$FEED" >/dev/null
dotnet pack Origo.Core/Origo.Core.csproj --configuration Release --output "$FEED" >/dev/null
dotnet pack Origo.GodotAdapter/Origo.GodotAdapter.csproj --configuration Release --output "$FEED" >/dev/null
dotnet pack Origo.ConsoleBridge/Origo.ConsoleBridge.csproj --configuration Release --output "$FEED" >/dev/null

# Exercise the release artifact validator against the same freshly packed
# feed, so normal CI and local runs cover exact identities, shell/kernel
# pairing, dependency direction, analyzer assets, and kernel isolation. The
# validator is the only place the required five-package set and the analyzer
# asset are asserted; the smoke does not repeat them, so those checks cannot
# drift between the two scripts.
bash scripts/bash/validate-release-packages.sh "$FEED" "$VERSION"

cp -R tools/ShellPackageConsumer/. "$CONSUMER_DIR/"
cp -R tools/ShellPackageConsumer/. "$PROBE_DIR/"
cp -R tools/CorePackageConsumer/. "$CORE_CONSUMER_DIR/"
cp -R tools/ConsoleBridgePackageConsumer/. "$CONSOLE_CONSUMER_DIR/"

cat > "$CONSUMER_DIR/nuget.config" <<NUGET_CONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-feed" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
NUGET_CONFIG
cp "$CONSUMER_DIR/nuget.config" "$PROBE_DIR/nuget.config"
cp "$CONSUMER_DIR/nuget.config" "$CORE_CONSUMER_DIR/nuget.config"
cp "$CONSUMER_DIR/nuget.config" "$CONSOLE_CONSUMER_DIR/nuget.config"

grep -q "<ProjectReference" "$CONSUMER_DIR/OrigoShellPackageConsumer.csproj" \
    && FAIL "consumer fixture must not contain a ProjectReference."
grep -q "<ProjectReference" "$CORE_CONSUMER_DIR/OrigoCorePackageConsumer.csproj" \
    && FAIL "Core-only consumer fixture must not contain a ProjectReference."
grep -q "<ProjectReference" "$CONSOLE_CONSUMER_DIR/OrigoConsoleBridgePackageConsumer.csproj" \
    && FAIL "ConsoleBridge consumer fixture must not contain a ProjectReference."

# The local feed must be the only source of Origo packages. An ambient global
# package cache can contain the same package id/version from an earlier build,
# which would silently satisfy restore and make this smoke validate stale
# artifacts (or fail when the stale entry misses newer assets). Restore and
# build the consumer from an isolated package cache owned by this run. NuGet
# records the physical path in project.assets.json on macOS, where TMPDIR is
# reached through the /var symlink, so resolve the cache path before exporting.
ISOLATED_PACKAGES=$(canonicalize_directory "$ISOLATED_PACKAGES") \
    || FAIL "could not resolve the isolated NuGet package cache path."
export NUGET_PACKAGES="$ISOLATED_PACKAGES"

(cd "$CONSUMER_DIR" && dotnet restore OrigoShellPackageConsumer.csproj \
    -p:OrigoShellPackageVersion="$VERSION" >/dev/null)

ASSETS=$(find_first_file "$CONSUMER_DIR" -path "*/obj/project.assets.json")
[[ -n "$ASSETS" ]] || FAIL "package restore did not produce project.assets.json."
grep -Fq "$NUGET_PACKAGES" "$ASSETS" \
    || FAIL "package restore did not use the isolated NuGet package cache."
for package in Origo.Core Origo.GodotAdapter Origo.Core.Contracts Origo.Core.Kernel; do
    grep -q "\"$package/" "$ASSETS" || FAIL "package restore did not resolve $package."
done

(cd "$CONSUMER_DIR" && dotnet build OrigoShellPackageConsumer.csproj \
    --no-restore -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" >/dev/null)

[[ -n "$(find_first_file "$CONSUMER_DIR/.godot" -type f -name "Origo.Core.Kernel.dll")" ]] \
    || FAIL "kernel runtime assembly was not provided by package restore."

(cd "$CONSOLE_CONSUMER_DIR" && dotnet restore OrigoConsoleBridgePackageConsumer.csproj \
    -p:OrigoShellPackageVersion="$VERSION" >/dev/null)

CONSOLE_ASSETS=$(find_first_file "$CONSOLE_CONSUMER_DIR" -path "*/obj/project.assets.json")
[[ -n "$CONSOLE_ASSETS" ]] || FAIL "ConsoleBridge consumer restore did not produce project.assets.json."
grep -Fq "$NUGET_PACKAGES" "$CONSOLE_ASSETS" \
    || FAIL "ConsoleBridge consumer restore did not use the isolated NuGet package cache."
for package in Origo.ConsoleBridge Origo.Core.Contracts; do
    grep -Fq "\"$package/" "$CONSOLE_ASSETS" \
        || FAIL "ConsoleBridge consumer restore did not resolve $package."
done
if grep -Fq '"Origo.Core/' "$CONSOLE_ASSETS"; then
    FAIL "ConsoleBridge consumer unexpectedly resolved Origo.Core."
fi
if grep -Fq '"Origo.Core.Kernel/' "$CONSOLE_ASSETS"; then
    FAIL "ConsoleBridge consumer unexpectedly resolved Origo.Core.Kernel."
fi

(cd "$CONSOLE_CONSUMER_DIR" && dotnet build OrigoConsoleBridgePackageConsumer.csproj \
    --no-restore --configuration Release -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" >/dev/null)
if [[ -n "$(find_first_file "$CONSOLE_CONSUMER_DIR" -type f \( -name "Origo.Core.dll" -o -name "Origo.Core.Kernel.dll" \))" ]]; then
    FAIL "ConsoleBridge consumer build unexpectedly received Core shell or Kernel runtime assemblies."
fi
set +e
CONSOLE_STARTUP_OUTPUT=$(cd "$CONSOLE_CONSUMER_DIR" && dotnet run \
    --project OrigoConsoleBridgePackageConsumer.csproj \
    --configuration Release --no-build --no-restore 2>&1)
CONSOLE_EXIT=$?
set -e
if [[ $CONSOLE_EXIT -ne 0 ]]; then
    echo "$CONSOLE_STARTUP_OUTPUT" | tail -30
    FAIL "ConsoleBridge consumer exited with $CONSOLE_EXIT."
fi
if ! grep -q "CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK" <<<"$CONSOLE_STARTUP_OUTPUT"; then
    echo "$CONSOLE_STARTUP_OUTPUT" | tail -30
    FAIL "ConsoleBridge consumer startup did not print CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK."
fi
echo "$CONSOLE_STARTUP_OUTPUT" | grep "CONSOLE_BRIDGE_PACKAGE_CONSUMER_OK"

cp "$CONSOLE_CONSUMER_DIR/KernelLeakProbe.cs.template" "$CONSOLE_CONSUMER_DIR/KernelLeakProbe.cs"
set +e
CONSOLE_PROBE_OUTPUT=$(cd "$CONSOLE_CONSUMER_DIR" && dotnet build OrigoConsoleBridgePackageConsumer.csproj \
    --no-restore --configuration Release -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" 2>&1)
CONSOLE_PROBE_EXIT=$?
set -e
if [[ $CONSOLE_PROBE_EXIT -eq 0 ]]; then
    FAIL "ConsoleBridge kernel leak probe unexpectedly succeeded; kernel compile assets leaked through the shell package."
fi
if ! grep -q "CS0246" <<<"$CONSOLE_PROBE_OUTPUT"; then
    echo "$CONSOLE_PROBE_OUTPUT" | tail -20
    FAIL "ConsoleBridge kernel leak probe failed without the expected CS0246 diagnostic."
fi

cp "$PROBE_DIR/KernelLeakProbe.cs.template" "$PROBE_DIR/KernelLeakProbe.cs"
set +e
PROBE_OUTPUT=$(cd "$PROBE_DIR" && dotnet build OrigoShellPackageConsumer.csproj \
    -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" 2>&1)
PROBE_EXIT=$?
set -e
if [[ $PROBE_EXIT -eq 0 ]]; then
    FAIL "negative compile probe unexpectedly succeeded; kernel compile assets leaked."
fi
grep -q "CS0246" <<<"$PROBE_OUTPUT" \
    || { tail -20 <<<"$PROBE_OUTPUT"; FAIL "negative probe failed without the expected CS0246 diagnostic."; }

# Core-only fixture: references only the Origo.Core package and starts the
# shell host in a console process. It must not pull adapter, ConsoleBridge, or
# Godot assets into the restore graph, and it must not require a Godot engine.
set +e
CORE_RESTORE_OUTPUT=$(cd "$CORE_CONSUMER_DIR" && dotnet restore OrigoCorePackageConsumer.csproj \
    -p:OrigoShellPackageVersion="$VERSION" 2>&1)
CORE_RESTORE_EXIT=$?
set -e
if [[ $CORE_RESTORE_EXIT -ne 0 ]]; then
    echo "$CORE_RESTORE_OUTPUT" | tail -30
    FAIL "Core-only consumer restore failed with $CORE_RESTORE_EXIT."
fi

CORE_ASSETS=$(find_first_file "$CORE_CONSUMER_DIR" -path "*/obj/project.assets.json")
[[ -n "$CORE_ASSETS" ]] || FAIL "Core-only consumer restore did not produce project.assets.json."
grep -Fq "$NUGET_PACKAGES" "$CORE_ASSETS" \
    || FAIL "Core-only consumer restore did not use the isolated NuGet package cache."
for package in Origo.Core Origo.Core.Contracts Origo.Core.Kernel; do
    grep -Fq "\"$package/" "$CORE_ASSETS" \
        || FAIL "Core-only consumer restore did not resolve $package."
done
for package in Origo.GodotAdapter Origo.ConsoleBridge GodotSharp; do
    if grep -Fq "\"$package/" "$CORE_ASSETS"; then
        FAIL "Core-only consumer unexpectedly resolved $package."
    fi
done

set +e
CORE_BUILD_OUTPUT=$(cd "$CORE_CONSUMER_DIR" && dotnet build OrigoCorePackageConsumer.csproj \
    --no-restore --configuration Release -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" 2>&1)
CORE_BUILD_EXIT=$?
set -e
if [[ $CORE_BUILD_EXIT -ne 0 ]]; then
    echo "$CORE_BUILD_OUTPUT" | tail -40
    FAIL "Core-only consumer build failed with $CORE_BUILD_EXIT."
fi

set +e
CORE_STARTUP_OUTPUT=$(cd "$CORE_CONSUMER_DIR" && dotnet run \
    --project OrigoCorePackageConsumer.csproj \
    --configuration Release --no-build --no-restore 2>&1)
CORE_EXIT=$?
set -e
if [[ $CORE_EXIT -ne 0 ]]; then
    echo "$CORE_STARTUP_OUTPUT" | tail -30
    FAIL "Core-only consumer exited with $CORE_EXIT."
fi
if ! grep -q "CORE_CONSUMER_STARTUP_OK" <<<"$CORE_STARTUP_OUTPUT"; then
    echo "$CORE_STARTUP_OUTPUT" | tail -30
    FAIL "Core-only consumer did not print CORE_CONSUMER_STARTUP_OK."
fi
echo "$CORE_STARTUP_OUTPUT" | grep "CORE_CONSUMER_STARTUP_OK"

cp "$CORE_CONSUMER_DIR/KernelLeakProbe.cs.template" "$CORE_CONSUMER_DIR/KernelLeakProbe.cs"
set +e
CORE_PROBE_OUTPUT=$(cd "$CORE_CONSUMER_DIR" && dotnet build OrigoCorePackageConsumer.csproj \
    --no-restore --configuration Release -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" 2>&1)
CORE_PROBE_EXIT=$?
set -e
if [[ $CORE_PROBE_EXIT -eq 0 ]]; then
    FAIL "Core-only kernel leak probe unexpectedly succeeded; kernel compile assets leaked through the Origo.Core package."
fi
if ! grep -q "CS0246" <<<"$CORE_PROBE_OUTPUT"; then
    echo "$CORE_PROBE_OUTPUT" | tail -20
    FAIL "Core-only kernel leak probe failed without the expected CS0246 diagnostic."
fi

GODOT_BIN=$(bash scripts/bash/download-godot.sh)
echo "Using Godot binary: $GODOT_BIN"

# Import resources once so the first real run is not affected by Godot's
# resource-import side effects.
"$GODOT_BIN" --headless --path "$CONSUMER_DIR" --import >/dev/null 2>&1 || true

set +e
STARTUP_OUTPUT=$("$GODOT_BIN" --headless --path "$CONSUMER_DIR" --quit-after 10 2>&1)
STARTUP_EXIT=$?
set -e

if [[ $STARTUP_EXIT -ne 0 ]]; then
    echo "$STARTUP_OUTPUT" | tail -30
    FAIL "consumer startup exited with $STARTUP_EXIT."
fi
if ! grep -q "SHELL_CONSUMER_STARTUP_OK" <<<"$STARTUP_OUTPUT"; then
    echo "$STARTUP_OUTPUT" | tail -30
    FAIL "consumer startup did not print SHELL_CONSUMER_STARTUP_OK."
fi
if grep -q "SHELL_CONSUMER_STARTUP_FAILED" <<<"$STARTUP_OUTPUT"; then
    echo "$STARTUP_OUTPUT" | tail -30
    FAIL "consumer startup reported SHELL_CONSUMER_STARTUP_FAILED."
fi

echo "$STARTUP_OUTPUT" | grep "SHELL_CONSUMER_STARTUP_OK"
echo ""
echo "Package consumer smoke: OK"
