# Contributing to Origo

Thanks for your interest in contributing. This document guides you through the
process.

## Before you start

- Read the development workflow in [`AGENTS.md`](AGENTS.md) — it is the
  authoritative entry point for all code changes.
- The script map in [`scripts/README.md`](scripts/README.md) explains the
  `bash/`, `powershell/`, and `python/` layout and every gate.
- Enable the repository hooks once per clone with
  `git config core.hooksPath .githooks`. The pre-commit hook runs the
  generated-document and link check; regenerate and commit docs instead of
  bypassing it.
- Read the commit message convention in [`docs/META.en.md`](docs/META.en.md#git-commit-message-format). The local pre-push hook runs `scripts/bash/lint-commits.sh` before Git uploads branch commits (type, 72-character subject limit, no trailing period, body lines no longer than 72 characters). Dependabot-authored commits are skipped because Dependabot generates their message and supports only a prefix; `.github/dependabot.yml` sets that prefix to `chore(deps)` for every ecosystem.
- Read [`docs/release-process.en.md`](docs/release-process.en.md) before touching `CHANGELOG.md` or cutting a release.
- Use the [pull request template](PULL_REQUEST_TEMPLATE.md) when opening a PR.
- Read the [review and pull-request process](docs/review-process.en.md) before a
  review round, a remediation series, or a cross-module change.
- Read the [code of conduct](CODE_OF_CONDUCT.md).

## Development loop

Every change must follow this cycle (see the Development Loop section in `AGENTS.md` for details):

1. Develop the source change.
2. Extend or adapt tests (red-first, real-path regression for bug fixes — see `docs/Origo.Core.Tests/META-TEST.en.md`).
3. Iterate with `bash scripts/bash/check.sh quick` or `bash scripts/bash/check.sh affected`; before commit run `bash scripts/bash/test.sh` and fix until green.
4. Update `CHANGELOG.md` under `[Unreleased]` if the change is user-facing.
5. Update the English documentation when behavior, interfaces, or design intent changes.
6. Commit source, tests, Changelog, and documentation together.
7. After the commit, run `bash scripts/bash/ci.sh` (lint-scripts + format + shell API baseline + build/test + coverage gates + benchmarks + Godot integration + shell package consumer smoke); amend and rerun if it fails.
8. After the commit, run `bash scripts/bash/lint-commits.sh`; the local pre-push hook repeats it and remote CI does not enforce commit lint.

## Pull requests

- One pull request carries one module or one cohesive behavior; split unrelated
  changes into separate PRs in merge order. Stack a child on the parent only
  when it cannot build on `main`.
- The PR title/body are repository-facing and become the squash commit body; do
  not include internal review-round, severity, agent-dialogue, prompt, or local
  work-buffer content. The audited-SHA/verdict record is allowed. AI assistance
  may be disclosed at a high level.
- After any amend or rebase, re-check the body's commands, coverage/test
  numbers, and file lists against the final commit. Merge only when the required
  checks are green, introduced findings are fixed, and pre-existing remaining
  findings are fixed or tracked as issues.
- Full rules: [Review and Pull-Request Process](docs/review-process.en.md).

## Dependency updates

Dependabot owns package version bumps; package versions are centralized in
`Directory.Packages.props`. Version-coupled package families are
grouped in [`.github/dependabot.yml`](.github/dependabot.yml); never bump one
member independently. The authoritative groups, ignore rules, and rationale
live in that file's comments.

- `xunit.v3` and `xunit.v3.extensibility.core` move together; semver-major
  updates stay ignored until the coordinated xUnit v4 / Microsoft Testing
  Platform migration updates the packages, test projects, and
  `scripts/bash/test.sh` in one PR.
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
