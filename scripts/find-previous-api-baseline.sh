#!/usr/bin/env bash
# Locate the newest formal release tag reachable from HEAD, excluding the
# commit HEAD is currently on, whose tree contains the tracked shell API
# baseline. On success, writes that baseline to a temporary file and prints
# its path. Prints nothing when no previous baseline is available.
set -euo pipefail

current_commit="$(git rev-parse HEAD)"
previous_tag=""

while IFS= read -r tag; do
    [[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]] || continue

    tag_commit="$(git rev-list -n 1 "$tag" 2>/dev/null || true)"
    [[ -n "$tag_commit" ]] || continue

    # A release workflow checks out the current tag's commit; skip that tag so
    # the comparison uses the previous formal release, not the release itself.
    [[ "$tag_commit" != "$current_commit" ]] || continue

    if git merge-base --is-ancestor "$tag_commit" HEAD; then
        previous_tag="$tag"
        break
    fi
done < <(git tag --list 'v*' --sort=-version:refname)

[[ -n "$previous_tag" ]] || exit 0

baseline_path="tools/ApiInventoryTool/shell-api-baseline.json"
if ! git cat-file -e "${previous_tag}:${baseline_path}" 2>/dev/null; then
    exit 0
fi

output="$(mktemp)"
git show "${previous_tag}:${baseline_path}" > "$output"
printf '%s\n' "$output"
