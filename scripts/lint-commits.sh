#!/usr/bin/env bash
# Conventional Commits + 72-character subject gate for pull requests.
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

SUBJECTS="$(mktemp)"
git log --no-merges --pretty=format:'%s' "$BASE_SHA..$HEAD_SHA" > "$SUBJECTS"

FAILED=0
while IFS= read -r subject; do
  if [[ -z "$subject" ]]; then
    echo "FAIL empty subject" >&2
    FAILED=1
    continue
  fi

  if [[ ${#subject} -gt 72 ]]; then
    echo "FAIL subject longer than 72 chars: $subject" >&2
    FAILED=1
    continue
  fi

  if [[ "$subject" == *. ]]; then
    echo "FAIL subject must not end with a period: $subject" >&2
    FAILED=1
    continue
  fi

  if ! grep -Eq '^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([^)]*\))?!?: .+' <<<"$subject"; then
    echo "FAIL non-conventional subject: $subject" >&2
    FAILED=1
  fi
done < "$SUBJECTS"

rm -f "$SUBJECTS"

if [[ $FAILED -ne 0 ]]; then
  echo ""
  echo "Commit messages must follow docs/META: Conventional Commits," >&2
  echo "English imperative subject, <= 72 characters, no trailing period." >&2
  exit 1
fi

echo "Commit lint: OK"
