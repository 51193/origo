# Repository Maintenance Log

> [↑ Back to Origo Manual](README.en.md)

Repository-facing maintenance changes that do not change user-visible runtime, public API, save-format, package behavior, published release artifacts, or release status. Entries are moved here from the user-facing [Changelog](../CHANGELOG.md) and retain their original wording; this is a rolling log and is not cleared or archived by the release checklist.

## Current (rolling)

### Development and Verification Tooling

CI workflow shape, local test/coverage gates, package-consumer fixture smoke coverage, shell-API baseline tooling, PowerShell/Bash parity, and developer-suite commands.

- **Dedicated Bash development-suite CI job** — the paired `bash-checks`/
  `powershell-checks` and `bash-lint`/`powershell-lint` CI jobs run the Bash
  and PowerShell suites side by side. The parity gate fails if any of the four
  dedicated CI jobs disappears.

- **Bash development-suite parity gates** — `scripts/bash/test-dev-suite.sh`
  verifies that `ci.sh` and `ci.ps1` run the same ordered gate sequence, every
  Bash self-test is wired into `lint-scripts.sh`, the PowerShell lint gate keeps
  its Bash-free shared checks, and every script appears in the script map.
  `scripts/bash/test-api-inventory-parent.sh` covers the Bash counterpart of
  the PowerShell metadata-release parent regression.

- **Native Windows PowerShell development suite** — `scripts/powershell/` implements the full development gate set without invoking the POSIX scripts: SDK install (`dotnet-install.ps1`), format, shell API baseline and previous-package comparison, build/tests/coverage, benchmarks, Godot headless integration, package consumer smoke, layered `plan`/`quick`/`affected`/`full` checks, and commit lint. `common.ps1` centralizes SDK/Python/Godot resolution and disables MSBuild node reuse for reliable output capture; `ci.ps1` mirrors `scripts/bash/ci.sh`. The paired `powershell-lint`/`powershell-checks` CI jobs verify the suite alongside the Bash lint and Windows build jobs, and [docs/usage/windows-development.en.md](usage/windows-development.en.md) documents prerequisites and usage.

- **Shell API baseline records generic variance and nullable-flow attributes** —
  the Roslyn inventory now records `in`/`out` variance and the nullable-flow
  attributes that affect consumer analysis, with `schemaVersion` 2; previous-package
  validation projects an older (v1) baseline to the legacy shape so the released
  0.1.0 surface stays comparable, and previous-baseline resolution now fails closed
  when a formal release cannot resolve a usable baseline (the pre-first-release skip
  is explicit).

- **CI jobs have bounded runtimes** — every CI, release, and CodeQL job sets
  `timeout-minutes`, and apt steps run under `timeout 300` with
  `Acquire::Retries=3`, so a hung infrastructure step fails the required check
  instead of occupying it for hours.

- **Test gates are pinned** — `scripts/bash/test.sh` refuses to run when a measured
  test project loses its Coverlet coverage configuration, threshold, filter, or
  solution membership; `scripts/bash/benchmark.sh` fails when a baseline benchmark
  metric disappears from the run; architecture guard helpers gain negative-control
  tests proving they report known violations.

- **Public shell signatures are generated and checked locally** — a committed
  generated contract API page is produced from the shell API baseline. The
  documentation gate also validates local links and the contractual integration
  test/type inventories; generated files are committed because the repository
  does not depend on a remote documentation service.

- **Package-consumer smoke exercises a demo-like capability path** — the
  Core/Adapter fixture now discovers a consumer strategy, loads an authored
  entity/node, saves and reloads state, and verifies hook data before printing
  `DEMO_CONSUMER_OK`; the Windows mirror runs the same scenario.

- **CI and release Godot verification gates are structurally pinned** — `scripts/python/verify-ci-workflow.py` pins the CI workflow triggers, required jobs, matrices, environment, action inputs, step order, shells, and exact commands, so a deleted or masked required job or step fails locally. The release workflow guard now pins the complete `godot-integration-tests` job body (checkout ref, caches, apt packages, tag-layout probe, and final command) instead of only the release job's `needs:` edge. The Bash and PowerShell lint gates run the same shared guards, so the required check set cannot be neutralized by editing the workflow while the local gates stay green.

- **The Bash development-suite parity gate checks the PowerShell test-project
  list against `Origo.sln`** — a new xUnit project can no longer run only on the
  Linux gate, and a listed project file that does not exist is rejected.

- **Bash/PowerShell test-project parity covers both project lists** —
  `scripts/bash/test-dev-suite.sh` compares the solution's xUnit projects with both
  `$testProjects` and `$directProjects` in the PowerShell suite, so a new project
  cannot silently run on only one platform.

- **Windows PowerShell parent resolution and gate dispatch are correct** — API inventory uses `~1` instead of a caret that `cmd.exe` consumes, `check.py` accepts an explicit suite and dispatches Windows Godot checks to `godot-test.ps1`, and the Windows lint/godot/package gates cover the shared release, pin, and identity checks.

- **Coverage runs no longer emit a false test-host crash report in read-only-runtime sandboxes** — `scripts/bash/test.sh` gives the test host a private writable `XDG_RUNTIME_DIR`; Coverlet's named-mutex unload path otherwise reports an unhandled `System.IO.IOException` after a fully passing run when `/run/user/<id>` is EROFS.

### Release and Distribution Tooling

Release/snapshot workflow mechanics, provenance, validators, version stamping, and preflight checks that do not change published artifact sets or release status.

- **Approved pre-1.0 breaking changes in the shell API gate** — a reviewed
  `tools/ApiInventoryTool/shell-api-approved-breaks.json` records the exact
  previous-baseline line and a reason for a maintainer-approved break;
  undeclared, malformed, duplicate, dormant, or wrong-assembly entries still
  fail the gate and the CI/release workflow, while obsolete approvals whose line
  a newer release already absorbed are reported for cleanup instead of failing.

- **Validated release artifact set** — `scripts/bash/validate-release-artifacts.sh`
  asserts the exact nine documented release assets (five packages, source archive,
  docs `tar.gz`/`zip`, and `nuget.config`) with no extras before the Release
  workflow moves the tag; the workflow guard requires the artifact assembly steps
  and their order, and the validator is staged from `origin/main` like the other
  trusted validators so snapshot tags that predate it can still be recovered.

- **Validated release source archive** — the Release workflow builds
  `origo-source-<version>.tar.gz` from the stamped release commit and validates
  its `<Version>` / `AssemblyVersion` / `FileVersion` (plus the formal CHANGELOG
  and analyzer metadata) before moving the tag. The archive is attached to the
  GitHub Release as the authoritative source snapshot.

- **Local release preflight** — `scripts/bash/release-check.sh <x.y.z>` stamps a
  temporary worktree and runs the strict release verifier without modifying the
  caller's checkout, so `main` can keep a development baseline. It rejects a
  HEAD baseline that is not a newer `-dev` version, is not newer than the newest
  formal release, or carries numeric assembly stamps that do not match that
  baseline.

- **Release provenance gate** — `scripts/bash/verify-release-provenance.sh` rejects a
  formal tag on unmerged feature history and a snapshot tag that is not
  `main` history or a weekly/release version-stamp commit based on `main`. It
  accepts only version element changes in the metadata commit, and the release
  job fetches the
  validators from `main` instead of executing the tag tree's copies. The release
  job also uses an explicit checkout ref so a re-run after a tag move still
  starts, and a metadata commit for a different version is rejected.

- **Formal releases stamp a tag-only release commit** — the Release workflow no
  longer writes a version commit back to `main`. It rewrites
  `Directory.Build.props` from the tag, creates a metadata-only release commit,
  runs every gate on it, moves only the tag after packing succeeds, and leaves
  `main` on `0.1.1-dev`. A version-only PR is no longer required before
  packaging; a `CHANGELOG`/analyzer release-prep PR is still required. The
  publish step rechecks the tag on every run, so a rerun still moves a stale
  tag.

- **Weekly snapshots require an advanced development base** — the weekly job
  fails fast when `<Version>` is not newer than the latest formal release, so a
  forgotten base bump cannot publish a snapshot that sorts below an existing
  release.

- **Previous-package API comparison uses the newest baseline not newer
  than the source version** — a formal release tag newer than the source version
  is skipped instead of aborting the gate, so a stale development baseline no
  longer blocks comparison; a line added in the skipped release and removed
  again is invisible to that comparison, while the committed-baseline `verify`
  gate still enforces same-schema changes. Obsolete approved breaks must be
  deleted after the release that makes them obsolete. The trade-off is
  documented in the shell API baseline manual.

- **Release workflow guard rejects neutralized gates** — required commands only count
  when a non-comment shell line actually runs them (an `echo`, a comment, `|| true`,
  a chained `;`/`&`/`|`, command substitution, or a `true` substitute no longer
  satisfies the guard); required steps must use the default/Bash shell and may not
  hide commands behind PowerShell comments, traps, a control-flow branch, a heredoc,
  or a success exit/return before the command, nor set `if:`/`continue-on-error:`
  (the documented tag-push idempotent path and snapshot marker step are the only
  allowlisted exceptions). The release job must keep the full ordered gate set
  (lint, format, API baseline, tests/coverage, benchmarks, package consumer smoke,
  and the Godot prerequisite job) before the tag moves. Trusted validators must be
  staged with the exact reviewed `git show ... > ...` lines; other writes,
  variable/cwd-relative writes, and tag-tree copies into `.release-validators/` are
  rejected, and a required validator must be invoked as exactly the reviewed
  `bash <path>` command. The artifact-set validator is bound to its exact reviewed
  argv and no artifact-directory write may follow it; the weekly check/bump/dispatch
  conditions are pinned, and the weekly completeness assertion must be a code line
  starting with `[[` and ending with `]]` that precedes any early exit (including
  heredoc/backslash/quoted/multiple-heredoc masking).

- **Release metadata verification runs the trusted verifier from the correct depth** — the Release workflow now stages `verify-release.sh` at `.release-validators/scripts/verify-release.sh`, where its `../..` root resolution reaches the repository; the workflow guard requires the metadata step and its exact staging path, and the self-test executes both the correct and one-level-too-deep layouts.

- **Weekly snapshots recover all orphaned/incomplete releases without dropping the current week** — the workflow dispatches every missing/draft/partial release for the current base (not exactly the nine expected asset names) and still creates today's tag; GitHub API errors are distinguished from 404, and the release action receives `prerelease` at creation.

- **Release verification rejects unshipped rules, prefix-only analyzer blocks, and malformed dates** — the metadata verifier now recognizes the standard `ORIGOSGnnn | ...` row format, matches `## Release x.y.z` as a whole heading, and requires a real `YYYY-MM-DD` CHANGELOG date.

- **Release preflight validates the HEAD baseline's assembly stamps** — `release-check.sh` now rejects a development baseline whose `AssemblyVersion` / `FileVersion` do not match its `-dev` `<Version>`, instead of re-stamping the values it is supposed to verify.

- **Release tags must have the exact `x.y.z` shape** — `write-version-stamp.py`
  and `verify-release.sh` now reject truncated (`0.1`) and four-part
  (`0.1.0.0`) versions before rewriting or verifying metadata; those shapes
  previously produced assembly stamps that could never match the release tag.

- **Normal CI actually enforces the previous-release API baseline** — the
  build-and-test checkout now fetches full history and tags, and a dedicated
  step fails before the shell API gate if no formal release tag is visible.
  Previously a depth-1 checkout had no tags, so
  `find-previous-api-baseline.sh` silently fell back to the first-release
  message and the previous-package comparison never ran outside the Release
  workflow.

- **Previous release baselines survive tag-only releases** — tag-only release
  commits are siblings of the next release commit, not its ancestors. The API
  baseline helper now selects the newest formal tag not newer than the current
  source version and accepts a metadata-only release commit whose parent is
  reachable, instead of silently falling back to an older baseline.

- **Weekly snapshots push only their tag** — the weekly build no longer pushes
  its version-bump commit to `main`; it tags that commit and pushes only
  `refs/tags/<tag>`. The tagged commit already carries the snapshot version
  stamps, so the release pipeline finds nothing to rewrite, branch protection
  cannot reject a snapshot, and `main` keeps its own `<Version>`. The
  release-workflow guard rejects any weekly step that pushes a branch ref or
  adds a second push.

### Documentation Tooling

Documentation generation, version-link maintenance, and local link/anchor validation.

- **Documentation generation has an explicit maintenance contract** — the
  pre-commit hook and normal lint gate run the local documentation check;
  extension rules for generated artifacts, summary documents, checked counts,
  and source-of-truth updates live in `docs/META.en.md` and
  `docs/generated/README.en.md`.

- **The manual links to the build version instead of copying it** —
  `docs/README.en.md` points at `<Version>` in `Directory.Build.props`, and
  `verify-release.sh` checks the stamped build properties rather than a second
  documentation literal.

- **Local documentation link validation covers the script map and GitHub
  Markdown** — `docs.sh check` also validates links in `scripts/README.md` and
  `.github/**/*.md`.

### Contributor Workflow and Repository Layout

Contributor-facing workflow, pull-request policy, and script/repository layout changes.

- **PR title/body lint for squash-merge commit messages** — the `pr-body-lint` workflow checks the PR title as a Conventional Commit subject and the PR body lines that GitHub uses for the squash commit, running the linter from `origin/main`; merge commits remain exempt from the local message lint.

- **BREAKING: repository scripts are grouped by implementation language** — POSIX scripts live under `scripts/bash/`, the Windows PowerShell suite under `scripts/powershell/`, and cross-platform Python helpers under `scripts/python/`; `scripts/README.md` is the gate and directory map. Entry points are now `bash scripts/bash/ci.sh` and `powershell -File scripts/powershell/ci.ps1`. Automation that invoked the old `scripts/*.sh`, `scripts/*.py` paths must update; no compatibility wrappers are provided.

---

[↑ Back to Origo Manual](README.en.md)
