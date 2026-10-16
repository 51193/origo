#!/usr/bin/env bash
# Source-only helpers for scripts/package-consumer-smoke.sh.
#
# These helpers keep the smoke's file checks free of `command | grep -q`
# pipelines. The smoke runs under `set -o pipefail`, where grep's early exit can
# make the producer fail with SIGPIPE even though the requested value was found.

canonicalize_directory() {
    (cd "$1" && pwd -P)
}

find_first_file() {
    local root="$1"
    shift

    find "$root" "$@" -print -quit
}
