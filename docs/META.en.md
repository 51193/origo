<!-- docsync-pair: META -->
<!-- docsync-revision: 15 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Documentation Maintenance Meta-Instructions

> [↑ Back to Origo Manual](README.en.md)

> **⚠️ Mandatory Development Loop: Every change must close the loop in order — ① Develop source → ② Extend/adapt tests → ③ Execute tests → ④ Fix source + re-test until all pass → ⑤ Changelog → ⑥ Docs sync. Before modifying source code, you must read the documentation of its upstream, downstream, and related facilities. Never misdiagnose cross-module collaborative design as defects. Full rules in [AGENTS.md](../AGENTS.md).**

## Documentation Positioning

`docs/` is the Origo framework's documentation mirror, maintained alongside the source code in the same repository. The goal is: **read the root → find the target directory → continue reading → recursively descend, without having to read source code from scratch.**

## Writing Principles

### Bottom-Up

1. **Leaf layer** (deepest directory): describe file list + feature overview + design decisions (why / why not)
2. **Intermediate layer** (with subdirectories): aggregate all sub-module capabilities, omit details, describe the module's overall external value
3. **Module root**: subsystem overview + module responsibilities + architectural constraints
4. **Project root**: top-level index, entry points for all sub-modules

### Link Conventions

- **Every README must contain a link to its parent (parent directory)**, format: `` `[↑ Back to Xxx](path)` ``
- **Every README must contain links to all sub-modules** (if it has subdirectories)
- **Horizontal associations are optional** (e.g., implementation ↔ abstraction), format: `` `[↔ Xxx](path)` ``
- **No orphan leaves**: the entire documentation tree is strictly connected through links

### Content Conventions

| Layer | Content |
|-------|---------|
| Leaf directory | File list + feature overview + design decisions (why / why not) |
| Intermediate directory | Sub-module capability summary + direct file descriptions for this layer |
| Module root | Subsystem overview + module architectural constraints |
| Top level | All module entry index + manual usage guide |

### Writing Style

- Every README begins with the parent link (↑) for the current layer
- Leaf-layer READMEs may repeat the parent link at the end (for easy back navigation)
- Tables clearly list file responsibilities and interface members
- Design decisions use "why" and "why not" bullet-point exposition
- **Uncertain design decisions must be escalated to the maintainer; do not fabricate**
- **No evolution markers**: documentation is a snapshot of the current state. Do not use markers such as "new", "legacy", "deprecated", "since v0.x" that track the version evolution history of code/interfaces. Any description of an interface/method/decision should directly state its current responsibilities and rationale, without implying whether it "previously did not exist" or "may be removed in the future."

### Bilingual Documentation Mechanism (DocSyncTool)

`docs/` organizes multilingual documentation using **co-located language suffixes**. Every directory contains:

| File | Purpose |
|------|---------|
| `README.md` | **Auto-generated** navigation hub (lists all `.zh.md` / `.en.md` files). **Do not edit manually.** |
| `README.zh.md` | Chinese content |
| `README.en.md` | English content |

Two files with the same base name but different language suffixes form a **sync pair**. Sync status is tracked through metadata headers at the top of each content file:

```markdown
<!-- docsync-pair: Origo.Core/Snd/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
```

| Field | Meaning |
|-------|---------|
| `docsync-pair` | Globally unique pair identifier (file path minus language suffix). Automatically derived; must be identical across languages. |
| `docsync-revision` | Monotonic integer, **computed by DocSyncTool from git history**. **Two files of a pair are in sync when their revisions are equal.** Do not edit it by hand. |

**Revision rules** (computed automatically by `generate`, verified by `validate`):

| Git change | Computed revision |
|-----------|-------------------|
| New file / new pair | Starts at `1`. A new translation added to an existing pair catches up to the peer revision. |
| One language of a pair changed | The leading side advances by one generation; a change to the stale side catches up to the peer (translation catch-up). |
| Both languages changed in the same commit | Both advance together by one generation. |
| Metadata-only / pure-rename commits | No revision change. |
| Multiple content commits in one push | Every content commit is counted; the final CI checkout does not collapse them. |

The planner hashes each file with the DocSync metadata block removed, finds
the last generated content state in that file's git history, and replays the
content-changing commits since then. CI fetches the complete history
(`fetch-depth: 0`) because GitHub only runs CI for the final commit of a
multi-commit push.

**After any doc content change**, you must run:

```bash
dotnet run --project tools/DocSyncTool -- generate
```

This rewrites the revision headers and produces two kinds of derived files
(commit them together):

1. **`README.md`** navigation hubs in every directory — auto-generated index listing all docs by language
2. **`docs/.sync-status.json`** — machine-readable snapshot of every pair's revision state, including content hashes used as the idempotent planning anchor

**DocSyncTool cheat-sheet** (run from repo root):

| Command | What it does |
|---------|-------------|
| `dotnet run --project tools/DocSyncTool -- generate` | Auto-compute `docsync-revision` from git history, regenerate all `README.md` nav hubs + `.sync-status.json`. Idempotent and always succeeds. |
| `dotnet run --project tools/DocSyncTool -- validate` | Read-only check: matching and monotonic pair revisions (floored by the previous revisions recorded by `generate`), same-language links, existing file/directory/anchor targets, and complete reference-style link definitions. Exit code 1 on failure. |
| `dotnet run --project tools/DocSyncTool -- init` | **One-time migration** — rename `.md` → `.zh.md`, inject metadata, update links. Already executed; do not re-run. |

**Link discipline** (enforced as ERROR by `validate`):

- Chinese docs (`.zh.md`) link only to `.zh.md` targets
- English docs (`.en.md`) link only to `.en.md` targets
- **Cross-language links are forbidden**
- Bare `.md` links without language suffix are forbidden (after migration)

**Configured languages** are defined in `tools/DocSyncTool/docsync-config.json`:

```json
{ "languages": ["zh", "en"], "docs_root": "docs" }
```

**CI enforcement**: `scripts/doc-sync.sh` (called by `scripts/ci.sh`) runs `generate` then `validate`. On `push` to main, CI auto-commits stale generated files; on `pull_request`, stale generated files cause failure with instructions to run `generate` locally. Validation failure always blocks the build.

## Sync Rules

### Situations Requiring Sync Update

1. **Add/delete/rename source code directory** → mirror the same operation in `docs/`
2. **Add public interface/method** → update the interface list in the corresponding leaf README
3. **Design decision change** → update the design decisions section
4. **New config key/command** → update relevant README and usage docs
5. **Inter-module dependency change** → update module README links
6. **AGENTS.md meta-instruction changes** → synchronize references to new rules in this document (e.g., AGENTS.md §1.7 comment language requirements and the vendored-source exemption, §1.8 git history awareness, §1.9 dependency update grouping — version-coupled package families must be bumped together, never independently, §1.10 environment bootstrap — install the SDK requested by global.json and never downgrade the request to match the machine, §3 red-first rule — bug fixes require a red regression test that reproduces the bug through a real reachable path, and the file's git history must be consulted before fixing or extending it)

### Situations NOT Requiring Sync

- Pure internal implementation detail changes (not affecting public API or design intent)
- Code refactoring (not changing module responsibilities or interfaces)
- Performance optimizations (not changing external behavioral semantics)

### Sync Checklist

After a code PR is merged, check:
- [ ] Is the directory structure mirrored (add/delete/rename)?
- [ ] Are leaf README interface/file lists accurate?
- [ ] Are intermediate README sub-module indexes complete?
- [ ] Are all links valid (no 404)?
- [ ] Does the design decisions section reflect current design intent?

## Git Commit Message Format

All commits must follow the Conventional Commits specification to keep repository history readable and machine-parseable. PR commit messages are enforced by `scripts/lint-commits.sh` and `.github/workflows/commit-lint.yml`: type, 72-character subject limit, no trailing period, and body lines no longer than 72 characters.

### Basic Format

```
type: short description

Detailed paragraphs explaining **what** was changed and **why**, not
implementation details (the code diff already shows "how").

Multi-line body: each line no more than 72 characters, blank lines
between paragraphs. Use group headers when the change involves
multiple sub-projects.
```

### Types

| Type | Usage |
|------|-------|
| `feat` | New feature (user-facing or for downstream library consumers) |
| `fix` | Bug fix |
| `refactor` | Code restructuring that does not change external behavior |
| `perf` | Performance optimization |
| `docs` | Documentation-only changes |
| `test` | Test-only additions or modifications |
| `chore` | Build, dependencies, version bumps, and other maintenance changes |
| `build` | Build-system or external dependency changes |
| `ci` | CI configuration or CI script changes |
| `style` | Formatting/style changes that do not affect code meaning |
| `revert` | Revert a previous commit |

### Short Description Rules

- Use English imperative mood (e.g., `add`, `fix`, `remove`, `extract`), start with lowercase
- One line only, no more than 72 characters
- No trailing period
- Describe external behavior, not internal details

### Body Rules (required when multi-paragraph, optional for single-line fixes)

- Explain **why** the change was made (e.g., design flaw, tech debt, new requirement)
- Explain **impact on users** (API changes, behavioral changes, breaking changes)
- Breaking changes must be preceded by a `BREAKING CHANGE:` prefixed paragraph at the end of the body
- Associated issue or PR numbers go on the last line (`Closes #xxx` / `Refs #xxx`)

### Examples

```
feat: add Vector3 support to TypedData inline storage

Register Vector3, Vector3I, and Vector4 as GodotAdapter inline types
with startKind=128. The TypedData source generator now emits TryGetXxx
and AsXxx extension methods for all registered adapter types.

Closes #42
```

```
refactor: extract SaveCoordinator from ProgressRun nested class

SaveCoordinator held references to ProgressRun internals via _owner,
preventing isolated testing. Extracting it with explicit constructor
injection makes save orchestration independently testable and clarifies
the ProgressRun persistence boundary.

BREAKING CHANGE: SaveCoordinator constructor now requires IStateMachineContainer
instead of accessing ProgressScope.StateMachines through the owner reference.
```

```
fix: prevent partial session state after failed load recovery

ResetAfterLoadFailure used a single try-catch that swallowed all
exceptions, leaving the session in an inconsistent state. Split into
per-step try-finally blocks with aggregate rethrow to ensure each
cleanup step executes independently and failures are surfaced.
```

```
chore: bump Origo to 0.0.7-nightly.20260608
```

### Forbidden Practices

- ❌ Commit messages without a type prefix
- ❌ Empty commit messages
- ❌ Messages with no informational value such as `update`, `fix bug`, `wip`
- ❌ Writing implementation details in the commit message ("changed to use class X", "changed parameter from A to B") — those are in the diff
- ❌ Describing plans or intentions outside the scope of this commit
- ❌ Using internal codenames or priority markers (e.g., `P0`, `P1`, `Phase 1`, etc.) — commit messages are intended for readers without prior context and should directly describe the change content, not internal development classifications
- ❌ Preserving intermediate development commit messages during squash merge (rewrite a feature-oriented message instead)

## Directory Structure Conventions

```
docs/                            # Documentation root (inside the origo repository)
├── README.md                    # Auto-generated: bilingual navigation hub
├── README.zh.md                 # Chinese top-level index (hand-authored)
├── META.zh.md                   # This file (maintenance meta-instructions)
├── .sync-status.json            # Auto-generated: sync status for all pairs
├── usage/                       # System usage documentation
│   ├── README.md               # Auto-generated: navigation hub
│   ├── README.zh.md            # Usage doc index (hand-authored)
│   └── *.zh.md                 # Docs organized by usage scenario (hand-authored)
├── benchmarks/                  # Performance baselines (TypedData current snapshot)
│   ├── README.md               # Auto-generated
│   ├── README.zh.md            # Hand-authored
│   └── baseline.zh.md
├── Origo.Core/                  # Mirrors the repo root Origo.Core/ directory structure
│   ├── README.md               # Auto-generated: navigation hub
│   ├── README.zh.md            # Module root doc (hand-authored)
│   └── subdirectories/          # Each subdirectory: README.md (auto) + README.zh.md (hand-authored)
├── Origo.Core.Tests/            # Mirrors the repo root Origo.Core.Tests/
├── Origo.GodotAdapter/          # Mirrors the repo root Origo.GodotAdapter/
├── Origo.GodotAdapter.Tests/    # Mirrors the repo root Origo.GodotAdapter.Tests/
├── Origo.ConsoleBridge/         # Mirrors the repo root Origo.ConsoleBridge/
├── Origo.ConsoleBridge.Tests/   # Mirrors the repo root Origo.ConsoleBridge.Tests/
├── Origo.SourceGeneration/      # Mirrors the repo root Origo.SourceGeneration/
└── Origo.SourceGeneration.Tests/ # Mirrors the repo root Origo.SourceGeneration.Tests/
```

> Top-level entry point [AGENTS.md](../AGENTS.md) lives at the repo root, is auto-injected into every session, and links to this file.
>
> Every `.zh.md` content file has a corresponding `.en.md` file alongside it, and the `README.md` navigation hub automatically lists entries for both languages.

## Environment Bootstrap

Before running any `dotnet` command, configure the environment to match the repository instead of editing `global.json` to match the machine (full rules in [AGENTS.md §1.10](../AGENTS.md#110-environment-bootstrap-install-the-required-sdk-never-downgrade-the-request)):

1. `global.json` is the single authority for the required .NET SDK feature band; never downgrade the request.
2. Run `bash scripts/install-dotnet.sh`, which installs the exact version with the official `dotnet-install.sh`.
3. Prefer the default install root (`$HOME/.dotnet`) so the login shell resolves plain `dotnet` without per-session `PATH` or `DOTNET_ROOT` exports.
4. When the default root is read-only, the script falls back to the repository-local `.dotnet/`; use the `./dotnet` wrapper from the repo root. Repository scripts source `scripts/dotnet-env.sh` to select the local or system SDK automatically.
5. The Godot engine binary is downloaded by `scripts/download-godot.sh` according to the `Godot.NET.Sdk` version in `Origo.GodotAdapter.csproj` and cached under `.godot_binary/`.

## Manual Version

Documentation is synchronized with the `<Version>` in the repository's `Directory.Build.props` — since docs and source code are co-located in the same repo, versioning is naturally consistent.

## Generation

The **content files** (`.zh.md` / `.en.md`) of this manual are hand-written after analyzing source code. The **navigation hubs** (`README.md`) and **sync status file** (`.sync-status.json`) are auto-generated by `DocSyncTool generate` and must not be edited manually. Quality depends on correct understanding of the source code and the maintainer's design knowledge. If discrepancies are found, report to the manual maintainer.

---
[↑ Back to Origo Manual](README.en.md)
