#!/usr/bin/env bash
# Regression tests for scripts/write-version-stamp.py.
#
# The release and weekly workflows share this helper, so its version parsing,
# numeric rewrite, and fail-fast behavior are pinned here.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/origo-version-stamp.XXXXXX")"
cleanup() {
    rm -rf "$FIXTURE"
}
trap cleanup EXIT

FAIL() {
    echo "ERROR: $*" >&2
    exit 1
}

write_props() {
    cat > "$FIXTURE/Directory.Build.props" <<PROPS_EOF
<Project>
    <PropertyGroup>
        <Version>0.0.1</Version>
        <AssemblyVersion>0.0.1.0</AssemblyVersion>
        <FileVersion>0.0.1.0</FileVersion>
    </PropertyGroup>
</Project>
PROPS_EOF
}

cd "$FIXTURE"

write_props
python3 "$ROOT/scripts/write-version-stamp.py" "0.1.0" >/dev/null || FAIL "stable version failed"
grep -q "<Version>0.1.0</Version>" Directory.Build.props || FAIL "stable <Version>"
grep -q "<AssemblyVersion>0.1.0.0</AssemblyVersion>" Directory.Build.props || FAIL "stable <AssemblyVersion>"
grep -q "<FileVersion>0.1.0.0</FileVersion>" Directory.Build.props || FAIL "stable <FileVersion>"

# Idempotent when the stamps already match.
python3 "$ROOT/scripts/write-version-stamp.py" "0.1.0" >/dev/null || FAIL "idempotent rewrite failed"

# A prerelease suffix stays in <Version> but not in the assembly versions.
python3 "$ROOT/scripts/write-version-stamp.py" "0.2.0-nightly.7" >/dev/null || FAIL "prerelease failed"
grep -q "<Version>0.2.0-nightly.7</Version>" Directory.Build.props || FAIL "prerelease <Version>"
grep -q "<AssemblyVersion>0.2.0.0</AssemblyVersion>" Directory.Build.props || FAIL "prerelease <AssemblyVersion>"

# A version without a usable numeric prefix fails before rewriting.
if python3 "$ROOT/scripts/write-version-stamp.py" "not-a-version" 2>/dev/null; then
    FAIL "non-numeric version was accepted"
fi
grep -q "<Version>0.2.0-nightly.7</Version>" Directory.Build.props || FAIL "failed rewrite modified the file"

# The tag shape is exactly x.y.z or x.y.z-suffix; truncated and four-part
# versions used to produce assembly stamps the release verifier rejects.
for invalid in "0.3" "0.3.0.0" "0.3.0+build"; do
    if python3 "$ROOT/scripts/write-version-stamp.py" "$invalid" 2>/dev/null; then
        FAIL "invalid version shape '$invalid' was accepted"
    fi
    grep -q "<Version>0.2.0-nightly.7</Version>" Directory.Build.props \
        || FAIL "invalid shape '$invalid' modified the file"
done

# A missing stamp fails instead of writing a partially updated file.
cat > Directory.Build.props <<'PROPS_EOF'
<Project>
    <PropertyGroup>
        <Version>0.0.1</Version>
        <AssemblyVersion>0.0.1.0</AssemblyVersion>
    </PropertyGroup>
</Project>
PROPS_EOF
if python3 "$ROOT/scripts/write-version-stamp.py" "0.3.0" 2>/dev/null; then
    FAIL "missing <FileVersion> was accepted"
fi
grep -q "<Version>0.0.1</Version>" Directory.Build.props || FAIL "partial rewrite on missing stamp"

# Duplicate stamps are rejected as ambiguous.
cat > Directory.Build.props <<'PROPS_EOF'
<Project>
    <PropertyGroup>
        <Version>0.0.1</Version>
        <Version>0.0.2</Version>
        <AssemblyVersion>0.0.1.0</AssemblyVersion>
        <FileVersion>0.0.1.0</FileVersion>
    </PropertyGroup>
</Project>
PROPS_EOF
if python3 "$ROOT/scripts/write-version-stamp.py" "0.3.0" 2>/dev/null; then
    FAIL "duplicate <Version> was accepted"
fi

# The helper takes exactly one version argument.
write_props
if python3 "$ROOT/scripts/write-version-stamp.py" 2>"$FIXTURE/argv.err"; then
    FAIL "missing version argument was accepted"
fi
grep -q "expected exactly one version argument" "$FIXTURE/argv.err" || FAIL "missing-argument message"
write_props
if python3 "$ROOT/scripts/write-version-stamp.py" "0.1.0" "0.2.0" 2>"$FIXTURE/argv.err"; then
    FAIL "extra version argument was accepted"
fi
grep -q "expected exactly one version argument" "$FIXTURE/argv.err" || FAIL "extra-argument message"

echo "write-version-stamp: OK"
