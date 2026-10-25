#!/usr/bin/env bash
# Regression tests for scripts/python/lint-pr-body.py.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
TMP="$(mktemp -d "${TMPDIR:-/tmp}/origo-pr-body.XXXXXX")"
cleanup() {
    rm -rf "$TMP"
}
trap cleanup EXIT

write_event() {
    local body="$1"
    local author="${2:-human}"
    local title="${3:-fix: test change}"
    python3 - "$TMP/event.json" "$body" "$author" "$title" <<'PY'
import json
import sys
path, body, author, title = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
with open(path, "w", encoding="utf-8") as handle:
    json.dump({
        "pull_request": {
            "body": body,
            "title": title,
            "number": 91,
            "user": {"login": author},
        }
    }, handle)
PY
}

expect_pass() {
    GITHUB_EVENT_PATH="$TMP/event.json" python3 "$ROOT/scripts/python/lint-pr-body.py" >/dev/null
}

expect_fail() {
    if GITHUB_EVENT_PATH="$TMP/event.json" python3 "$ROOT/scripts/python/lint-pr-body.py" >/dev/null 2>&1; then
        echo "ERROR: $1 was accepted." >&2
        exit 1
    fi
}

write_event "short body"$'\n'"another short line"
expect_pass

write_event "$(python3 -c 'print("x" * 73)')"
expect_fail "overlong PR body line"

write_event "short" "human" "Add an untyped title"
expect_fail "non-conventional PR title"

write_event "short" "human" "fix: $(python3 -c 'print("x" * 70)')"
expect_fail "overlong PR title"

write_event "short" "human" "fix: trailing period."
expect_fail "PR title ending with a period"

# The repository's own PR template must pass, or every new PR starts red.
write_event "$(cat "$ROOT/PULL_REQUEST_TEMPLATE.md")"
expect_pass

write_event "short"$'\n''```'$'\n'"$(python3 -c 'print("x" * 100)')"$'\n''```'
expect_pass

write_event "short"$'\n'"| $(python3 -c 'print("x" * 100)') |"
expect_pass

write_event "$(python3 -c 'print("x" * 100)')" "dependabot[bot]"
expect_pass

echo "PR title/body lint: OK"
