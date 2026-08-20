# Contributing to Origo

Thanks for your interest in contributing. This document guides you through the
process.

## Before you start

- Read the development workflow in [`AGENTS.md`](AGENTS.md) — it is the
  authoritative entry point for all code changes.
- Read the commit message convention in [`docs/META.en.md`](docs/META.en.md#git-提交消息格式). PR commit subjects are linted by `scripts/lint-commits.sh` in the `commit-lint` workflow.
- Use the [pull request template](PULL_REQUEST_TEMPLATE.md) when opening a PR.
- Read the [code of conduct](CODE_OF_CONDUCT.md).

## Development loop

Every change must follow this cycle (see `AGENTS.md` §2 for details):

1. Develop the source change.
2. Extend or adapt tests.
3. Run `bash scripts/ci.sh` (format + build + test + coverage gates + benchmarks + Godot integration).
4. Fix and retest until everything passes.
5. Update `CHANGELOG.md` under `[Unreleased]` if the change is user-facing.
6. Sync `docs/` if public API, design decisions, or module structure changed.

## Dependency updates

Dependabot owns package version bumps. Version-coupled package families are
grouped in [`.github/dependabot.yml`](.github/dependabot.yml) and must be
updated in a single PR.

- `xunit.v3` and `xunit.v3.extensibility.core` are grouped because the
  xunit.v3 3.x metapackage pins its transitive dependencies with exact `=`
  version ranges. Bumping only one member causes NU1608 restore errors in
  every test project. `xunit.runner.visualstudio` has an independent version
  line and may move separately.
- Semver-major `xunit.v3` / `xunit.v3.extensibility.core` updates are
  ignored until the coordinated xunit.v3 4.0 / Microsoft Testing Platform
  migration. Do not remove those ignore rules without updating the xUnit
  packages, test projects, and `scripts/test.sh` in the same PR.
- `Microsoft.CodeAnalysis.*` updates are ignored because the source
  generator is loaded as an analyzer and must not reference a Roslyn
  compiler newer than the SDK in `global.json` (otherwise the build fails
  with CS9057). Bump Roslyn packages manually together with the matching
  SDK update in one PR.
- See `AGENTS.md` §1.9 for the full dependency update policy.

## Reporting issues

- For **bugs**, use the Bug Report template.
- For **feature requests**, use the Feature Request template.
- For **security issues**, see [`SECURITY.md`](SECURITY.md).

## Style

- C# code style is enforced by `.editorconfig` and validated via
  `dotnet format --verify-no-changes --severity info` in CI.
- Follow fail-fast: contracts violated → exception. No silent fallback.
- Early development: no backward-compatibility shims. See `AGENTS.md` §1.2.
