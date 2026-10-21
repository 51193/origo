#!/usr/bin/env bash
# Regression tests for scripts/release-check.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-release-check.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

REPO="$FIXTURE/repo"
mkdir -p "$REPO/scripts" "$REPO/Origo.SourceGeneration"
git -C "$FIXTURE" init -q -b main "$REPO"
git -C "$REPO" config user.name "Origo Test"
git -C "$REPO" config user.email "origo-test@example.invalid"
cp "$ROOT/scripts/release-check.sh" "$REPO/scripts/release-check.sh"
cp "$ROOT/scripts/verify-release.sh" "$REPO/scripts/verify-release.sh"
cp "$ROOT/scripts/write-version-stamp.py" "$REPO/scripts/write-version-stamp.py"

cat > "$REPO/Directory.Build.props" <<'PROPS_EOF'
<Project>
    <PropertyGroup>
        <Version>0.1.2-dev</Version>
        <AssemblyVersion>0.1.2.0</AssemblyVersion>
        <FileVersion>0.1.2.0</FileVersion>
    </PropertyGroup>
</Project>
PROPS_EOF
cat > "$REPO/CHANGELOG.md" <<'CHANGELOG_EOF'
# Changelog

## [Unreleased]

## [0.1.1] - 2026-10-21

### Added

- release preflight fixture
CHANGELOG_EOF
printf '## Release 0.1.1\n' > "$REPO/Origo.SourceGeneration/AnalyzerReleases.Shipped.md"
printf '; no unshipped rules\n' > "$REPO/Origo.SourceGeneration/AnalyzerReleases.Unshipped.md"
git -C "$REPO" add -A
git -C "$REPO" commit -qm "release prep"

WORKTREES_BEFORE="$(git -C "$REPO" worktree list | wc -l)"
OUTPUT="$(cd "$REPO" && bash scripts/release-check.sh 0.1.1)"
grep -q "Release preflight passed for 0.1.1" <<<"$OUTPUT" \
    || FAIL "release preflight did not pass:
$OUTPUT"
grep -q "<Version>0.1.2-dev</Version>" "$REPO/Directory.Build.props" \
    || FAIL "release preflight modified the caller checkout."
[[ "$(git -C "$REPO" worktree list | wc -l)" -eq "$WORKTREES_BEFORE" ]] \
    || FAIL "release preflight left a worktree behind."


# The preflight reads the committed HEAD baseline, not a dirty worktree.
python3 - "$REPO/Directory.Build.props" <<'PYEOF'
import sys
from pathlib import Path
path = Path(sys.argv[1])
path.write_text(path.read_text().replace('<Version>0.1.2-dev</Version>', '<Version>0.1.1-dev</Version>'))
PYEOF
DIRTY_OUTPUT="$(cd "$REPO" && bash scripts/release-check.sh 0.1.1)"
grep -q "Release preflight passed for 0.1.1" <<<"$DIRTY_OUTPUT" \
    || FAIL "release preflight read a dirty worktree instead of HEAD:
$DIRTY_OUTPUT"
git -C "$REPO" checkout -- Directory.Build.props

set +e
OUTPUT="$(cd "$REPO" && bash scripts/release-check.sh 0.1.0 2>&1)"
EXIT_CODE=$?
set -e
[[ $EXIT_CODE -eq 1 ]] || FAIL "missing release block expected exit 1, got $EXIT_CODE:
$OUTPUT"
grep -q "CHANGELOG.md has no version block heading '## \[0.1.0\] -'" <<<"$OUTPUT" \
    || FAIL "missing release block did not report the expected failure:
$OUTPUT"
grep -q "<Version>0.1.2-dev</Version>" "$REPO/Directory.Build.props" \
    || FAIL "failed release preflight modified the caller checkout."
[[ "$(git -C "$REPO" worktree list | wc -l)" -eq "$WORKTREES_BEFORE" ]] \
    || FAIL "failed release preflight left a worktree behind."

# A main baseline that is not newer than the target must fail before stamping.
python3 - "$REPO/Directory.Build.props" <<'PYEOF'
import sys
from pathlib import Path
path = Path(sys.argv[1])
text = path.read_text()
text = text.replace('<Version>0.1.2-dev</Version>', '<Version>0.1.1-dev</Version>')
Path(sys.argv[1]).write_text(text)
PYEOF
git -C "$REPO" add -A
git -C "$REPO" commit -qm "stale baseline"
set +e
OUTPUT="$(cd "$REPO" && bash scripts/release-check.sh 0.1.1 2>&1)"
EXIT_CODE=$?
set -e
[[ $EXIT_CODE -eq 1 ]] || FAIL "stale baseline expected exit 1, got $EXIT_CODE:
$OUTPUT"
grep -q "is not newer than release 0.1.1" <<<"$OUTPUT" \
    || FAIL "stale baseline did not report the expected failure:
$OUTPUT"
grep -q "<Version>0.1.1-dev</Version>" "$REPO/Directory.Build.props" \
    || FAIL "stale baseline check modified the caller checkout."

echo "Release preflight helper: OK"
