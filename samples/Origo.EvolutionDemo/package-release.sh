#!/usr/bin/env bash
# Build the self-contained playable demo packages for a release.
# Usage: package-release.sh <version> <artifacts-dir>
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
source "$ROOT/scripts/bash/dotnet-env.sh"
PROJECT="$ROOT/samples/Origo.EvolutionDemo"
VERSION="${1:?version required (x.y.z or x.y.z-suffix)}"
ARTIFACTS="${2:?artifacts directory required}"
GODOT="$(bash "$ROOT/scripts/bash/download-godot.sh")"
mkdir -p "$ARTIFACTS"
# Resolve the caller's path before the platform archives are created: the zip
# steps change the working directory to a temporary export directory, so a
# relative artifacts path would resolve there instead.
ARTIFACTS="$(cd "$ARTIFACTS" && pwd)"
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
macos_executable="$(find "${WORK}/macos/OrigoEvolutionDemo.app/Contents/MacOS" \
    -maxdepth 1 -type f -perm -111 -print -quit)"
if [[ -z "$macos_executable" ]]; then
    echo "ERROR: macOS demo export does not contain the app executable." >&2
    exit 1
fi

# Godot exports the .NET assemblies only when the project solution is present
# and the publish succeeds; a package without them cannot start.
for demo_platform in linux windows macos; do
    for demo_assembly in Origo.EvolutionDemo.dll Origo.EvolutionDemo.Logic.dll; do
        if [[ -z "$(find "${WORK}/${demo_platform}" -type f \
            -name "${demo_assembly}" -print -quit)" ]]; then
            echo "ERROR: ${demo_platform} demo package is missing ${demo_assembly}." >&2
            exit 1
        fi
    done
done

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

# Platform-specific archive structure checks. `grep -q` would exit on the
# first match and close the listing pipe while tar/unzip is still writing,
# which `set -o pipefail` turns into a spurious "tar: stdout: write error";
# consume the full listing instead.
archive_contains() {
    local label="$1"
    local pattern="$2"
    shift 2
    if ! "$@" | grep -E "$pattern" >/dev/null; then
        echo "ERROR: ${label} demo archive failed its structure check: ${pattern}" >&2
        exit 1
    fi
}

archive_contains "Linux" '(^|/)OrigoEvolutionDemo\.x86_64$' \
    tar -tzf "${ARTIFACTS}/origo-evolution-demo-${VERSION}-linux-x86_64.tar.gz"
archive_contains "Windows" '(^|/)OrigoEvolutionDemo\.exe$' \
    unzip -Z1 "${ARTIFACTS}/origo-evolution-demo-${VERSION}-windows-x86_64.zip"
archive_contains "macOS" 'OrigoEvolutionDemo\.app/Contents/MacOS/[^/]+$' \
    unzip -Z1 "${ARTIFACTS}/origo-evolution-demo-${VERSION}-macos-universal.zip"

echo "Playable demo packages:"
ls -lh \
    "${ARTIFACTS}/origo-evolution-demo-${VERSION}-linux-x86_64.tar.gz" \
    "${ARTIFACTS}/origo-evolution-demo-${VERSION}-windows-x86_64.zip" \
    "${ARTIFACTS}/origo-evolution-demo-${VERSION}-macos-universal.zip"
