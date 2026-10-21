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

- The baseline is the **last formal release tag** (for example `v0.1.0`), not a
  nightly tag.
- Identifiers such as `-nightly`, `-alpha`, or `-preview` are snapshot labels,
  not semantic versions. Those changes stay in `[Unreleased]`; only an unsuffixed
  formal version creates a `## [x.y.z] - YYYY-MM-DD` block.
- Do not record intra-version back-and-forth (added then removed, introduced then
  fixed). Record only the final state.
- Describe user-visible behavior impact, not internal implementation details; do
  not mis-record deliberate cross-module designs as `Fixed`.

### Source Version Model

- `main` carries the **next development baseline**, for example `0.1.1-dev`
  with `AssemblyVersion` / `FileVersion` `0.1.1.0`. It never carries the release
  version.
- A release tag carries the exact release version. The Release workflow rewrites
  `Directory.Build.props` from the tag in a metadata-only commit, moves **only
  the tag** to that commit, and leaves `main` untouched. The commit is not on
  `main`; its parent is the main commit the release was cut from.
- `docs/README.en.md` refers to `Directory.Build.props` instead of copying the
  version, so documentation cannot drift from the build stamp.
- The development baseline must be newer than the latest formal release before
  a weekly snapshot runs; the weekly workflow fails explicitly instead of
  publishing a snapshot that sorts below an existing release.

### Writing Process

1. Find the last formal release tag.
2. Compare that tag with the current HEAD.
3. Filter out intra-version churn and categorize significant user-visible changes.
4. Write the entries into the matching categories under `[Unreleased]`.

## Weekly Snapshot Builds

- The scheduled run is every Monday at **02:30 UTC** and covers the **week that
  just ended**: `[previous Monday 00:00, current Monday 00:00) UTC`. The run
  only starts from `main`.
- `workflow_dispatch` can snapshot the current partial week on demand.
- Snapshot tags look like `v<base>-nightly.YYYYMMDD`, where `<base>` is derived
  from the development `<Version>` in `Directory.Build.props`.
- A publish rewrites `<Version>` to the snapshot version and derives
  `AssemblyVersion` / `FileVersion` from its numeric base, commits the stamps in
  the runner checkout, tags that commit, and pushes **only the tag**; it then
  starts the Release workflow with `gh workflow run release.yml -f tag=<tag>`.
  The tagged commit already carries the snapshot version stamps, so the release
  pipeline finds nothing to rewrite and never creates a release commit. A
  snapshot therefore never writes to `main`, branch rules that require a pull
  request cannot block one, and `main` keeps its development baseline. The
  explicit dispatch is required because a tag pushed with `GITHUB_TOKEN` does
  not trigger `push` workflows and `workflow_dispatch` is the documented
  exception, so no personal access token or GitHub App is needed.
- Before publishing, the workflow checks that the development base is newer than
  the latest formal release tag. If a post-release base bump was forgotten, the
  job fails with the required fix instead of creating `0.1.1-nightly.*` below an
  existing `0.1.1`.
- The weekly job asks for a snapshot release (`gh workflow run ... -f
  snapshot=true`), which publishes the GitHub Release as a **pre-release**, so an
  automatic snapshot never becomes the repository's Latest release. A tag pushed
  or released by hand is never forced either way: its release state stays as
  chosen on GitHub.
- Snapshot tags reuse the formal release pipeline for packages, source archives,
  and documentation snapshots; `verify-release.sh` skips formal metadata
  verification for versions containing `-`.

## Formal Release Checklist

Complete these steps in order:

1. Choose the new version `x.y.z` (no `-nightly` or similar suffix).
2. Move `[Unreleased]` content into a `## [x.y.z] - YYYY-MM-DD` block and clear
   `[Unreleased]`.
3. Keep `main` on a development baseline: set `<Version>` to the next planned
   version with a `-dev` suffix (for example `0.1.2-dev`) and set
   `AssemblyVersion` / `FileVersion` to its numeric form. Do **not** commit the
   release version; the Release workflow stamps `x.y.z` on the tag-only release
   commit. If the baseline already names the release as `x.y.z-dev`, advance it
   to the next planned base in the same release-prep PR (for example
   `0.1.2-dev` when releasing `0.1.1`); the weekly job rejects a base that is
   not newer than the latest formal release.
4. Move shipped rules from `Origo.SourceGeneration/AnalyzerReleases.Unshipped.md`
   to `AnalyzerReleases.Shipped.md` and add a `## Release x.y.z` block.
5. Commit `CHANGELOG.md`, the analyzer release files, the next development
   baseline in `Directory.Build.props`, and all other documentation changes
   together.
6. Run `bash scripts/release-check.sh x.y.z` on that commit and confirm it
   passes. The helper creates a temporary worktree, stamps `x.y.z` there, and
   runs `scripts/verify-release.sh`; the caller checkout is not modified. It
   rejects a HEAD baseline that is not a newer `-dev` version, then requires the
   stamped `<Version>` and `AssemblyVersion` / `FileVersion`, the matching
   CHANGELOG version block, an empty `[Unreleased]`, the analyzer shipped block,
   and no unshipped rules in `AnalyzerReleases.Unshipped.md`. Documentation
   refers to the build stamp instead of duplicating the version.
7. Run the full `bash scripts/ci.sh` on that commit and confirm every
   compatibility gate listed under Release Pipeline Artifacts below passes.
8. Run `bash scripts/lint-commits.sh` on that commit; fix and amend if it fails.
9. Create and push tag `vx.y.z` on the verified commit. A formal tag must point
   at a commit reachable from `main` (or at the previous metadata-only release
   commit on a rerun); the Release workflow rejects a tag on an unmerged feature
   commit.
10. The Release workflow verifies provenance, creates the tag-only release
    commit, runs the gates, moves the tag to it after packing succeeds, and
    creates the GitHub Release. `main` stays on its development baseline.

## Release Pipeline Artifacts

The Release workflow starts from a pushed `v*` tag or from `workflow_dispatch`
with the `tag` input, which is how weekly snapshots start it. The tag is resolved
first; every step below then applies to that tag:

- Before stamping, fetch the validators from `origin/main` and verify tag provenance with `scripts/verify-release-provenance.sh`: a formal tag must point at main history or the metadata-only release commit from an earlier run; a snapshot tag must point at main history or at a weekly/release version-stamp commit based on main. The metadata commit may change only the three version elements in `Directory.Build.props`, so a props-only commit cannot smuggle extra MSBuild logic. The validators are not executed from the tag tree. Tag creation remains a trusted operation: a tag-triggered workflow still uses the workflow file at the tag, so the gate prevents accidental unmerged tags rather than a malicious tag writer;
- Rewrite `Directory.Build.props` from the tag through the shared `scripts/write-version-stamp.py` helper (also used by the weekly snapshot workflow): strip only the leading `v` for `<Version>`, and derive `AssemblyVersion` / `FileVersion` from its four-part numeric form (a `-nightly` / `-alpha` suffix stays only in `<Version>`). If anything changed, the pipeline creates a `chore(release): sync version stamps to <tag>` commit whose parent is the tagged main commit;
- Run `verify-release.sh`, `scripts/lint-scripts.sh`, and the full test suite. The automatic rewrite covers only the version stamps; the formal metadata (CHANGELOG version block, empty `[Unreleased]`, and analyzer shipped block) must still be present in the tagged commit;
- Run all compatibility gates: `scripts/lint-scripts.sh` (script, workflow, and instruction guards),
  `scripts/api-inventory.sh` (shell API baseline; auto-extracts the previous formal tag's baseline when no explicit previous baseline is set, allowing compatible additions while rejecting removals/signature changes),
  `scripts/test.sh` (behavior and save contracts), benchmarks, Godot headless
  integration, and `scripts/package-consumer-smoke.sh` (shell-only restore,
  negative compile probe, and startup);
- Pack `Origo.Core.Contracts`, `Origo.Core.Kernel`, `Origo.Core`,
  `Origo.GodotAdapter`, and `Origo.ConsoleBridge` as NuGet packages;
- Run `scripts/validate-release-packages.sh` to validate the five package
  identities and versions, exact shell/kernel pairing, dependency direction
  (including the adapter's `GodotSharp` dependency matching the repository's
  Godot.NET.Sdk pin), analyzer assets, and kernel isolation; any missing,
  unexpected, or mismatched package aborts the release;
- Build `origo-source-<version>.tar.gz` with `git archive` from the release
  commit and validate it with `scripts/validate-release-source.sh`. The archive
  must carry the exact `<Version>` / `AssemblyVersion` / `FileVersion`, the
  expected `origo-<version>/` root, and, for a formal release, the same metadata
  verifier as the checkout. This is the authoritative source snapshot; GitHub's
  automatically generated source links resolve the same tag;
- Build a documentation snapshot archive containing `docs/`, `AGENTS.md`,
  `CHANGELOG.md`, and `Directory.Build.props` (the version authority referenced
  by the manual);
- Only after tests, package validation, and source-archive validation succeed,
  move **only the tag** to the release commit with a lease check. `main` is
  never written, so branch rules that require a pull request cannot block a
  release. Validation or packing failures leave every ref untouched. A fresh
  workflow run -- including a GitHub re-run, whose checkout now uses an
  explicit ref -- resolves the tag's current target first, so a tag already
  moved by this pipeline does not produce a duplicate release commit;
- Attach the five packages, the source archive, and the documentation snapshot to
  the GitHub Release, marked as a pre-release when the run was started as a
  snapshot (the `snapshot` input, which the weekly job sets); a tag pushed by hand
  keeps the release state chosen on GitHub.

The release commit is not on `main`: `main` keeps the development baseline and
the tag is the only ref that moves. Local clones that already fetched the old tag
need `git fetch --tags --force` to see the release commit. The pipeline uses
`GITHUB_TOKEN`, which does not trigger another workflow run; every validation,
test, benchmark, and pack step runs in the same run. If the process requires
release tags to be immutable, create the tag after the stamped commit exists
(for example a dispatch-created tag) instead of moving an existing tag.

Packages are not pushed to nuget.org as formal artifacts; consumers download them
from the GitHub Release and configure a local package source as described in the
root [README](../README.md).

## Related Files

- [CHANGELOG.md](../CHANGELOG.md)
- [scripts/verify-release.sh](../scripts/verify-release.sh)
- [scripts/release-check.sh](../scripts/release-check.sh)
- [scripts/validate-release-source.sh](../scripts/validate-release-source.sh)
- [scripts/verify-release-provenance.sh](../scripts/verify-release-provenance.sh)
- [scripts/check-weekly-base.sh](../scripts/check-weekly-base.sh)
- [scripts/find-previous-api-baseline.sh](../scripts/find-previous-api-baseline.sh)
- [.github/workflows/release.yml](../.github/workflows/release.yml)
- [.github/workflows/weekly-build.yml](../.github/workflows/weekly-build.yml)

---
[↑ Back to Origo Manual](README.en.md)
