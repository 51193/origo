#!/usr/bin/env bash
# Locate the newest formal release tag not newer than the current source version,
# excluding the commit HEAD is currently on and an explicit current release
# tag, whose tree contains the tracked shell API baseline. On success, writes
# that baseline to a temporary file and prints its path. Prints nothing when no
# previous baseline is available.
#
# Release tags are selected by version, not only by ancestry. A tag-only release
# commit is a child of the main commit it was cut from, so it is a sibling of the
# next release commit rather than its ancestor. Requiring ancestry alone would
# skip the immediately preceding release and silently compare against an older
# baseline. A non-ancestor tag is therefore accepted only when it is the known
# metadata-only release commit produced by the release workflow.
set -euo pipefail

baseline_path="tools/ApiInventoryTool/shell-api-baseline.json"
current_commit="$(git rev-parse HEAD)"
current_release_tag="${ORIGO_CURRENT_RELEASE_TAG:-}"
current_version=""

if [[ "$current_release_tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    current_version="${current_release_tag#v}"
elif [[ -f Directory.Build.props ]]; then
    # BSD grep (macOS) has no -P, so use a portable extraction. A failure here
    # must not silently disable the version ceiling.
    current_version="$(
        sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' Directory.Build.props 2>/dev/null \
            | head -1 \
            | tr -d '[:space:]' \
            | sed -E 's/^[vV]//; s/[-+].*$//'
    )"
    if [[ -z "$current_version" ]]; then
        echo "ERROR: cannot read <Version> from Directory.Build.props." >&2
        exit 1
    fi
    if ! [[ "$current_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        echo "ERROR: <Version> '$current_version' has no usable numeric form." >&2
        exit 1
    fi
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

is_release_metadata_commit() {
    local commit="$1"
    local parent subject path
    parent="$(git rev-parse "$commit^" 2>/dev/null)" || return 1
    git merge-base --is-ancestor "$parent" HEAD 2>/dev/null || return 1
    subject="$(git log -1 --format=%s "$commit" 2>/dev/null)" || return 1
    [[ "$subject" =~ ^chore\(release\):\ sync\ version\ stamps\ to\ v[0-9]+\.[0-9]+\.[0-9]+$ ]] || return 1
    while IFS= read -r path; do
        [[ "$path" == "Directory.Build.props" ]] || return 1
    done < <(git diff --name-only "$commit^" "$commit")
    return 0
}

previous_tag=""
while IFS= read -r tag; do
    [[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]] || continue

    # A current release tag may already have been stamped; exclude it explicitly.
    [[ "$tag" != "$current_release_tag" ]] || continue

    tag_version="${tag#v}"
    if [[ -n "$current_version" ]] && version_is_newer "$tag_version" "$current_version"; then
        continue
    fi

    tag_commit="$(git rev-list -n 1 "$tag" 2>/dev/null || true)"
    [[ -n "$tag_commit" ]] || continue

    # A release workflow normally checks out the current tag's commit; skip
    # that tag so the comparison does not compare the release with itself.
    [[ "$tag_commit" != "$current_commit" ]] || continue

    if ! git merge-base --is-ancestor "$tag_commit" HEAD 2>/dev/null; then
        is_release_metadata_commit "$tag_commit" || continue
    fi

    git cat-file -e "${tag}:${baseline_path}" 2>/dev/null || continue
    previous_tag="$tag"
    break
done < <(git tag --list 'v*' --sort=-version:refname)

[[ -n "$previous_tag" ]] || exit 0

output="$(mktemp)"
git show "${previous_tag}:${baseline_path}" > "$output"
printf '%s\n' "$output"
