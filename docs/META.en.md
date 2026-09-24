<!-- docsync-pair: META -->
<!-- docsync-revision: 26 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Documentation Maintenance Meta-Instructions

> [↑ Back to Origo Manual](README.en.md)

> **⚠️ Mandatory Development Loop: Every change must close the loop in order — ① Develop source → ② Extend/adapt tests → ③ Execute tests → ④ Fix source + re-test until all pass → ⑤ Changelog → ⑥ Docs sync → ⑦ Commit → ⑧ post-commit `scripts/ci.sh` → ⑨ post-commit `scripts/lint-commits.sh`. Before modifying source code, you must read the documentation of its upstream, downstream, and related facilities. Never misdiagnose cross-module collaborative design as defects. Full rules in [AGENTS.md](../AGENTS.md).**

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

`docs/` organizes multilingual documentation as **same-basename `.zh.md`/`.en.md` pairs**. The common case is a `README` pair; other basenames (for example `Integration.*`, `pipeline.*`) are valid too. Navigation-only directories contain only the generated `README.md` hub. The `docs/agents/issue-tracker.md`, `triage-labels.md`, and `domain.md` files are tool configuration at fixed paths for Matt Pocock's skills, outside the manual's content pairs. The directory's `README.zh.md`/`README.en.md` remains governed by this mechanism. When configuration changes, synchronize that README pair so the manual entry stays accurate.

| File | Purpose |
|------|---------|
| `README.md` | **Auto-generated** navigation hub (lists all language pairs and subdirectories). **Do not edit manually.** |
| `<name>.zh.md` | Chinese content file; `<name>` is commonly `README` |
| `<name>.en.md` | English content file; `<name>` is commonly `README` |

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
| `dotnet run --project tools/DocSyncTool -- validate` | Read-only check: matching and monotonic pair revisions (floored by the previous revisions recorded by `generate`), same-language links inside the mirror, no cross-language/bare `.md` links, existing file/directory/anchor targets, complete reference-style definitions, and every `.cs` file in each mirrored source directory listed in that directory's bilingual READMEs. Heading-structure differences are warning-only. Exit code 1 on failure. |

**Link discipline** (enforced as ERROR by `validate` inside the docs mirror):

- Chinese docs (`.zh.md`) link only to `.zh.md` targets
- English docs (`.en.md`) link only to `.en.md` targets
- **Cross-language links are forbidden**
- Bare `.md` links without a language suffix are forbidden inside the mirror; links that leave the mirror for root files (for example `../AGENTS.md` or `../CHANGELOG.md`) are allowed

The tool configuration (languages, docs root, source-mirror roots, and source→doc overrides) lives in `tools/DocSyncTool/docsync-config.json`:

```json
{
  "Languages": ["zh", "en"],
  "DocsRoot": "docs",
  "SourceMirrorRoots": [
    "Origo.Core",
    "Origo.GodotAdapter",
    "Origo.ConsoleBridge",
    "Origo.SourceGeneration",
    "Origo.TestSupport"
  ],
  "SourceDocOverrides": {
    "Origo.TestSupport/Metadata": "docs/Origo.TestSupport/Architecture",
    "Origo.TestSupport/Runtime": "docs/Origo.TestSupport/Architecture"
  }
}
```

**CI enforcement**: `scripts/doc-sync.sh` (called by `scripts/ci.sh`) runs `generate` then `validate`. On `push` to main, CI auto-commits stale generated files; on `pull_request`, stale generated files cause failure with instructions to run `generate` locally. Validation failure always blocks the build.

## Sync Rules

### Situations Requiring Sync Update

1. **Add/delete/rename source code directory** → mirror the same operation in `docs/`
2. **Add/rename/delete any `.cs` file under `SourceMirrorRoots`** → update that directory's mirror README file list in both languages (internal files included; `validate` enforces this). Test-project/tool `.cs` changes follow item 7 instead.
3. **Add public interface/method** → update the interface list in the corresponding leaf README
4. **Design decision change** → update the design decisions section
5. **New config key/command** → update relevant README and usage docs
6. **Inter-module dependency change** → update module README links
7. **Test capability/method change** → update the corresponding `docs/Origo.*.Tests/` capability docs
8. **Release or Changelog rule change** → update [release-process.en.md](release-process.en.md) (Chinese peer: `release-process.zh.md`)
9. **AGENTS.md meta-instruction changes** → [AGENTS.md](../AGENTS.md) is authoritative on conflict; synchronize the affected sections of this document in the same change. Do not hard-code AGENTS section numbers; when a rule is owned by this document, keep the full rule here rather than a summary that can go stale.

### Situations NOT Requiring Sync

- Pure internal implementation detail changes (not affecting public API or design intent) — no design prose update, but item 2 still requires the mirror README file list
- Code refactoring (not changing module responsibilities or interfaces) — file additions/renames/deletions still follow item 2
- Performance optimizations (not changing external behavioral semantics) — file structure changes still follow item 2

### Sync Checklist

After a code PR is merged, check:
- [ ] Is the directory structure mirrored (add/delete/rename)?
- [ ] Are leaf README interface/file lists accurate?
- [ ] Are intermediate README sub-module indexes complete?
- [ ] Are all links valid (no 404)?
- [ ] Does the design decisions section reflect current design intent?
- [ ] Do `docs/usage/` and test capability docs cover new scenarios/capabilities?
- [ ] Are all added/renamed/deleted `.cs` files listed in both mirror READMEs (internal files included)?
- [ ] If release or Changelog rules changed, were `release-process.zh/en.md` updated?

## Git Commit Message Format

All commits must follow the Conventional Commits specification to keep repository history readable and machine-parseable. PR commit messages are enforced by `scripts/lint-commits.sh` and `.github/workflows/commit-lint.yml`: type, 72-character subject limit, no trailing period, and body lines no longer than 72 characters. Dependabot-authored commits are the only exemption: Dependabot can configure a commit-message prefix but does not support custom message templates, and its generated body lines exceed 72 characters. `.github/dependabot.yml` sets the `chore(deps)` prefix for every ecosystem so generated subjects remain Conventional Commits, and `scripts/lint-commits.sh` skips Dependabot-authored commits; human-authored commits in the same PR remain fully checked.

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
├── README.zh.md / README.en.md  # Top-level indexes (hand-authored, bilingual pair)
├── META.zh.md / META.en.md      # These maintenance meta-instructions (bilingual pair)
├── release-process.zh/.en.md    # Formal releases, weekly snapshots, Changelog rules (bilingual pair)
├── .sync-status.json            # Auto-generated: sync status for all pairs
├── usage/                       # System usage documentation (zh/en pairs)
├── architecture/                # Architecture overview, decisions, and deferred designs (bilingual pairs)
├── agents/                      # Matt Pocock skills configuration + bilingual manual entry
├── benchmarks/                  # Performance baselines (zh/en pairs + baseline.json)
├── Origo.Core/                  # Mirrors the repo root Origo.Core/ directory structure
├── Origo.Core.Tests/            # Test capability docs (grouped by capability, zh/en pairs)
├── Origo.GodotAdapter/          # Mirrors the repo root Origo.GodotAdapter/
├── Origo.GodotAdapter.Tests/    # GodotAdapter test capability docs
├── Origo.GodotAdapter.Integration.Tests/ # Godot headless integration test docs
├── Origo.ConsoleBridge/         # Mirrors the repo root Origo.ConsoleBridge/
├── Origo.ConsoleBridge.Tests/   # ConsoleBridge test capability docs
├── Origo.SourceGeneration/      # Mirrors the repo root Origo.SourceGeneration/
├── Origo.SourceGeneration.Tests/ # Source generator test capability docs
├── Origo.TestSupport/           # Test support library docs
└── tools/                       # Repository tool test docs (DocSyncTool.Tests)
```

Every hand-authored manual content file has paired `.zh.md` / `.en.md` versions; the three `docs/agents/` tool configuration files follow the exception above. Every directory `README.md` navigation hub is auto-generated by `generate`. Navigation-only directories have no language-suffixed pair files. Architecture overviews, decision records, and deferred designs live in `docs/architecture/`.

> Top-level entry point [AGENTS.md](../AGENTS.md) lives at the repo root, is auto-injected into every session, and links to this file.
>
> Every `.zh.md` content file has a corresponding `.en.md` file alongside it, and the `README.md` navigation hub automatically lists entries for both languages.

## Environment Bootstrap

Before running any `dotnet` command, configure the environment to match the
repository instead of editing `global.json` to match the machine:

1. `global.json` is the single authority for the required .NET SDK feature band.
   Never downgrade the request or edit `global.json` to match an
   already-installed SDK.
2. Run `bash scripts/install-dotnet.sh`. It parses `global.json` and installs the
   exact SDK through the official `dotnet-install.sh`, preferring the default
   install root (`$HOME/.dotnet`) so a normal login shell resolves plain
   `dotnet` without per-session exports.
3. When the default install root is read-only, the script falls back to the
   repository-local `.dotnet/`; use the tracked `./dotnet` wrapper. The wrapper
   sets the child-process environment internally and exports nothing into the
   caller shell.
4. Repository scripts source `scripts/dotnet-env.sh`, which prefers `.dotnet/`
   when present and otherwise falls back to the system `dotnet`. Do not add
   `PATH` / `DOTNET_ROOT` / `NUGET_PACKAGES` exports to shell profiles or
   per-session workflows as a substitute for the install script.
5. The Godot engine binary is separate: `scripts/download-godot.sh` reads the
   `Godot.NET.Sdk` version from `Origo.GodotAdapter/Origo.GodotAdapter.csproj`
   and caches the matching engine under `.godot_binary/`.

## Local Agent Work Buffer

`_origo_local/` is a git-ignored single-book work buffer for findings and
handoffs that cannot be finished immediately. Root `README.md` is the live
index/status holder; numbered chapters belong to the same work item.

- **Status**: `inbox`, `in-progress`, `blocked`, `done`, or `superseded`;
  record owner/date/baseline commit.
- **Producer**: include problem evidence, official docs, upstream/downstream
  collaborators, related tests, relevant git history, scope/acceptance criteria,
  commands run, and known unknowns. Do not dump raw chat transcripts.
- **Consumer**: claim only after re-reading the full chain context and
  revalidating that its baseline assumptions still hold; set `in-progress` and
  add owner/branch or a new baseline commit; never delete or rewrite another
  agent's `in-progress` work.
- **Closeout**: only after the full AGENTS development loop passes; record the
  final commit and durable documentation location. Never delete or mark `done`
  an unverified, partially implemented, or uncommitted item; leave an exact
  handoff instead: worktree state, changed files, commands run, failures, and
  exact next action.
- **Constraints**: never `git add` or commit anything under `_origo_local/`;
  never store secrets. Stale content is marked `superseded` with a pointer to
  the replacement, not deleted for cleanliness. Durable conclusions must be
  migrated into tracked `docs/`, tests, or `CHANGELOG.md`.
- If the buffer grows large, organize it by date/topic without breaking the
  producer/consumer lifecycle.

Live protocol: `_origo_local/README.md`. These tracked rules are authoritative
when the buffer is present.

## Manual Version

Documentation is synchronized with the `<Version>` in the repository's `Directory.Build.props` — since docs and source code are co-located in the same repo, versioning is naturally consistent. A formal release must also update the version text in `docs/README.zh.md` / `docs/README.en.md` as described in [release-process.en.md](release-process.en.md); `scripts/verify-release.sh` checks that both files mention the release version.

## Generation

The **content files** (`.zh.md` / `.en.md`) of this manual are hand-written after analyzing source code. The **navigation hubs** (`README.md`) and **sync status file** (`.sync-status.json`) are auto-generated by `DocSyncTool generate` and must not be edited manually. Quality depends on correct understanding of the source code and the maintainer's design knowledge. If discrepancies are found, report to the manual maintainer.

---
[↑ Back to Origo Manual](README.en.md)
