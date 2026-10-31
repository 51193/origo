# Repository Scripts

> [↑ Back to AGENTS.md](../AGENTS.md)
> [Manual](../docs/README.en.md) · [Environment Bootstrap](../docs/META.en.md#environment-bootstrap)

This directory is grouped by implementation language. Pick the entry point for
the host OS; do not call a script from another platform's directory.

## Start Here

| Host | Full gate | Notes |
|---|---|---|
| Linux / macOS | `bash scripts/bash/ci.sh` | POSIX shell, Bash 3.2+ |
| Windows | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\powershell\ci.ps1` | Windows PowerShell 5.1+ |

Both entry points implement the same development gate sequence. Bash-free shared Python
validators run on both; Bash-backed POSIX self-tests remain Linux-side and the
Windows suite is not a release gate. The POSIX scripts
never invoke the PowerShell suite and vice versa.

## Layout

```text
scripts/
  README.md        this file
  bash/            POSIX shell entry points, helpers, and self-tests
  powershell/      Windows PowerShell suite
  python/          cross-platform Python helpers
dotnet             repository-root SDK launcher (Bash; source in bash/)
```

Rules:

- `.sh` files belong in `bash/`, `.ps1` in `powershell/`, `.py` in `python/`.
- `scripts/` root contains only this README. Add new scripts under the language
  directory.
- Entry points are `ci` and the gate named after the command (`format`, `test`,
  `benchmark`, ...). Files prefixed `test-` are self-tests; files ending
  `-lib` or `-env` are sourced helpers, not entry points.
- When a script path changes, update this file and every reference in
  `AGENTS.md`, `docs/`, `.github/`, and `.githooks/` in the same change.

## Gate Map

| Gate | POSIX | Windows |
|---|---|---|
| Install SDK | `bash/install-dotnet.sh` | `powershell/install-dotnet.ps1` |
| Format | `bash/format.sh` | `powershell/format.ps1` |
| Shell API baseline | `bash/api-inventory.sh` | `powershell/api-inventory.ps1` |
| Build + tests + coverage | `bash/test.sh` | `powershell/test.ps1` |
| Benchmarks | `bash/benchmark.sh` | `powershell/benchmark.ps1` |
| Godot integration | `bash/godot-test.sh` | `powershell/godot-test.ps1` |
| Package consumer smoke | `bash/package-consumer-smoke.sh` | `powershell/package-consumer-smoke.ps1` |
| Script lint | `bash/lint-scripts.sh` | `powershell/lint-scripts.ps1` |
| Layered checks | `bash/check.sh` | `powershell/check.ps1` |
| Commit lint | `bash/lint-commits.sh` | `powershell/lint-commits.ps1` |
| Documentation generation/check | `bash/docs.sh` | `powershell/docs.ps1` |
| Full pipeline | `bash/ci.sh` | `powershell/ci.ps1` |
| Godot download | `bash/download-godot.sh` | `powershell/download-godot.ps1` |

## Bash (`scripts/bash`)

| Script | Purpose |
|---|---|
| `ci.sh` | Full local gate: lint, format, API, test, benchmark, Godot, package smoke |
| `docs.sh` | Generate or validate committed documentation artifacts (Bash) |
| `check.sh` | Layered `plan` / `quick` / `affected` / `full` verification |
| `test.sh` | Restore, build, `dotnet test`, Coverlet line coverage |
| `format.sh` | `dotnet format --verify-no-changes` |
| `api-inventory.sh` | Shell API inventory and previous-release comparison |
| `benchmark.sh` | Benchmark tests and baseline comparison |
| `godot-test.sh` | Godot headless integration tests |
| `package-consumer-smoke.sh` | Packed-package consumer restore/build/startup smoke |
| `lint-scripts.sh` | ShellCheck, Python syntax, workflow YAML, script self-tests |
| `lint-commits.sh` | Conventional Commit message lint |
| `install-dotnet.sh` | Install the `global.json` SDK |
| `dotnet-env.sh` | Source-only SDK selection and `global.json` validation |
| `download-godot.sh` | Resolve/cache the Godot mono binary |
| `find-previous-api-baseline.sh` | Select the previous formal release API baseline |
| `check-weekly-base.sh` | Validate weekly snapshot base/version |
| `release-check.sh` | Local release preflight in a temporary worktree |
| `verify-release.sh` | Release metadata verification |
| `verify-release-provenance.sh` | Tag ancestry and release-commit provenance |
| `validate-release-packages.sh` | Validate the five-package release artifact set |
| `validate-release-artifacts.sh` | Validate the exact nine release asset names before publishing |
| `validate-release-source.sh` | Validate the release source archive |
| `work-identity.sh` | Branch/worktree identity validation |
| `package-consumer-smoke-lib.sh` | Source-only helpers for the package smoke |
| `test-api-inventory-parent.sh` | Bash API-inventory parent/metadata-commit regression tests |
| `test-docs.sh` | Generated-document and local-link validation |
| `test-check-weekly-base.sh` | Weekly snapshot base/version validation |
| `test-dev-suite.sh` | Bash/PowerShell gate parity, test-project parity, self-test wiring, and script-map guard |
| `test-dotnet-env.sh` | SDK bootstrap, selection, and installer regression tests |
| `test-download-godot-deps.sh` | Godot downloader dependency/cache tests |
| `test-find-previous-api-baseline.sh` | Previous release baseline selection tests |
| `test-lint-pr-body.sh` | PR title/body linter regression tests |
| `test-package-consumer-smoke-lib.sh` | Package-smoke helper and fixture contract tests |
| `test-pre-push-hook.sh` | Pre-push commit-lint hook tests |
| `test-release-check.sh` | Release preflight regression tests |
| `test-validate-release-source.sh` | Release source archive validator tests |
| `test-validate-release-artifacts.sh` | Release artifact-set validator tests |
| `test-verify-release-provenance.sh` | Release provenance verifier tests |
| `test-verify-release.sh` | Release metadata verifier tests |
| `test-write-version-stamp.sh` | Version stamp helper tests |

**Script-lint environment**: CI hard-fails when `shellcheck` is missing, while a
local run without `shellcheck` prints a warning and still runs `bash -n`; local
and CI results can also differ with the installed `shellcheck` version. This is
the recorded supported-environment limitation tracked by issue #52, not a
fallback that makes an uninstalled local gate fail.

**Bash-only release pipeline**: the release/weekly validators
(`release-check.sh`, `verify-release*.sh`, `validate-release-*.sh`,
`find-previous-api-baseline.sh`, `check-weekly-base.sh`) and their self-tests
are intentionally Bash-only, like the rest of the release pipeline. The Release
and Weekly Build workflows run on `ubuntu-latest` only, so the PowerShell suite
mirrors the development gates but not the release pipeline; this is why
`validate-release-artifacts.sh` has no `scripts/powershell/` counterpart.

## Python (`scripts/python`)

| Script | Purpose |
|---|---|
| `check.py` | Planner/executor used by `check.sh` and `check.ps1` |
| `docs_generate.py` | Generate committed contract API and validate local documentation links |
| `docs_generate_test.py` | Focused tests for the documentation generator and checks |
| `check_test.py` | Regression tests for the layered-check planner |
| `lint-pr-body.py` | PR title/body lint for squash commit messages |
| `validate-agent-docs.py` | AGENTS.md structure and required-snippet guard |
| `validate-release-workflow.py` | Release/weekly workflow contract guard |
| `test-validate-release-workflow.py` | Tests for the release workflow guard |
| `test-validate-release-packages.py` | Exercises the release package validator |
| `work_identity.py` | Branch/worktree identity implementation |
| `work_identity_test.py` | Tests for branch/worktree identity |
| `write-version-stamp.py` | Stamp version properties for release/snapshot commits |

These helpers are invoked through the Bash or PowerShell gates; they are not
standalone platform entry points.

## PowerShell (`scripts/powershell`)

| Script | Purpose |
|---|---|
| `ci.ps1` | Full Windows gate: lint, format, API, test, benchmark, Godot, package smoke |
| `check.ps1` | Layered `plan` / `quick` / `affected` / `full` verification |
| `test.ps1` | Restore, build, `dotnet test`, Coverlet line coverage |
| `format.ps1` | `dotnet format --verify-no-changes` |
| `api-inventory.ps1` | Shell API inventory and previous-release comparison |
| `benchmark.ps1` | Benchmark tests and baseline comparison |
| `godot-test.ps1` | Godot headless integration tests |
| `package-consumer-smoke.ps1` | Packed-package consumer restore/build/startup smoke |
| `lint-scripts.ps1` | PowerShell parse check and Python guard scripts |
| `lint-commits.ps1` | Conventional Commit message lint |
| `docs.ps1` | Generate or validate committed documentation artifacts |
| `test-api-inventory-parent.ps1` | Git parent-resolution regression for the API inventory |
| `install-dotnet.ps1` | Install the `global.json` SDK |
| `download-godot.ps1` | Resolve/cache the Godot mono binary |
| `common.ps1` | Source-only SDK/Python/Godot/process helpers |

## Adding or Moving Scripts

1. Place the file under `bash/`, `powershell/`, or `python/` by suffix.
2. Update this README, `AGENTS.md`, and the affected `docs/` entry.
3. Update every reference in `.github/`, `.githooks/`, and the other scripts.
4. Add or update a `test-*` self-test when the script has behavior that can
   regress.
5. Run the full gate for the host and the affected self-tests.
