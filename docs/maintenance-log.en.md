# Repository Maintenance Log

> [↑ Back to Origo Manual](README.en.md)

Repository-facing maintenance changes that do not change user-visible runtime, public API, save-format, package behavior, published release artifacts, or release status. Entries are moved here from the user-facing [Changelog](../CHANGELOG.md) and retain their original wording; this is a rolling log and is not cleared or archived by the release checklist.

## Current (rolling)

### Development and Verification Tooling

CI workflow shape, local test/coverage gates, package-consumer fixture smoke coverage, shell-API baseline tooling, PowerShell/Bash parity, and developer-suite commands.

- **Godot export-template downloads are checksum-pinned** — the release
  template installer now refuses an archive that does not match the SHA-512
  entry in `samples/Origo.EvolutionDemo/export-templates.sha512`, before
  unpacking. The self-test serves a matching and a tampered archive, pins the
  success path, tamper rejection, and the unpinned-version error. Update the
  checksum with the Godot.NET.Sdk version.

- **Evolution Demo acceptance smoke runs in both Godot CI gates** — after the
  Godot integration tests, the Bash and PowerShell gates build the demo and run
  a short campaign-autoplay headless smoke with checkpoints and a final save in
  a temporary user-data home. The existing runner assertions now cover
  population/food caps, camera fit, save/load/reset, and checkpoint provenance
  in CI instead of only in a local developer run. A parity guard pins both
  wiring paths in `test-dev-suite.sh`.

- **Evolution Demo is part of the root build/format gate** — the Godot demo
  project is now listed in `Origo.sln`, so `scripts/bash/format.sh`,
  `scripts/bash/test.sh`, and the Windows solution gate compile and format it.
  Its three format diagnostics were fixed, and the packaging self-test now
  stubs the SDK bootstrap and checks both the demo and Logic assemblies.
  Regression coverage grew for V4 codec/validation, settings ranges, sensor
  port filtering, content rollback, diversity, analysis output directories,
  and report provenance.

- **Evolution Demo genome analysis exposes per-creature diet** — the save reader accepts the new additive
  `evo.creatureSoftMeals`/`toughMeals`/`toxicMeals` counters (older saves default to zero), feature extraction
  adds `behavior.*Meals` and `diet.*` shares/dominance/entropy, and the HTML report's cluster behavior profile
  and metric definitions include the diet features. The analysis suite covers the formulas and old-save
  fallback; a real 300 s save validates the reader/report path.

- **Evolution Demo genome analysis reports standard cluster statistics** — the
  genome report now uses the Rousseeuw silhouette (mean distance to the nearest
  other cluster, singleton rows score 0, documented deterministic systematic
  sample), a true bootstrap adjusted Rand index, Benjamini-Hochberg correction
  over the full tested signature set, DBSCAN noise kept as `-1` and excluded
  from the cluster count, silhouette, and enrichment, and effective organ/trait
  features (every `OrganKindV4` count zero-filled, every `LifeTraitKindV4`
  value after the runtime phenotype fallbacks). k-means reports the
  minimum-inertia solution over the documented restarts, auto-k keeps a
  documented minimum-cluster-size validity floor (`--min-cluster-size`) and
  reports every candidate instead of silently discarding it, and PCA reports
  explained-variance ratios. New `--metrics-json` and `--enrichment-csv`
  options expose the same numbers for automation and independent FDR auditing,
  `--allow-skips` makes undecodable-creature handling explicit (the default is
  to fail), and the timeline CSV adds tick, analyzed-row, and controller-counter
  columns. A regression test project covers the statistics, CLI failure modes,
  and feature semantics on generated v7 saves.

- **Evolution Demo analysis tool is covered by the root solution gates** —
  `samples/Origo.EvolutionDemo.Analysis`, its regression suite, and the demo
  Logic project it references are part of `Origo.sln` and the PowerShell
  test-project lists, so `scripts/bash/format.sh`, `scripts/bash/test.sh`, and
  the development-suite parity gate cover the tool. The v7 save reader now
  canonicalizes and deduplicates input paths, rejects a multi-scene directory
  unless `--pool` is passed, fails explicitly on a missing controller,
  `evo.genomeVersion`, `evo.time`, or `evo.tick` instead of defaulting, reports
  JSON parse errors with the file and line, counts malformed/unknown entities
  and reader-level creature skips, preserves boolean controller values, and
  warns when a save root lacks `.payload.sha` or contains a
  `.write_in_progress` marker. The timeline CSV keeps one row per save and
  carries numeric, string, and boolean controller values; the HTML report adds
  a latest-save controller-counter table; the analysis and samples READMEs
  document the v7 schema, CLI, and gate coverage.

- **Evolution Demo perf analysis separates systems from subphases** — phase ids
  with one dot are systems and two-dot ids nested under a system are subphases;
  `share` is the system mean divided by the sum of system means (nested markers
  are never added to the denominator), the subphase table renders with its parent,
  and an inconsistent phase key set now fails fast. GC columns are labelled as
  sampled-tick sums with the derived tick interval and an interval-scaled run-total
  estimate, percentiles use Hyndman-Fan type 7, multi-segment profiles are split on
  a `simTime` decrease and default to the last segment with an explicit note
  (`perf`/`compare` accept `--segment all|last|<index>`, and `--from/--to` compose
  inside the selection), and `compare` reports the symmetric union with `n/a` for
  one-sided and zero-baseline rows plus per-side metadata and warnings.

- **Evolution Demo analysis reader exposes controller counters and richer
  perf diffs** — `SaveSnapshot` now carries the save's numeric/string controller
  dictionaries (counters, seed, tick) for downstream tooling, and `perf compare`
  reports allocation and GC deltas plus every subphase's mean/p95/allocation
  instead of stopping at the system layer.

- **Evolution Demo analysis tool supports pinned clustering and timeline
  output** — `genome --k N` now pins exactly N clusters instead of searching up
  to N, `--timeline-csv` writes per-save feature means for time-series
  analysis, the cluster-by-save report table is ordered by simulation time, and
  `perf` accepts `--from`/`--to` simulation-time windows. Previously `--k`
  behaved as a maximum despite the documented fixed-count meaning.

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

- **Godot integration leak gate detects leaked instances without a pipe** —
  `scripts/bash/godot-test.sh` matched the leak warning by piping its output
  into `grep -q`; an early match could close the pipe and fail the `echo` with
  SIGPIPE under `set -o pipefail`, silently passing the gate. The check now
  uses a Bash string comparison, and the development-suite parity gate rejects
  `command | grep -q` pipelines in the runner.

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

- **Release artifact packagers resolve the caller's artifacts directory** —
  `package-framework-artifacts.sh` and the playable-demo `package-release.sh`
  passed their `artifacts` argument to archive commands that run from a
  temporary work directory, so the Release workflow's relative `./artifacts`
  path pointed inside that directory: the framework bundle step failed with
  `zip` exit 15 after packing, and the demo packaging step that runs later
  carried the same defect. Both scripts now resolve the path once before
  archiving, and the bundle manifest uses `sha256sum` or macOS `shasum` so the
  documented macOS local gate keeps working. New self-tests pin the relative
  workflow invocation, both bundle formats and their checksum manifests, the
  demo archive layout, a snapshot version, an absolute path, and the error
  paths, and the script map names the tar.gz bundle and the fifteen-asset
  validator.

- **Playable demo exports produce runnable packages** — the demo project had no
  solution file, so Godot's .NET export skipped publishing and every C# resource
  raised a missing-solution error, and the macOS universal preset failed because
  ETC2/ASTC import was disabled. The project now carries the solution file and
  the ETC2/ASTC import flag, and `package-release.sh` fails when a platform
  export does not contain the published demo assemblies or the macOS app
  executable instead of archiving a package that cannot start. The self-test
  pins the payload assertion for every platform and the macOS executable check.

- **Demo archive structure checks are pipefail-safe** —
  `samples/Origo.EvolutionDemo/package-release.sh` piped `tar`/`unzip`
  listings into `grep -q`; on a populated demo bundle grep exits at the first
  match and closes the pipe while tar is still writing, and `set -o pipefail`
  failed the packaging step with `tar: stdout: write error` even though the
  entry existed. The checks now consume the full listing, and the self-test
  rejects new `| grep -q` archive-listing pipelines.

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

- **Release artifact counts match the fifteen-asset contract** — `AGENTS.md`,
  the shell-kernel boundary page, and the `[Unreleased]` Changelog entry still
  described the older nine-asset set after the playable-demo and framework
  bundle additions; all three now state the current contract enforced by
  `validate-release-artifacts.sh`.

### Contributor Workflow and Repository Layout

Contributor-facing workflow, pull-request policy, and script/repository layout changes.

- **PR title/body lint for squash-merge commit messages** — the `pr-body-lint` workflow checks the PR title as a Conventional Commit subject and the PR body lines that GitHub uses for the squash commit, running the linter from `origin/main`; merge commits remain exempt from the local message lint.

- **BREAKING: repository scripts are grouped by implementation language** — POSIX scripts live under `scripts/bash/`, the Windows PowerShell suite under `scripts/powershell/`, and cross-platform Python helpers under `scripts/python/`; `scripts/README.md` is the gate and directory map. Entry points are now `bash scripts/bash/ci.sh` and `powershell -File scripts/powershell/ci.ps1`. Automation that invoked the old `scripts/*.sh`, `scripts/*.py` paths must update; no compatibility wrappers are provided.

- **Evolution Demo logic tests join the root test gates** — the pure
  `Origo.EvolutionDemo.Logic.Tests` suite is now listed in `Origo.sln` and both
  PowerShell test-project arrays, so the Bash/PowerShell test gates run the
  genome, mutation, phenotype, and demo mechanic regression tests and the
  development-suite parity check keeps the project lists aligned instead of
  leaving the suite as an ad-hoc local command.

---

[↑ Back to Origo Manual](README.en.md)
