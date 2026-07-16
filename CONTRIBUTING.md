# Contributing to Origo

Thanks for your interest in contributing. This document guides you through the
process.

## Before you start

- Read the development workflow in [`AGENTS.md`](AGENTS.md) — it is the
  authoritative entry point for all code changes.
- Read the commit message convention in [`docs/META.en.md`](docs/META.en.md#git-提交消息格式).
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

## Reporting issues

- For **bugs**, use the Bug Report template.
- For **feature requests**, use the Feature Request template.
- For **security issues**, see [`SECURITY.md`](SECURITY.md).

## Style

- C# code style is enforced by `.editorconfig` and validated via
  `dotnet format --verify-no-changes --severity info` in CI.
- Follow fail-fast: contracts violated → exception. No silent fallback.
- Early development: no backward-compatibility shims. See `AGENTS.md` §1.2.
