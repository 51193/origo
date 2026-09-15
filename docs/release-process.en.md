<!-- docsync-pair: release-process -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Release & Changelog Process

> [↑ Back to Origo Manual](README.en.md)

This file is the detailed authority for Origo **version snapshots, formal releases,
and Changelog rules**. [AGENTS.md](../AGENTS.md) remains the mandatory gate and
routing document; if a hard gate conflicts with this file, AGENTS.md wins and
this file must be corrected. The development loop is in AGENTS.md's Development
Loop section; documentation maintenance rules are in [META.en.md](META.en.md).

## Changelog Conventions

`CHANGELOG.md` follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/)
and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

| Category | Meaning |
|----------|---------|
| `Added` | New features |
| `Changed` | Changes to existing behavior |
| `Deprecated` | Features that will be removed |
| `Removed` | Removed features |
| `Fixed` | Bug fixes |
| `Security` | Security improvements |

**Breaking changes do not get a separate category**: classify them under
`Changed` (behavior change) or `Removed` (API removal), prefix the entry with
`BREAKING:`, and document the migration path in the body.

### Baseline and Versions

- The baseline is the **last formal release tag** (for example `v0.0.9`), not a
  nightly tag.
- Identifiers such as `-nightly`, `-alpha`, or `-preview` are snapshot labels,
  not semantic versions. Those changes stay in `[Unreleased]`; only an unsuffixed
  formal version creates a `## [x.y.z] - YYYY-MM-DD` block.
- Do not record intra-version back-and-forth (added then removed, introduced then
  fixed). Record only the final state.
- Describe user-visible behavior impact, not internal implementation details; do
  not mis-record deliberate cross-module designs as `Fixed`.

### Writing Process

1. Find the last formal release tag.
2. Compare that tag with the current HEAD.
3. Filter out intra-version churn and categorize significant user-visible changes.
4. Write the entries into the matching categories under `[Unreleased]`.

## Weekly Snapshot Builds

- The scheduled run is every Monday at **02:30 UTC** and covers the **week that
  just ended**: `[previous Monday 00:00, current Monday 00:00) UTC`.
- An idle window publishes nothing; `workflow_dispatch` can snapshot the current
  partial week on demand.
- Snapshot tags look like `v<base>-nightly.YYYYMMDD`, where `<base>` is derived
  from `<Version>` in `Directory.Build.props`.
- Snapshot tags reuse the formal release pipeline for packages and documentation
  snapshots; `verify-release.sh` skips formal metadata verification for versions
  containing `-`.

## Formal Release Checklist

Complete these steps in order before tagging:

1. Choose the new version `x.y.z` (no `-nightly` or similar suffix).
2. Move `[Unreleased]` content into a `## [x.y.z] - YYYY-MM-DD` block and clear
   `[Unreleased]`.
3. Update `<Version>` in `Directory.Build.props` to `x.y.z`; the tag is `vx.y.z`,
   and the pipeline compares versions after stripping the `v` prefix.
4. Update the version text in `docs/README.zh.md` and `docs/README.en.md` to
   `x.y.z`.
5. Move shipped rules from `Origo.SourceGeneration/AnalyzerReleases.Unshipped.md`
   to `AnalyzerReleases.Shipped.md` and add a `## Release x.y.z` block.
6. Run `TAG_VERSION=x.y.z bash scripts/verify-release.sh` and confirm it passes.
   The check requires: the matching CHANGELOG version block, an empty
   `[Unreleased]`, the analyzer shipped block, no unshipped rules in
   `AnalyzerReleases.Unshipped.md`, and both `docs/README.*` files mentioning
   the version.
7. Run `dotnet run --project tools/DocSyncTool -- generate`.
8. Commit `CHANGELOG.md`, `Directory.Build.props`, the analyzer release files,
   both `docs/README` version stamps, all docs content, generated hubs, and
   `.sync-status.json` together.
9. Run the full `bash scripts/ci.sh` on that commit and confirm lint-scripts,
   format, doc-sync, test, benchmark, and Godot integration all pass. Amend and
   rerun if it fails.
10. Run `bash scripts/lint-commits.sh` on that commit; fix and amend if it fails.
11. Create and push tag `vx.y.z` on the verified commit.

## Release Pipeline Artifacts

Pushing a `v*` tag triggers the Release workflow:

- Validate that the tag matches `<Version>` exactly after stripping only the leading `v`; a `-nightly` / `-alpha` suffix in the tag must already be present in `<Version>`;
- Run `verify-release.sh`, the committed DocSync check, and the full test suite;
- Pack `Origo.Core`, `Origo.GodotAdapter`, and `Origo.ConsoleBridge` as NuGet
  packages;
- Build a documentation snapshot archive containing `docs/`, `AGENTS.md`, and
  `CHANGELOG.md`;
- Attach packages and the documentation snapshot to the GitHub Release.

Packages are not pushed to nuget.org as formal artifacts; consumers download them
from the GitHub Release and configure a local package source as described in the
root [README](../README.md).

## Related Files

- [CHANGELOG.md](../CHANGELOG.md)
- [scripts/verify-release.sh](../scripts/verify-release.sh)
- [.github/workflows/release.yml](../.github/workflows/release.yml)
- [.github/workflows/weekly-build.yml](../.github/workflows/weekly-build.yml)

---
[↑ Back to Origo Manual](README.en.md)
