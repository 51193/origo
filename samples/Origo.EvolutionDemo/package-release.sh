#!/usr/bin/env bash
# Build the self-contained playable demo packages for a release.
# Usage: package-release.sh <version> <artifacts-dir>
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="$ROOT/samples/Origo.EvolutionDemo"
VERSION="${1:?version required (x.y.z or x.y.z-suffix)}"
ARTIFACTS="${2:?artifacts directory required}"
GODOT="$(bash "$ROOT/scripts/bash/download-godot.sh")"
mkdir -p "$ARTIFACTS"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir -p "${WORK}/linux" "${WORK}/windows" "${WORK}/macos"

dotnet build "$PROJECT/Origo.EvolutionDemo.csproj" --configuration Release --nologo -v minimal

# Import resources once; a fresh CI checkout has no .godot import cache.
"$GODOT" --headless --path "$PROJECT" --editor --quit

export_platform() {
    local preset="$1"
    local output="$2"
    mkdir -p "$(dirname "$output")"
    "$GODOT" --headless --path "$PROJECT" --export-release "$preset" "$output"
}

export_platform "Linux" "${WORK}/linux/OrigoEvolutionDemo.x86_64"
export_platform "Windows" "${WORK}/windows/OrigoEvolutionDemo.exe"
export_platform "macOS" "${WORK}/macos/OrigoEvolutionDemo.app"

test -x "${WORK}/linux/OrigoEvolutionDemo.x86_64"
test -f "${WORK}/windows/OrigoEvolutionDemo.exe"
test -d "${WORK}/macos/OrigoEvolutionDemo.app/Contents/MacOS"

cat > "${WORK}/README.txt" <<'README'
Origo Evolution Demo

Self-contained playable package. Launch the platform executable:
  Linux:   ./OrigoEvolutionDemo.x86_64
  Windows: OrigoEvolutionDemo.exe
  macOS:   OrigoEvolutionDemo.app

The package includes the Godot runtime and the game's .NET assemblies.
README
mkdir -p "${WORK}/linux" "${WORK}/windows" "${WORK}/macos"
cp "${WORK}/README.txt" "${WORK}/linux/README.txt"
cp "${WORK}/README.txt" "${WORK}/windows/README.txt"
cp "${WORK}/README.txt" "${WORK}/macos/README.txt"

tar -C "${WORK}/linux" -czf \
    "${ARTIFACTS}/origo-evolution-demo-${VERSION}-linux-x86_64.tar.gz" .
( cd "${WORK}/windows" && zip -rq "${ARTIFACTS}/origo-evolution-demo-${VERSION}-windows-x86_64.zip" . )
( cd "${WORK}/macos" && zip -rq "${ARTIFACTS}/origo-evolution-demo-${VERSION}-macos-universal.zip" OrigoEvolutionDemo.app README.txt )

echo "Playable demo packages:"
ls -lh \
    "${ARTIFACTS}/origo-evolution-demo-${VERSION}-linux-x86_64.tar.gz" \
    "${ARTIFACTS}/origo-evolution-demo-${VERSION}-windows-x86_64.zip" \
    "${ARTIFACTS}/origo-evolution-demo-${VERSION}-macos-universal.zip"
