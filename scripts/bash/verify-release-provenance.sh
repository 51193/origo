#!/usr/bin/env bash
# Verify that a release tag originates from reviewed main history.
#
# A formal tag must point at a main commit, or at the metadata-only release
# commit this workflow creates on a rerun after it has moved the tag. A
# snapshot tag must point at the weekly version-stamp commit, whose parent is a
# main commit. This keeps tag creation from bypassing the pull-request-only
# main branch while still allowing weekly snapshots that are not on main.
set -euo pipefail

TAG="${1:-}"
COMMIT="${2:-}"

if [[ -z "$TAG" || -z "$COMMIT" ]]; then
    echo "ERROR: usage: $0 <v-tag> <tag-commit-sha>" >&2
    exit 2
fi
if [[ "$TAG" != v* ]]; then
    echo "ERROR: release tag '$TAG' must start with 'v'." >&2
    exit 2
fi

VERSION="${TAG#v}"
git fetch --force origin "refs/heads/main:refs/remotes/origin/main"
MAIN_REF="refs/remotes/origin/main"

props_are_version_only() {
    local commit="$1"
    python3 - "$commit" <<'PY'
import re
import subprocess
import sys

commit = sys.argv[1]

def show(rev):
    return subprocess.check_output(["git", "show", rev], text=True)

parent = show(f"{commit}^:Directory.Build.props")
current = show(f"{commit}:Directory.Build.props")

def normalize(text):
    for name in ("Version", "AssemblyVersion", "FileVersion"):
        text = re.sub(rf"<{name}>[^<]*</{name}>", f"<{name}>__ORIGO_VALUE__</{name}>", text)
    return text

if normalize(parent) != normalize(current):
    print("props content differs beyond the three version elements", file=sys.stderr)
    sys.exit(1)
PY
}

is_metadata_commit() {
    local commit="$1"
    local subject="$2"
    local parent path
    parent="$(git rev-parse "$commit^" 2>/dev/null)" || return 1
    git merge-base --is-ancestor "$parent" "$MAIN_REF" 2>/dev/null || return 1
    [[ "$(git log -1 --format=%s "$commit" 2>/dev/null)" =~ $subject ]] || return 1
    while IFS= read -r path; do
        [[ "$path" == "Directory.Build.props" ]] || return 1
    done < <(git diff --name-only "$commit^" "$commit")
    props_are_version_only "$commit" || return 1
    return 0
}

if [[ "$VERSION" == *-* ]]; then
    if git merge-base --is-ancestor "$COMMIT" "$MAIN_REF" 2>/dev/null; then
        echo "Snapshot tag $TAG points at main history."
        exit 0
    fi
    SNAPSHOT_SUBJECT="$(git log -1 --format=%s "$COMMIT" 2>/dev/null || true)"
    if ! is_metadata_commit "$COMMIT" '^chore: bump version to .* for weekly build$' &&
       ! is_metadata_commit "$COMMIT" '^chore\(release\): sync version stamps to v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$'; then
        echo "ERROR: snapshot tag $TAG does not point at main history or a weekly/release version-stamp commit based on main." >&2
        exit 1
    fi
    if [[ "$SNAPSHOT_SUBJECT" != "chore: bump version to ${VERSION} for weekly build" &&
          "$SNAPSHOT_SUBJECT" != "chore(release): sync version stamps to v${VERSION}" ]]; then
        echo "ERROR: snapshot tag $TAG points at a version-stamp commit for a different version (${SNAPSHOT_SUBJECT})." >&2
        exit 1
    fi
    echo "Snapshot tag $TAG is based on a version-stamp commit on main."
    exit 0
fi

if git merge-base --is-ancestor "$COMMIT" "$MAIN_REF" 2>/dev/null; then
    echo "Formal tag $TAG points at main history."
    exit 0
fi

if ! is_metadata_commit "$COMMIT" '^chore\(release\): sync version stamps to v[0-9]+\.[0-9]+\.[0-9]+$'; then
    echo "ERROR: formal tag $TAG does not point at main history or the metadata-only release commit." >&2
    exit 1
fi
FORMAL_SUBJECT="$(git log -1 --format=%s "$COMMIT")"
if [[ "$FORMAL_SUBJECT" != "chore(release): sync version stamps to v${VERSION}" ]]; then
    echo "ERROR: formal tag $TAG points at a version-stamp commit for a different version (${FORMAL_SUBJECT})." >&2
    exit 1
fi
echo "Formal tag $TAG points at the metadata-only release commit based on main."
