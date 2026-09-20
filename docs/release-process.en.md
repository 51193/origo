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
  just ended**: `[previous Monday 00:00, current Monday 00:00) UTC`. The run
  only starts from `main`, and it ignores the version-bump commits it creates
  itself, so an idle window publishes nothing.
- `workflow_dispatch` can snapshot the current partial week on demand.
- Snapshot tags look like `v<base>-nightly.YYYYMMDD`, where `<base>` is derived
  from `<Version>` in `Directory.Build.props`.
- A publish bumps `<Version>` to the snapshot version, derives
  `AssemblyVersion` / `FileVersion` from its numeric base, commits all three
  stamps to `main`, tags that commit, and then starts the Release workflow with
  `gh workflow run release.yml -f tag=<tag>`. The explicit dispatch is required
  because a tag pushed with `GITHUB_TOKEN` does not trigger `push` workflows and
  `workflow_dispatch` is the documented exception, so no personal access token
  or GitHub App is needed.
- The base version is the numeric part of the current `<Version>`, so bumping the
  base (`0.0.10-nightly.YYYYMMDD` to `0.0.11-nightly.YYYYMMDD` or `0.0.11`) moves
  the snapshot tag, the NuGet package version, and the assembly metadata
  together; the release pipeline then finds no version stamp left to rewrite.
- The weekly job asks for a snapshot release (`gh workflow run ... -f
  snapshot=true`), which publishes the GitHub Release as a **pre-release**, so an
  automatic snapshot never becomes the repository's Latest release. A tag pushed
  or released by hand is never forced either way: its release state stays as
  chosen on GitHub.
- Snapshot tags reuse the formal release pipeline for packages and documentation
  snapshots; `verify-release.sh` skips formal metadata verification for versions
  containing `-`.

## Formal Release Checklist

Complete these steps in order before tagging:

1. Choose the new version `x.y.z` (no `-nightly` or similar suffix).
2. Move `[Unreleased]` content into a `## [x.y.z] - YYYY-MM-DD` block and clear
   `[Unreleased]`.
3. Update `<Version>` in `Directory.Build.props` to `x.y.z` and set
   `AssemblyVersion` / `FileVersion` to `x.y.z.0`; the tag is `vx.y.z`. If the
   tagged commit still carries a different stamp, the release pipeline creates
   a release commit to correct it and, after tests and packing succeed, writes
   it back to `main` and the tag; this requires the tag to be created on the
   current `main` tip, and keeping the tagged commit correct remains preferred.
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

The Release workflow starts from a pushed `v*` tag or from `workflow_dispatch`
with the `tag` input, which is how weekly snapshots start it. The tag is resolved
first; every step below then applies to that tag:

- Before validation, rewrite `Directory.Build.props` from the tag: strip only the leading `v` for `<Version>`, and derive `AssemblyVersion` / `FileVersion` from its four-part numeric form (a `-nightly` / `-alpha` suffix stays only in `<Version>`). The tag is then validated against `<Version>` as before. If anything changed, the pipeline creates a `chore(release): sync version stamps to <tag>` commit, and DocSync, tests, benchmarks, and packing all run on that release commit;
- Run `verify-release.sh`, the committed DocSync check, and the full test suite. The automatic rewrite covers only the version stamps; the formal metadata (CHANGELOG version block, empty `[Unreleased]`, analyzer shipped block, and both `docs/README.*` version stamps) must still be present in the tagged commit;
- Only after tests and packing succeed, a single `--atomic` push fast-forwards `main` to the release commit and moves the tag to it; the GitHub Release is created afterwards. Validation or packing failures therefore leave every ref untouched, and a successful release keeps `main`, tag, commit, and packages on one version. This write-back requires the tag to be created on the current `main` tip; if main has advanced, the tag is not there, or either ref moves during the run, the lease checks fail and abort the release. Reruns resolve the tag's current target first, so a tag already written back by this pipeline does not produce a duplicate release commit. Branch/tag protection rules or a token without permission make the atomic push fail and abort the release;
- Pack `Origo.Core`, `Origo.GodotAdapter`, and `Origo.ConsoleBridge` as NuGet
  packages;
- Build a documentation snapshot archive containing `docs/`, `AGENTS.md`, and
  `CHANGELOG.md`;
- Attach packages and the documentation snapshot to the GitHub Release, marked
  as a pre-release when the run was started as a snapshot (the `snapshot` input,
  which the weekly job sets); a tag pushed by hand keeps the release state chosen
  on GitHub.

Writing back fast-forwards `main` and moves an existing `v*` ref: local clones
that already fetched the old tag need `git fetch --tags --force` to see the new
release commit. The write-back uses `GITHUB_TOKEN`, which does not trigger another
workflow run; every validation, test, benchmark, and pack step runs in the same
run. If the process requires release tags to be immutable, push the release commit
to `main` or a dedicated branch instead of moving the tag.

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
