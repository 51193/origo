#!/usr/bin/env bash
# Require a weekly snapshot base to sort above the latest formal release.
#
# A forgotten post-release baseline bump would otherwise publish
# 0.1.1-nightly.* below an existing 0.1.1. Keeping the comparison in a tested
# helper avoids an unpinned shell block inside the workflow.
set -euo pipefail

BASE_VERSION="${1:-}"
LATEST_VERSION="${2:-}"

if [[ -z "$BASE_VERSION" ]]; then
    echo "ERROR: usage: $0 <base-version> [latest-formal-version]" >&2
    exit 2
fi
if ! [[ "$BASE_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "ERROR: base version must be x.y.z; got '$BASE_VERSION'." >&2
    exit 2
fi
if [[ -z "$LATEST_VERSION" ]]; then
    exit 0
fi
if ! [[ "$LATEST_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "ERROR: latest formal version must be x.y.z; got '$LATEST_VERSION'." >&2
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

if ! version_is_newer "$BASE_VERSION" "$LATEST_VERSION"; then
    echo "ERROR: development base $BASE_VERSION is not newer than the latest formal release v$LATEST_VERSION." >&2
    echo "Advance <Version> in Directory.Build.props before publishing a weekly snapshot." >&2
    exit 1
fi
