#!/usr/bin/env bash
# Shell-only package consumer smoke test.
#
# Packs the five Origo shell/kernel packages into a fresh local feed, copies
# the tracked fixtures to isolated temporary directories with no repository
# project references, restores them through package restore only against an
# isolated NuGet package cache, builds with warnings as errors, proves kernel
# compile assets are inaccessible with a negative probe, runs the public
# ConsoleBridge path, and starts the Godot consumer headlessly through the
# public OrigoDefaultEntry path.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

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

source "$ROOT/scripts/dotnet-env.sh"

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

command -v unzip >/dev/null 2>&1 || FAIL "unzip is required to verify package assets."

VERSION=$(dotnet msbuild Origo.GodotAdapter/Origo.GodotAdapter.csproj -getProperty:Version -nologo | tr -d '\r' | tail -1)
[[ -n "$VERSION" ]] || FAIL "could not resolve the repository package version."
SDK_VERSION=$(sed -nE 's#.*Godot\.NET\.Sdk/([0-9]+\.[0-9]+\.[0-9]+).*#\1#p' Origo.GodotAdapter/Origo.GodotAdapter.csproj | head -1)
CONSUMER_SDK_VERSION=$(sed -nE 's#.*Godot\.NET\.Sdk/([0-9]+\.[0-9]+\.[0-9]+).*#\1#p' tools/ShellPackageConsumer/OrigoShellPackageConsumer.csproj | head -1)
[[ "$SDK_VERSION" == "$CONSUMER_SDK_VERSION" ]] || FAIL "Godot.NET.Sdk pin mismatch: adapter=$SDK_VERSION consumer=$CONSUMER_SDK_VERSION"

WORK=$(mktemp -d "${TMPDIR:-/tmp}/origo-package-consumer.XXXXXX")
FEED="$WORK/feed"
CONSUMER_DIR="$WORK/consumer"
PROBE_DIR="$WORK/kernel-probe"
CONSOLE_CONSUMER_DIR="$WORK/console-consumer"
ISOLATED_PACKAGES="$WORK/packages"
cleanup() {
    rm -rf "$WORK"
}
trap cleanup EXIT

mkdir -p "$FEED" "$CONSUMER_DIR" "$PROBE_DIR" "$CONSOLE_CONSUMER_DIR" "$ISOLATED_PACKAGES"

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

for package in Origo.Core.Contracts Origo.Core.Kernel Origo.Core Origo.GodotAdapter Origo.ConsoleBridge; do
    [[ -f "$FEED/$package.$VERSION.nupkg" ]] || FAIL "missing package: $package.$VERSION.nupkg"
done

ANALYZER_ASSET="analyzers/dotnet/cs/Origo.SourceGeneration.dll"
unzip -l "$FEED/Origo.Core.Contracts.$VERSION.nupkg" | grep -Fq "$ANALYZER_ASSET" \
    || FAIL "Origo.Core.Contracts package is missing analyzer asset $ANALYZER_ASSET"

# Exercise the release artifact validator against the same freshly packed
# feed, so normal CI and local runs cover exact identities, shell/kernel
# pairing, dependency direction, analyzer assets, and kernel isolation.
bash scripts/validate-release-packages.sh "$FEED" "$VERSION"

cp -R tools/ShellPackageConsumer/. "$CONSUMER_DIR/"
cp -R tools/ShellPackageConsumer/. "$PROBE_DIR/"
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
cp "$CONSUMER_DIR/nuget.config" "$CONSOLE_CONSUMER_DIR/nuget.config"

grep -q "<ProjectReference" "$CONSUMER_DIR/OrigoShellPackageConsumer.csproj" \
    && FAIL "consumer fixture must not contain a ProjectReference."
grep -q "<ProjectReference" "$CONSOLE_CONSUMER_DIR/OrigoConsoleBridgePackageConsumer.csproj" \
    && FAIL "ConsoleBridge consumer fixture must not contain a ProjectReference."

# The local feed must be the only source of Origo packages. An ambient global
# package cache can contain the same package id/version from an earlier build,
# which would silently satisfy restore and make this smoke validate stale
# artifacts (or fail when the stale entry misses newer assets). Restore and
# build the consumer from an isolated package cache owned by this run.
export NUGET_PACKAGES="$ISOLATED_PACKAGES"

dotnet restore "$CONSUMER_DIR/OrigoShellPackageConsumer.csproj" \
    -p:OrigoShellPackageVersion="$VERSION" >/dev/null

ASSETS=$(find "$CONSUMER_DIR" -path "*/obj/project.assets.json" | head -1)
[[ -n "$ASSETS" ]] || FAIL "package restore did not produce project.assets.json."
grep -Fq "$NUGET_PACKAGES" "$ASSETS" \
    || FAIL "package restore did not use the isolated NuGet package cache."
for package in Origo.Core Origo.GodotAdapter Origo.Core.Contracts Origo.Core.Kernel; do
    grep -q "\"$package/" "$ASSETS" || FAIL "package restore did not resolve $package."
done

dotnet build "$CONSUMER_DIR/OrigoShellPackageConsumer.csproj" \
    --no-restore -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" >/dev/null

find "$CONSUMER_DIR/.godot" -type f -name "Origo.Core.Kernel.dll" | grep -q . \
    || FAIL "kernel runtime assembly was not provided by package restore."

dotnet restore "$CONSOLE_CONSUMER_DIR/OrigoConsoleBridgePackageConsumer.csproj" \
    -p:OrigoShellPackageVersion="$VERSION" >/dev/null

CONSOLE_ASSETS=$(find "$CONSOLE_CONSUMER_DIR" -path "*/obj/project.assets.json" | head -1)
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

dotnet build "$CONSOLE_CONSUMER_DIR/OrigoConsoleBridgePackageConsumer.csproj" \
    --no-restore --configuration Release -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" >/dev/null
if find "$CONSOLE_CONSUMER_DIR" -type f \( -name "Origo.Core.dll" -o -name "Origo.Core.Kernel.dll" \) | grep -q .; then
    FAIL "ConsoleBridge consumer build unexpectedly received Core shell or Kernel runtime assemblies."
fi
set +e
CONSOLE_STARTUP_OUTPUT=$(dotnet run \
    --project "$CONSOLE_CONSUMER_DIR/OrigoConsoleBridgePackageConsumer.csproj" \
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
CONSOLE_PROBE_OUTPUT=$(dotnet build "$CONSOLE_CONSUMER_DIR/OrigoConsoleBridgePackageConsumer.csproj" \
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
PROBE_OUTPUT=$(dotnet build "$PROBE_DIR/OrigoShellPackageConsumer.csproj" \
    -warnaserror \
    -p:OrigoShellPackageVersion="$VERSION" 2>&1)
PROBE_EXIT=$?
set -e
if [[ $PROBE_EXIT -eq 0 ]]; then
    FAIL "negative compile probe unexpectedly succeeded; kernel compile assets leaked."
fi
echo "$PROBE_OUTPUT" | grep -q "CS0246" \
    || { echo "$PROBE_OUTPUT" | tail -20; FAIL "negative probe failed without the expected CS0246 diagnostic."; }

GODOT_BIN=$(bash scripts/download-godot.sh)
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
