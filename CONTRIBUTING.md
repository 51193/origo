# Contributing to Origo

Thanks for your interest in contributing. This document guides you through the
process.

## Before you start

- Read the development workflow in [`AGENTS.md`](AGENTS.md) — it is the
  authoritative entry point for all code changes.
- Read the commit message convention in [`docs/META.en.md`](docs/META.en.md#git-commit-message-format). PR commit messages are linted by `scripts/lint-commits.sh` in the `commit-lint` workflow (type, 72-character subject limit, no trailing period, body lines no longer than 72 characters). Dependabot-authored commits are skipped because Dependabot generates their message and supports only a prefix; `.github/dependabot.yml` sets that prefix to `chore(deps)` for every ecosystem.
- Read [`docs/release-process.en.md`](docs/release-process.en.md) before touching `CHANGELOG.md` or cutting a release.
- Use the [pull request template](PULL_REQUEST_TEMPLATE.md) when opening a PR.
- Read the [code of conduct](CODE_OF_CONDUCT.md).

## Development loop

Every change must follow this cycle (see the Development Loop section in `AGENTS.md` for details):

1. Develop the source change.
2. Extend or adapt tests (red-first, real-path regression for bug fixes — see `docs/Origo.Core.Tests/META-TEST.en.md`).
3. Iterate with `bash scripts/test.sh`; fix and retest until green.
4. Update `CHANGELOG.md` under `[Unreleased]` if the change is user-facing.
5. Sync `docs/` (including mirror README file lists for any `.cs` file under `SourceMirrorRoots`) and run `dotnet run --project tools/DocSyncTool -- generate`.
6. Commit source, tests, Changelog, docs content, generated hubs, and `.sync-status.json`.
7. After the commit, run `bash scripts/ci.sh` (lint-scripts + format + doc-sync + build/test + coverage gates + benchmarks + Godot integration); amend and rerun if it fails.
8. After the commit, run `bash scripts/lint-commits.sh`.

## Dependency updates

Dependabot owns package version bumps; package versions are centralized in
`Directory.Packages.props`. Version-coupled package families are
grouped in [`.github/dependabot.yml`](.github/dependabot.yml); never bump one
member independently. The authoritative groups, ignore rules, and rationale
live in that file's comments.

- `xunit.v3` and `xunit.v3.extensibility.core` move together; semver-major
  updates stay ignored until the coordinated xUnit v4 / Microsoft Testing
  Platform migration updates the packages, test projects, and
  `scripts/test.sh` in one PR.
- `Microsoft.CodeAnalysis.*` is coupled to the SDK's Roslyn compiler in
  `global.json`; bump it manually together with the matching SDK update.
- See the dependency-update rule in `AGENTS.md` for the short policy.

## Reporting issues

- For **bugs**, use the Bug Report template.
- For **feature requests**, use the Feature Request template.
- For **security issues**, see [`SECURITY.md`](SECURITY.md).

## Style

- C# code style is enforced by `.editorconfig` and validated via
  `dotnet format --verify-no-changes --severity info` in CI.
- Follow fail-fast: contracts violated → exception. No silent fallback.
- Early development: no backward-compatibility shims. See the early-development rule in `AGENTS.md`.
