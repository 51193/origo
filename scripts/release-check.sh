#!/usr/bin/env bash
# Verify release metadata without modifying the caller's checkout.
#
# A staged release commit is created by the Release workflow from the tag, so
# main intentionally carries the next development version. This helper stamps
# a temporary worktree at HEAD with the target version and runs the same strict
# verifier the workflow uses. Commit the release-prep changes first: the
# worktree is created from HEAD, not from uncommitted files.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:-}"

if [[ -z "$VERSION" ]]; then
    echo "ERROR: usage: $0 <x.y.z>" >&2
    exit 2
fi
if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "ERROR: release version must be x.y.z without a suffix; got '$VERSION'." >&2
    exit 2
fi

version_is_newer() {
    local candidate="$1"
    local reference="$2"
    local -a candidate_parts reference_parts
    IFS=. read -r -a candidate_parts <<< "$candidate"
    IFS=. read -r -a reference_parts <<< "$reference"
    local index candidate_part reference_part
    for index in 0 1 2; do
        candidate_part="${candidate_parts[index]:-0}"
        reference_part="${reference_parts[index]:-0}"
        if ((10#$candidate_part > 10#$reference_part)); then
            return 0
        fi
        if ((10#$candidate_part < 10#$reference_part)); then
            return 1
        fi
    done
    return 1
}

if ! HEAD_PROPS="$(git -C "$ROOT" show HEAD:Directory.Build.props 2>/dev/null)"; then
    echo "ERROR: cannot read HEAD:Directory.Build.props." >&2
    exit 1
fi
HEAD_VERSION="$(
    sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' <<< "$HEAD_PROPS" \
        | head -1 \
        | tr -d '[:space:]'
)"
if [[ -z "$HEAD_VERSION" ]]; then
    echo "ERROR: cannot read <Version> from HEAD:Directory.Build.props." >&2
    exit 1
fi
if [[ "$HEAD_VERSION" != *-dev ]]; then
    echo "ERROR: HEAD carries '$HEAD_VERSION'; main must keep a -dev baseline, not a release version." >&2
    exit 1
fi
HEAD_BASE="${HEAD_VERSION%%-*}"
if ! [[ "$HEAD_BASE" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "ERROR: HEAD development baseline '$HEAD_VERSION' has no usable x.y.z form." >&2
    exit 1
fi
if ! version_is_newer "$HEAD_BASE" "$VERSION"; then
    echo "ERROR: HEAD development baseline $HEAD_VERSION is not newer than release $VERSION." >&2
    echo "Advance <Version> in Directory.Build.props before running the release preflight." >&2
    exit 1
fi

EXPECTED_HEAD_ASSEMBLY_VERSION="$HEAD_BASE.0"
for element in AssemblyVersion FileVersion; do
    HEAD_ELEMENT_VERSION="$(
        sed -n "s:.*<$element>\([^<]*\)</$element>.*:\1:p" <<< "$HEAD_PROPS" \
            | head -1 \
            | tr -d '[:space:]'
    )"
    if [[ "$HEAD_ELEMENT_VERSION" != "$EXPECTED_HEAD_ASSEMBLY_VERSION" ]]; then
        echo "ERROR: HEAD development baseline $HEAD_VERSION carries <$element> " >&2
        echo "'$HEAD_ELEMENT_VERSION', but the matching numeric value is '$EXPECTED_HEAD_ASSEMBLY_VERSION'." >&2
        echo "Keep <Version>, <AssemblyVersion>, and <FileVersion> in step before the release preflight." >&2
        exit 1
    fi
done

LATEST_TAG="$(
    git -C "$ROOT" tag --list 'v[0-9]*' --sort=-v:refname         | while IFS= read -r tag; do
            [[ "$tag" == *-* ]] && continue
            printf '%s\n' "$tag"
            break
        done
)"
if [[ -n "$LATEST_TAG" ]]; then
    LATEST_VERSION="${LATEST_TAG#v}"
    if ! version_is_newer "$VERSION" "$LATEST_VERSION"; then
        echo "ERROR: release $VERSION is not newer than the latest formal release $LATEST_VERSION ($LATEST_TAG)." >&2
        exit 1
    fi
fi

TMP_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/origo-release-check.XXXXXX")"
WORKTREE="$TMP_ROOT/worktree"
cleanup() {
    git -C "$ROOT" worktree remove --force "$WORKTREE" >/dev/null 2>&1 || rm -rf "$WORKTREE"
    rmdir "$TMP_ROOT" >/dev/null 2>&1 || true
}
trap cleanup EXIT

git -C "$ROOT" worktree add --detach "$WORKTREE" HEAD >/dev/null

(
    cd "$WORKTREE"
    python3 scripts/write-version-stamp.py "$VERSION"
    TAG_VERSION="$VERSION" bash scripts/verify-release.sh
)

echo "Release preflight passed for $VERSION; the caller checkout was not modified."
