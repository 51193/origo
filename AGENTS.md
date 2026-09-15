# Origo Agent — Mandatory Workflow & Development Rules

> **Language**: reply in the user's current language; ask once only if it is
> ambiguous. This file is the single authoritative entry point, injected every
> session. Gates stay inline; procedure lives in linked docs; on conflict this
> file wins and the other must be corrected in the same change.
>
> Must-read: this file, `docs/META.*`, `docs/release-process.*`, module docs.

## 0. Pre-work Gate (Mandatory Reading)

Before reading or modifying source, read in full:

1. **This file**.
2. **`docs/META.zh.md`** (or `.en.md`) — writing, commits, DocSync, buffer.
3. **`docs/release-process.zh.md`** (or `.en.md`) — release and Changelog rules.
4. **Module docs** for the change plus upstream/downstream/related facilities
   (§1.3): the `docs/<mirror-path>/` language pair (usually `README.*`).
5. **`_origo_local/README.md` when present** — single-book buffer; read the
   root README (live status/index) and every numbered chapter relevant to the
   task. Full protocol: `docs/META.zh.md` / `.en.md` §Local Agent Work Buffer.

`docs/` mirrors source in `.zh.md`/`.en.md` pairs. Some directories contain
non-README language pairs; navigation-only directories contain only the
generated `README.md` hub. Never edit generated hubs or `docs/.sync-status.json`.
Read docs first, then follow the chain into source; README rationale is required context.

## 1. Core Principles

### 1.1 Fail-fast

- Throw when an interface contract is violated. Silent degradation and fallback are forbidden.
- Save/load must strictly validate integrity; fail explicitly instead of accepting a half-initialized state.
- Do not swallow exceptions, stuff defaults, or add defensive fallbacks just to get something running.

### 1.2 Early Development — No Backward-Compatibility Burden

This project is in early development and does not promise API stability.

- **Forbidden**: compatibility shims, deprecation layers, dual-track APIs, migration shells, evolution traces, dead code, and markers such as `since v0.x`, `legacy`, or `new`.
- Make a clean breaking change and bring code plus docs to the current correct state. Record breaking changes in `CHANGELOG.md` under `[Unreleased]` per §4, prefixed with `BREAKING:`.
- **Forbidden**: public/internal properties or methods added solely for test convenience. Production code is written as if tests do not exist; tests use `InternalsVisibleTo`, reflection, or `TestFactory`-style infrastructure.

### 1.3 Full-Chain Understanding — Eliminating False-Positive Tech Debt Fixes

> Most frequently violated and most costly.

Before modifying or extending source, read the docs and code of its
**upstream, downstream, and related facilities** to understand the
collaboration contracts.

- A module can be correct only with its collaborators; apparent redundancy, under-exposure, or duplication is often deliberate cross-module design.
- Do not treat an apparent design defect as tech debt before understanding the chain; such false-positive fixes introduce real bugs.
- Confirm suspected defects in the module README's design decisions or git history (§1.8). If unclear, ask the maintainer — do not change on speculation.
- Do not record cross-module co-designed constructs as `Fixed` in the Changelog.

### 1.4 Single Access Path — Eliminating Backdoors

> Every capability must have exactly one external access path. Backdoors breed bugs.

- A capability exposed by a dedicated interface (e.g. `ISessionRun.RequestKillEntity`) must have every alternative path sealed: make objects/methods `internal` and expose other capabilities through their own interfaces.
- **Forbidden**: hand-stitching low-level operations to simulate an interface. The orchestrated side effects (validation, hooks, state transitions, resource lifecycle) are skipped, and failures become extremely hard to diagnose.
- If a backdoor comes from an object that should not have the capability, treat it as a possible design defect and **ask the maintainer**. Do not auto-scan-and-fix deliberate cross-module collaboration.

### 1.5 Consistent Code Format — Local Equals CI

- All C# must pass `dotnet format --verify-no-changes --severity info`; `scripts/format.sh` runs analyzer dead-code checks. Format is the first gate on C# code.
- `.editorconfig` defines whitespace, `var`, and analyzer severities. Private-field `_camelCase` naming is enforced by architecture tests in each test project.
- Test projects use flat namespaces (`Origo.Core.Tests`, not nested); IDE0130 is suppressed for test paths. See `docs/Origo.Core.Tests/META-TEST.*`.
- Do not disable `.editorconfig` rules to bypass the gate.

### 1.6 Bilingual Documentation — Co-located Side-by-Side

`docs/` mirrors source in `.zh.md`/`.en.md` pairs. Full DocSync mechanics are
in `docs/META.zh.md` / `.en.md` §Bilingual Documentation Mechanism.

- Never edit `docsync-revision`, generated `README.md` hubs, or `docs/.sync-status.json`.
- After any docs content change run `dotnet run --project tools/DocSyncTool -- generate`, then commit all rewritten docs, hubs, and `.sync-status.json`.
- Links inside the docs mirror stay in the same language; cross-language and bare `.md` links inside the mirror are forbidden. Links to root files outside the mirror (e.g. `../AGENTS.md`) are allowed.
- CI runs `generate` + `validate`; local `scripts/doc-sync.sh` mirrors it. Validate checks pairs/revisions, same-language links, target/anchor/reference existence, and source-mirror file lists (§5).

### 1.7 Source Code Comments — English Only, IntelliSense-Ready

- Every `public`/`protected` type/member has an English `<summary>`; `<inheritdoc />` is accepted for implementations/overrides whose base declaration carries the contract.
- `internal` classes implementing public interfaces should have English comments for role and non-trivial contracts (constructor preconditions, disposal, thread safety).
- Chinese XML comments on public API are defects and must be translated; non-public comments should trend English. Tests and vendored upstream source (currently `Origo.Core/Addons/FastNoiseLite/FastNoiseLite.cs`, rationale in its README) are exempt.
- DocFX/Sandcastle-style API generation is forbidden; `docs/` is the reference.

### 1.8 Git History Awareness — File History Informs Changes

- Before fixing/extending a file read its history: `git log --follow -p <file>` plus upstream/downstream collaborators. The current shape is the outcome of prior decisions and fixes (§1.3).
- Churn is a warning: do not re-fix back-and-forth regions on the same reasoning; confirm intent with the maintainer and pin any fix with a regression test.
- History is for understanding only; §1.2 still forbids evolution markers in code/docs.
- Review the final diff before committing; keep it minimal and intentional.

### 1.9 Dependency Updates — Version-Coupled Packages Move as One

- Dependabot PRs must pass as proposed. Version-coupled families are grouped in `.github/dependabot.yml`; never bump one member alone. Groups/ignores/rationale live there.
- `xunit.v3` / `xunit.v3.extensibility.core` move together; major updates wait for the coordinated xUnit v4 migration (packages, tests, `scripts/test.sh`).
- `Microsoft.CodeAnalysis.*` is coupled to `global.json`'s Roslyn compiler; bump it with the matching SDK update.
- Apply the same rule to future version-coupled families.

### 1.10 Environment Bootstrap — Install the Required SDK, Never Downgrade the Request

- `global.json` is authoritative; never downgrade or edit it to match an already-installed SDK.
- Run `bash scripts/install-dotnet.sh` to install the exact SDK (default `$HOME/.dotnet`, repository-local `.dotnet/` fallback). Scripts source `scripts/dotnet-env.sh`; use `./dotnet` only in local mode. Do not substitute per-session `PATH`/`DOTNET_ROOT`/`NUGET_PACKAGES` exports.
- Godot binaries are separate: `scripts/download-godot.sh` reads `Godot.NET.Sdk` from `Origo.GodotAdapter/Origo.GodotAdapter.csproj` and caches under `.godot_binary/`. Full rules: `docs/META.zh.md` / `.en.md` §Environment Bootstrap.

### 1.11 Local Agent Work Buffer — `_origo_local/` (Untracked, Producer/Consumer)

> If a scan/review/design session finds work it cannot finish, or nears its context limit, write the finding and full context into `_origo_local/` before ending.

`_origo_local/` is git-ignored and must never be committed or staged. It uses
**single-book mode**: root `README.md` is the live index/status holder; numbered
chapters belong to one work item. Full tracked protocol:
`docs/META.zh.md` / `.en.md` §Local Agent Work Buffer.

- **Producer**: create/update the book's root status (`inbox`/`in-progress`/`blocked`/`done`/`superseded`), owner/date/baseline commit, evidence, full §1.3 chain context, scope/acceptance criteria, commands run.
- **Consumer**: claim only after re-reading and revalidating context; set the root `in-progress`. Never delete or rewrite another agent's `in-progress` work.
- Close only after the §2 loop; record the commit hash and durable docs location. If blocked, leave an exact handoff: worktree state, changed files, commands run, failures, next action.
- Never store secrets. Stale content is marked `superseded` with a replacement pointer, not deleted for cleanliness. Durable conclusions must be migrated into tracked docs, tests, or `CHANGELOG.md`.

## 2. Development Loop (Mandatory Order)

> Every change closes these steps in order; do not rearrange or skip.

1. **Develop source** — satisfy §0/§1; read target history and collaborators first (§1.8/§1.3).
2. **Extend/adapt tests** — behavior tests for new public API; red-first real-path regression for bug fixes (§3); sync behavior-change tests.
3. **Iterate with tests** — run `bash scripts/test.sh` (restore → build → test + coverage); fix and re-test until green.
4. **Changelog alignment** — update `CHANGELOG.md` `[Unreleased]` per §4.
5. **Docs sync** — update `docs/` per §5; after any content change run `dotnet run --project tools/DocSyncTool -- generate`.
6. **Commit** — source, tests, Changelog, docs content, generated hubs, and `.sync-status.json` together.
7. **Post-commit full CI** — run `bash scripts/ci.sh` (lint-scripts → format → doc-sync → test → benchmark → Godot; verifies generated docs are committed). Fix/amend, then rerun.
8. **Post-commit message lint** — run `bash scripts/lint-commits.sh`; a pre-commit run cannot inspect the new commit.

State any inapplicable step in the commit message. **Partial
completion is forbidden.** If closing an `_origo_local` book, apply §1.11
after steps 1–8.

- `lint-commits.sh` prefers `origin/main`; when unavailable it falls back to `HEAD~1` and reports it. Explicit ranges: `bash scripts/lint-commits.sh <base> <head>`.
- `ci.sh` is single-platform; CI adds OS matrix + `commit-lint`.

## 3. Test Requirements

| Change type | Test requirement |
|-------------|------------------|
| New public API | Behavior tests. |
| Bug fix | Regression test (red → green). |
| Behavior change | Update existing tests. |
| Refactoring | Existing tests pass; no new tests required. |

**Red-first, real-path fixes**: write the regression test through a real,
reachable user/business path (real hosts, strategies, save payloads, deferred
queue, or a faithful same-contract stand-in); confirm it fails on unmodified
code for the bug's own symptom; fix the source and confirm the same test passes
unchanged; check sibling paths. A test that passes through a different code
path is a blind spot, not a regression test. Details:
`docs/Origo.Core.Tests/META-TEST.zh.md` / `.en.md`.

- Test projects: `Origo.Core.Tests`, `Origo.GodotAdapter.Tests`, `Origo.ConsoleBridge.Tests`, `Origo.SourceGeneration.Tests`, `Origo.GodotAdapter.Integration.Tests` (Godot headless through `scripts/godot-test.sh`), and `tools/DocSyncTool.Tests`; `Origo.TestSupport` is a support library.
- Coverlet enforces ≥90% line coverage for measured xUnit projects in `scripts/test.sh`; the Godot integration runner is separate and outside that gate.
- Use `test.sh` during iteration; run `ci.sh` after commit per §2.

## 4. Changelog Conventions

Full rules (categories, baselines, snapshots, writing) are in
`docs/release-process.zh.md` / `.en.md`.

- Record user-facing significant changes under `[Unreleased]` in `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, or `Security`.
- Breaking changes use no separate category: classify under `Changed` / `Removed` and prefix with `BREAKING:`.
- Baseline = last formal release tag; nightly/alpha/preview are not versions/baselines. Do not record intra-version churn; record only the final state.
- §1.3 applies: do not mis-record deliberate cross-module designs as `Fixed`.

## 5. Docs Sync Rules

`docs/` mirrors source. Sync is required for:

| Source change | Docs action |
|---------------|-------------|
| Directory add/delete/rename | Mirror under `docs/`. |
| Any `.cs` add/rename/delete under `SourceMirrorRoots` | Update the mirror README file list in both languages. |
| Public interface/method add/delete/rename | Update the leaf README interface list. |
| Design decision change | Update the design-decisions section. |
| Config key/command change | Update the relevant README / `docs/usage/`. |
| Inter-module dependency change | Update module README links. |
| Test capability/method change | Update the corresponding test capability docs. |

"Internal implementation only" removes design/interface prose updates, **not**
mirror README file-list updates: `DocSyncTool validate` requires every `.cs` file
in each mirrored source directory to be listed in that directory's bilingual READMEs.

- After any docs change run `DocSyncTool generate` and commit generated files (§1.6).
- Checklist: directory mirrored, file/interface lists accurate, intermediate indexes complete, links valid, design decisions current, usage/test docs cover new scenarios.
- `validate` checks pairs/revisions, same-language links, targets/anchors/references, source-mirror directories/file lists, and heading parity warnings.
- Full rules: `docs/META.zh.md` / `.en.md` §Sync Rules.

## 6. Release Process

Full details: `docs/release-process.zh.md` / `.en.md`. Pre-tag summary:

1. Choose formal `x.y.z`; move `[Unreleased]` to `## [x.y.z] - YYYY-MM-DD` and clear it.
2. Set `<Version>` in `Directory.Build.props` to `x.y.z` (tag is `vx.y.z`).
3. Update the version text in `docs/README.zh.md` / `.en.md`.
4. Move analyzer rules to `AnalyzerReleases.Shipped.md` with `## Release x.y.z`; leave no unshipped rules in `AnalyzerReleases.Unshipped.md`.
5. Run `TAG_VERSION=x.y.z bash scripts/verify-release.sh` (Changelog block, empty `[Unreleased]`, analyzer tracking, docs version stamps).
6. Run `DocSyncTool generate` and commit all release changes together: metadata, docs content, generated hubs, and `.sync-status.json`.
7. Run `bash scripts/ci.sh` and `bash scripts/lint-commits.sh` on that commit; amend and rerun if either fails.
8. Tag `vx.y.z` on the verified commit and push it. Release workflow runs tests, packs the three libraries, and attaches packages plus the docs snapshot to GitHub Release; packages are not pushed to nuget.org.

## 7. Document Index

> Authoritative entry points only; detailed module indexes live in the docs hubs.

| Entry | Path |
|-------|------|
| Manual index | [`README.zh.md`](docs/README.zh.md) / [`README.en.md`](docs/README.en.md) |
| Generated docs hub | [`README.md`](docs/README.md) |
| Docs rules | [`META.zh.md`](docs/META.zh.md) / [`META.en.md`](docs/META.en.md) |
| Release rules | [`release-process.zh.md`](docs/release-process.zh.md) / [`release-process.en.md`](docs/release-process.en.md) |
| Test-doc rules | [`META-TEST.zh.md`](docs/Origo.Core.Tests/META-TEST.zh.md) / [`.en.md`](docs/Origo.Core.Tests/META-TEST.en.md) |
| Module manuals / usage | `docs/<mirror-root>/README.zh.md` / `.en.md`; [`usage/README.zh.md`](docs/usage/README.zh.md) / [`.en.md`](docs/usage/README.en.md) |
| Changelog | [`CHANGELOG.md`](CHANGELOG.md) |
| Buffer / tooling / CI | `_origo_local/README.md`; [`.editorconfig`](.editorconfig); [`.github/dependabot.yml`](.github/dependabot.yml); [`.github/workflows/`](.github/workflows/); [`tools/DocSyncTool/`](tools/DocSyncTool/); [`scripts/ci.sh`](scripts/ci.sh) |
