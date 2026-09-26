#!/usr/bin/env bash
# Source-only helpers for scripts/package-consumer-smoke.sh.
#
# These helpers keep the smoke's file and archive checks free of
# `command | grep -q` pipelines. The smoke runs under `set -o pipefail`, where
# grep's early exit can make the producer fail with SIGPIPE even though the
# requested value was found.

canonicalize_directory() {
    (cd "$1" && pwd -P)
}

package_contains_zip_entry() {
    local archive="$1"
    local entry="$2"

    # Query the requested entry directly. A pipeline with `grep -q` can make
    # unzip exit 141 through SIGPIPE even when the entry exists.
    unzip -l "$archive" "$entry" >/dev/null 2>&1
}

find_first_file() {
    local root="$1"
    shift

    find "$root" "$@" -print -quit
}
