#!/usr/bin/env bash
# Fixture tests for the repository pre-push commit-lint hook.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

mkdir -p "$WORK_DIR/repo/scripts" "$WORK_DIR/repo/.githooks"
cp "$ROOT/scripts/lint-commits.sh" "$WORK_DIR/repo/scripts/"
cp "$ROOT/.githooks/pre-push" "$WORK_DIR/repo/.githooks/"

cd "$WORK_DIR/repo"
git init -q
git config user.name "Hook Test"
git config user.email "hook-test@example.com"

printf 'base\n' > fixture.txt
git add fixture.txt
git commit -q -m "chore: create fixture"
base_sha="$(git rev-parse HEAD)"
git update-ref refs/remotes/origin/main "$base_sha"

printf 'valid\n' >> fixture.txt
git add fixture.txt
git commit -q -m "fix: accept valid local commit"
valid_sha="$(git rev-parse HEAD)"

printf 'refs/heads/topic %s refs/heads/topic %040d\n' "$valid_sha" 0 |
  bash .githooks/pre-push origin test-url >/dev/null

printf 'invalid\n' >> fixture.txt
git add fixture.txt
git commit -q -m "update"
invalid_sha="$(git rev-parse HEAD)"

if printf 'refs/heads/topic %s refs/heads/topic %040d\n' "$invalid_sha" 0 |
  bash .githooks/pre-push origin test-url >/dev/null 2>&1; then
  echo "ERROR: pre-push hook accepted a non-conventional commit." >&2
  exit 1
fi

printf 'refs/tags/v0.0.0 %s refs/tags/v0.0.0 %040d\n' "$invalid_sha" 0 |
  bash .githooks/pre-push origin test-url >/dev/null

echo "Pre-push hook tests: OK"
