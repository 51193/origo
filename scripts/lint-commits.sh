#!/usr/bin/env bash
# Conventional Commits gate for pull requests: conventional type,
# 72-character subject limit, no trailing period, and body lines no longer
# than 72 characters (docs/META commit message rules).
# Usage:
#   bash scripts/lint-commits.sh <base-sha> <head-sha>
#   bash scripts/lint-commits.sh          # uses origin/main..HEAD locally
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

BASE_SHA="${1:-}"
HEAD_SHA="${2:-HEAD}"

if [[ -z "$BASE_SHA" ]]; then
  BASE_SHA="$(git merge-base origin/main HEAD 2>/dev/null || echo '')"
  if [[ -z "$BASE_SHA" ]]; then
    echo "ERROR: cannot determine base SHA and origin/main is unavailable." >&2
    echo "Usage: bash scripts/lint-commits.sh <base-sha> <head-sha>" >&2
    exit 2
  fi
fi

COMMITS="$(mktemp)"
git rev-list --no-merges "$BASE_SHA..$HEAD_SHA" > "$COMMITS"

FAILED=0
while IFS= read -r sha; do
  subject="$(git log -1 --pretty=format:'%s' "$sha")"

  if [[ -z "$subject" ]]; then
    echo "FAIL empty subject ($sha)" >&2
    FAILED=1
  elif [[ ${#subject} -gt 72 ]]; then
    echo "FAIL subject longer than 72 chars: $subject" >&2
    FAILED=1
  elif [[ "$subject" == *. ]]; then
    echo "FAIL subject must not end with a period: $subject" >&2
    FAILED=1
  elif ! grep -Eq '^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([^)]*\))?!?: .+' <<<"$subject"; then
    echo "FAIL non-conventional subject: $subject" >&2
    FAILED=1
  fi

  body="$(git log -1 --pretty=format:'%b' "$sha")"
  while IFS= read -r line; do
    if [[ ${#line} -gt 72 ]]; then
      echo "FAIL body line longer than 72 chars in $sha ('$subject'): $line" >&2
      FAILED=1
    fi
  done <<< "$body"
done < "$COMMITS"

rm -f "$COMMITS"

if [[ $FAILED -ne 0 ]]; then
  echo ""
  echo "Commit messages must follow docs/META: Conventional Commits," >&2
  echo "English imperative subject, <= 72 characters, no trailing period," >&2
  echo "and body lines <= 72 characters." >&2
  exit 1
fi

echo "Commit lint: OK"
