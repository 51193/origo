#!/usr/bin/env bash
# Locate the newest formal release tag not newer than the current source version,
# excluding the commit HEAD is currently on and an explicit current release
# tag, whose tree contains the tracked shell API baseline. On success, writes
# that baseline to a temporary file and prints its path.
#
# Resolution is fail-closed: when formal release tags exist but no usable
# previous baseline can be resolved (a baseline-bearing tag that is neither an
# ancestor nor the recognized metadata-only release commit), the helper reports
# an error and exits non-zero instead of silently disabling the previous-package
# comparison. A formal tag newer than the current source version is skipped by
# the version ceiling (its changes are not a valid comparison baseline); if no
# older usable baseline remains, the helper fails closed. The only silent skip
# is the documented first-release state: no formal release tag contains the
# tracked baseline because those releases predate the baseline tool (or no
# formal tag exists at all).
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
newer_formal_tags=()
eligible_formal_tag_seen=0
baseline_tag_unresolvable=0
while IFS= read -r tag; do
    [[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]] || continue

    # A current release tag may already have been stamped; exclude it explicitly.
    [[ "$tag" != "$current_release_tag" ]] || continue

    tag_version="${tag#v}"
    if [[ -n "$current_version" ]] && version_is_newer "$tag_version" "$current_version"; then
        # A formal release newer than the source version means the development
        # baseline was not advanced. It cannot be the comparison baseline, so
        # skip it; the tag is recorded so the helper fails closed below when no
        # older usable baseline remains.
        newer_formal_tags+=("$tag")
        continue
    fi

    eligible_formal_tag_seen=1

    tag_commit="$(git rev-list -n 1 "$tag" 2>/dev/null || true)"
    if [[ -z "$tag_commit" ]]; then
        baseline_tag_unresolvable=1
        continue
    fi

    # A release workflow normally checks out the current tag's commit; skip
    # that tag so the comparison does not compare the release with itself.
    [[ "$tag_commit" != "$current_commit" ]] || continue

    if ! git merge-base --is-ancestor "$tag_commit" HEAD 2>/dev/null; then
        if ! is_release_metadata_commit "$tag_commit"; then
            if git cat-file -e "${tag}:${baseline_path}" 2>/dev/null; then
                baseline_tag_unresolvable=1
            fi
            continue
        fi
    fi

    git cat-file -e "${tag}:${baseline_path}" 2>/dev/null || continue
    previous_tag="$tag"
    break
done < <(git tag --list 'v*' --sort=-version:refname)

if [[ -n "$previous_tag" ]]; then
    output="$(mktemp)"
    git show "${previous_tag}:${baseline_path}" > "$output"
    printf '%s\n' "$output"
    exit 0
fi

if [[ "${#newer_formal_tags[@]}" -gt 0 ]]; then
    echo "ERROR: formal release tag(s) newer than the current source version: ${newer_formal_tags[*]}." >&2
    echo "Advance <Version> in Directory.Build.props past the latest formal release before comparing." >&2
    exit 1
fi

if [[ "$baseline_tag_unresolvable" -eq 1 ]]; then
    echo "ERROR: a formal release tag carries the tracked shell API baseline but is neither an ancestor" >&2
    echo "of HEAD nor a recognized metadata-only release commit; refusing to skip the previous-package comparison." >&2
    exit 1
fi

if [[ "$eligible_formal_tag_seen" -eq 1 ]]; then
    # Documented first-release exception: every eligible formal release predates
    # the baseline tool, so there is nothing to compare against. Stay silent;
    # the caller prints the explicit skip notice.
    exit 0
fi

# No formal release tag exists at all (for example a shallow local checkout):
# there is no previous-package contract to compare against.
exit 0
