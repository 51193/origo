# Origo Agent — Mandatory Workflow & Development Rules

> **Language**: If you are an AI agent reading this file, ask the user
> "Which language do you prefer?" before your first substantive response.
> Use that language for all subsequent communication.

> This file is the **single authoritative entry point** for AI agents and
> developers working on this repository. It is automatically injected at the
> start of every session.
> Any **reading or modification** of this repository must comply with this
> file and the documents it directs you to read (`docs/META.zh.md` or `docs/META.en.md`,
> each module's `docs/.../README.zh.md` or `.en.md`).
> Documentation lives inside the repo under `docs/` and does not depend on
> any external documentation repository.

---

## 0. Pre-work Gate (Mandatory Reading)

**Before reading or modifying any source code in this project, you must
read the following in full:**

1. **This file** — development loop, core principles, document index,
   bilingual documentation mechanism (§1.6).
2. **`docs/META.zh.md`** (or [`docs/META.en.md`](docs/META.en.md)) — documentation maintenance rules
   (writing conventions + Git commit message conventions + DocSyncTool
   usage).
3. **The documentation for the module you are changing**:
   `docs/<mirror-path>/README.zh.md` (Chinese) or
   `docs/<mirror-path>/README.en.md` (English), **as well as the
   documentation for its upstream/downstream and related facilities**
   (per §1.3 full-chain principle).

`docs/` is a **structural mirror** of the source code published in two
languages side by side. Every directory contains:

| File | Purpose |
|------|---------|
| `README.md` | **Auto-generated** navigation hub (lists all `.zh.md` / `.en.md` files). **Do not edit.** |
| `README.zh.md` | Chinese documentation content |
| `README.en.md` | English documentation content |

Documentation for `Origo.Core/Snd/Entity/` lives at
`docs/Origo.Core/Snd/Entity/README.zh.md` (Chinese) and
`docs/Origo.Core/Snd/Entity/README.en.md` (English).
The auto-generated `docs/Origo.Core/Snd/Entity/README.md` is a bilingual
navigation hub — it links to both language versions.

**Do not bypass the docs and start reading raw source.** Follow the chain
"this file → [`docs/README.md`](docs/README.md) (auto-generated hub)
→ language-specific README → module READMEs drilling down" to get the full
context. Every README includes design rationale and "why / why not"
trade-offs — these are prerequisites for safe changes, not optional reading.

---

## 1. Core Principles

### 1.1 Fail-fast

- When an interface contract is violated, **throw an exception**. **Silent
  degradation or fallback is forbidden.**
- Save/load operations **strictly validate integrity**. Prefer explicit failure
  over accepting a half-initialized state.
- Do not swallow exceptions, stuff defaults, or add defensive fallbacks just
  to "get it running first" — these mask real errors.

### 1.2 Early Development — No Backward-Compatibility Burden

This project is in **early development** and **does not promise API stability**.

- **Forbidden**: compatibility shims, deprecation layers, dual-track APIs,
  migration shells, or any code whose sole purpose is "keep the old usage
  working."
- **Forbidden**: preserving evolution traces — no dead code, commented-out
  old implementations, or historical markers like `since v0.x`, `legacy`,
  `new`.
- When a change is needed, **make a clean breaking change**. Bring the code
  and docs to what they should be right now. Do not create technical debt in
  the name of stability promises.
- Breaking changes are not free: they must still be recorded in
  `CHANGELOG.md` per §4, prefixed with `BREAKING:`, so users are informed.
- **Forbidden**: exposing public properties, `internal` properties, or extra
  methods in production code just for test convenience. Framework code should
  be written **as if tests do not exist**, with its own correctness and safety
  as the sole standard. Core objects like `ISndContext` and `SndContext` do
  not need a property "so tests can access the SessionManager." Tests should
  reach framework internals via `InternalsVisibleTo`, reflection, or
  self-constructed test infrastructure (`TestFactory`, etc.), rather than the
  framework "leaving a door open" for them.

### 1.3 Full-Chain Understanding — Eliminating False-Positive Tech Debt Fixes

> **This is the most frequently violated and most costly principle in this
> project.**

Before **modifying or extending** any source code, you **must** read the
documentation and code of its **upstream, downstream, and all related
facilities**, to understand the collaboration contracts between modules.

- **Many modules are only correct and safe in cooperation with their
  collaborators.** Viewed in isolation, a type may appear "redundant /
  unsafe / under-exposed / duplicated," but that is often deliberate design.
  - Example: `ISndEntityRawSubscription` uses **explicit interface
    implementation** to hide from business `ISndEntity`, driven **only by
    the per-scene-host `ObserverTopology`**. Observer bindings are
    serialized via topology (`ObserverIndices`) and auto-restored on load,
    rather than being manually reconnected by business code. "Fixing" it
    outside that chain would break cross-module correctness.
  - Example: `IEntityLifecycle`'s phased methods are intentionally not
    exposed on the business-facing `ISndEntity`. Hook timing is centrally
    orchestrated by `SndEntityFactory` / `SessionRun`.
- **Do not treat an "apparent defect" in design as tech debt to "fix" before
  understanding the entire collaboration chain.** Such misjudgments are
  **false positives** — changing them does not solve a problem but introduces
  real bugs.
- When you suspect a genuine defect: first **confirm the design intent** in
  the corresponding `docs/.../README.md` "Design Decisions / Why / Why Not"
  section. If the docs don't cover it or you cannot confirm, **ask the
  maintainer**. Do not make changes based on partial speculation.
- Likewise, §4's Changelog must **not** mis-record such "cross-module
  co-designed constructs" as `Fixed`.

### 1.4 Single Access Path — Eliminating Backdoors

> **Every capability must have exactly one external access path. Backdoors
> are a breeding ground for bugs.**

- If an operation is already exposed via a dedicated interface (e.g.,
  `ISessionRun.RequestKillEntity`), then **every other path that can achieve
  the same effect must be sealed**: make the objects/methods that could
  simulate that interface `internal`, and encapsulate their other capabilities
  behind corresponding dedicated interfaces.
- **Forbidden**: letting callers hand-stitch low-level operations to
  "manually simulate" what an interface does — even if the surface result
  appears identical, the intentionally orchestrated side effects inside the
  interface (validation, hooks, state transitions, resource lifecycle
  management) are skipped. Such omissions caused by backdoors are extremely
  hard to diagnose.
- If a backdoor originates from an object that **should not possess that
  capability** (i.e., it is neither the intended provider of that capability
  nor part of its delegation chain), this is likely a design defect. This
  situation **must be evaluated by the maintainer**. Agents must not
  auto-scan-and-fix — to avoid misclassifying deliberate cross-module
  collaboration as a backdoor (see §1.3).

### 1.5 Consistent Code Format — Local Equals CI

- **All C# code must pass `dotnet format --verify-no-changes --severity info`.**
  This is the first CI gate (see `scripts/format.sh` and
  `.github/workflows/ci.yml`).
- `.editorconfig` defines the project's full set of formatting rules:
  naming, whitespace, collection initializers, `var` preferences, primary
  constructors, etc. CI enforces them; local runs of `scripts/ci.sh`
  provide equivalent validation.
- **Test projects use flat namespaces** (`Origo.Core.Tests`, not
  `Origo.Core.Tests.Snd.Strategy`). This is deliberate xUnit convention
  design — it enables cross-directory type access. IDE0130 is suppressed
  for test paths in `.editorconfig`. See
  [`docs/Origo.Core.Tests/META-TEST.zh.md`](docs/Origo.Core.Tests/META-TEST.zh.md)
  (or [META-TEST.en.md](docs/Origo.Core.Tests/META-TEST.en.md))
  §测试命名空间约定.
- **Do not abuse `.editorconfig` exclusion rules**: they exist only for
  documented deliberate designs (e.g., flat test namespaces). Do not
  temporarily disable rules to bypass format checks.

### 1.6 Bilingual Documentation — Co-located Side-by-Side

> **Every documentation file exists in at least one language version.
> Links within a language stay within that language. Drift is detected
> automatically and CI-enforced.**

`docs/` uses **co-located language suffixed files**:

```
docs/Origo.Core/Snd/
├── README.md           ← auto-generated bilingual nav hub (DO NOT EDIT)
├── README.zh.md        ← Chinese content (source-authored)
├── README.en.md        ← English content (translated / independently authored)
└── Entity/
    ├── README.md       ← auto-generated
    ├── README.zh.md
    └── README.en.md
```

Two files of the same base name with different language suffixes form a
**sync pair**. Sync is tracked by metadata headers at the top of every
content file:

```markdown
<!-- docsync-pair: docs/Origo.Core/Snd/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
```

| Field | Meaning |
|-------|---------|
| `docsync-pair` | Globally unique pair identifier (file path minus language suffix). Derived automatically; must match across languages. |
| `docsync-revision` | Monotonic integer. **Two files of a pair are in sync when their revisions are equal.** The trailing comment is a mandatory reminder — validated by CI. |

**Revision rules** (enforced by developer, verified by CI):

| Action | What to do |
|--------|-----------|
| You change content in a `.zh.md` file | **Increment** `docsync-revision` in that file. The `.en.md` is now stale. |
| You translate the `.zh.md` changes into `.en.md` | **Set** `docsync-revision` in `.en.md` to match `.zh.md` |
| You add new original content to `.en.md` (not a translation) | **Set** `docsync-revision` in `.en.md` to `max(zh.rev, old_en.rev) + 1`. Now `.zh.md` is stale. |
| You create a brand new doc file | Start at `docsync-revision: 1` |

After any doc content or revision change, you **must** run:

```bash
dotnet run --project tools/DocSyncTool -- generate
```

This produces two kinds of derived files (commit them together with your change):

1. **`README.md`** navigation hubs in every directory — auto-generated
   index listing all `.zh.md` / `.en.md` files grouped by language.
2. **`docs/.sync-status.json`** — machine-readable snapshot of every pair's
   revision state.

**DocSyncTool cheat-sheet** (run from repo root):

| Command | What it does |
|---------|-------------|
| `dotnet run --project tools/DocSyncTool -- generate` | Regenerates all `README.md` hubs + `.sync-status.json`. Always succeeds. |
| `dotnet run --project tools/DocSyncTool -- validate` | Read-only check: all pairs have matching revisions across configured languages, all links point to same-language files, no broken links. **Exit code 1 on failure.** |
| `dotnet run --project tools/DocSyncTool -- init` | **One-time migration only** — renames `.md` → `.zh.md`, injects metadata, updates links. Already executed; do not re-run. |

**Link discipline** (validated as ERROR by `validate`):

- Chinese docs (`.zh.md`) link only to `.zh.md` targets
- English docs (`.en.md`) link only to `.en.md` targets
- Cross-language links are **forbidden**
- Bare `.md` links without language suffix are **forbidden** (after migration)

**Configured languages** are defined in
`tools/DocSyncTool/docsync-config.json`:

```json
{ "languages": ["zh", "en"], "docs_root": "docs" }
```

**CI enforcement**: `scripts/doc-sync.sh` (called by `scripts/ci.sh`) runs
`generate` then `validate`. On `push` to main, CI auto-commits stale
generated files; on `pull_request`, it fails with instructions to run
`generate` locally. Validation failures (revision mismatch, broken links,
missing language files) always fail the build.

**Rules summary for agents**:

- When you change a doc file → bump its `docsync-revision`
- When you add a new doc file → create it as `.zh.md` (or `.en.md`) with
  `docsync-revision: 1` and a `docsync-pair` header
- When you sync a translation → match the peer's revision
- After any doc change → run `generate` and commit the result
- **Never edit** auto-generated `README.md` hubs or `.sync-status.json`

### 1.7 Source Code Comments — English Only, IntelliSense-Ready

> **All XML doc comments on public and protected API surfaces must be in
> English. Comments serve IntelliSense discoverability in the IDE — not as
> a substitute for hand-written documentation.**

- **Every `public` and `protected` type and member** must carry a
  `<summary>` XML doc comment in English. This is the primary source of
  IDE tooltip content for library consumers.
- **`internal` classes that implement public interfaces** should also have
  English comments describing their role and any non-trivial contracts
  (e.g., constructor preconditions, disposal semantics, thread safety).
- **Existing Chinese XML doc comments on public API surfaces are a defect**
  and must be translated to English. Implementation files with Chinese
  comments are lower-priority but should trend toward English over time.
- **Test files are exempt** from this rule. Test methods may carry comments
  in either language or none at all.
- **Forbidden**: API documentation generation tools (DocFX, Sandcastle, etc.).
  The project maintains hand-written bilingual documentation under `docs/`;
  source comments complement it at the IDE level. No duplicate generated
  API reference is needed.
- This rule is enforced by developer discipline and code review.

---

## 2. Development Loop (Mandatory Order)

> **Every change must close the loop in the following order. The order must
> not be rearranged, and no step may be skipped.**

| Step | Name | Description |
|------|------|-------------|
| 1 | **Develop source** | Implement the feature / fix / refactor, satisfying §0 gate and §1 principles. |
| 2 | **Extend / adapt tests** | Add or adjust tests for this change: behavior tests for new public API, regression tests for bug fixes (red first), sync existing tests for behavior changes. |
| 3 | **Execute tests** | During development iteration, run `bash scripts/test.sh` (restore → build → test + coverage gates). **Before committing, you must run** `bash scripts/ci.sh`, which mirrors CI exactly: format + doc-sync + test + benchmarks + Godot integration. |
| 4 | **Fix source + re-test loop** | If tests are not all green, go back to fix the source and re-run step 3. **Loop until all pass.** Fixes must still comply with §1 (especially avoid false-positive fixes). |
| 5 | **Changelog alignment** | Write user-facing significant changes into `CHANGELOG.md` under the `[Unreleased]` section (conventions in §4). |
| 6 | **Docs sync** | Sync the `docs/` mirror: directory structure, interface lists, design decisions,   usage / test docs (rules in §5 and `docs/META.zh.md` / `docs/META.en.md`). **After any doc change, bump `docsync-revision`, run `DocSyncTool generate`, and commit all derived files** (per §1.6). |

**Partial completion is forbidden.** If a step is genuinely not applicable
(e.g., pure internal refactor with no public API or doc impact), you must
**explicitly state the reason for skipping** in the commit message.

---

## 3. Test Requirements

| Change type | Test requirement |
|-------------|------------------|
| New public API | Must have corresponding behavior tests. |
| Bug fix | Must have a regression test (red → green). |
| Behavior change | Update existing tests to reflect new behavior. |
| Refactoring | All existing tests must pass; no new tests required. |

- **Run command**: `bash scripts/ci.sh` (repo root, full local CI reproduction:
  format + build + test + coverage gates + benchmarks + Godot integration).
  During development iteration, use `bash scripts/test.sh` (build + test +
  coverage gates only).
- **Test projects**: `Origo.Core.Tests`, `Origo.GodotAdapter.Tests`,
  `Origo.ConsoleBridge.Tests`, `Origo.SourceGeneration.Tests`.
- **Coverage gates** are enforced by Coverlet in `test.sh` (≥ 90% line coverage
  across all test projects); falling
  below the threshold causes `dotnet test` to fail directly.
- Test style conventions, `InternalsVisibleTo` whitelist principles, static
  mutable state isolation, etc. are documented in
  [`docs/Origo.Core.Tests/META-TEST.zh.md`](docs/Origo.Core.Tests/META-TEST.zh.md)
  (or [META-TEST.en.md](docs/Origo.Core.Tests/META-TEST.en.md)).

---

## 4. Changelog Conventions

Format based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/),
following [Semantic Versioning](https://semver.org/spec/v2.0.0.html). File
location: `CHANGELOG.md`.

### Change categories

| Category | Meaning |
|----------|---------|
| `Added` | New features. |
| `Changed` | Changes to existing functionality. |
| `Deprecated` | Soon-to-be-removed features. |
| `Removed` | Removed features. |
| `Fixed` | Bug fixes. |
| `Security` | Security improvements. |

> **Breaking changes do not get a separate category.** Classify them under
> `Changed` (behavior change) or `Removed` (API removal), and prefix the
> entry with `BREAKING:`. Do not use a standalone `Breaking Changes` category.

### Key constraints

1. **Baseline is the last formal release tag.** Compare differences from the
   last formal release to the current state and extract user-facing
   significant changes. Nightly tags (e.g., `v0.0.8-nightly.20260626`) are
   not baselines.
2. **Nightly is not a version.** Version numbers with `-nightly`, `-alpha`,
   `-preview`, etc. are snapshot identifiers, not semantic versions. Those
   changes stay in `[Unreleased]`. Only un-suffixed formal version numbers
   (e.g., `0.0.7`) produce a `## [x.y.z] - YYYY-MM-DD` block.
3. **Do not record intra-version back-and-forth.** Features introduced then
   removed, or bugs introduced then fixed within the same release cycle —
   such noise should not appear in the changelog. Record only the final
   state.
4. **Do not record fixes for self-introduced issues in the same version.**
   Bugs introduced and fixed within the same formal release cycle are
   neither recorded as introduced nor as fixed.
5. **Write for the user.** Describe how the behavior change affects users,
   not internal implementation details.
6. **Comply with §1.3.** Do not record "cross-module co-designed constructs"
   as `Fixed`.
7. **Daily changes go into `[Unreleased]`.** Nightly builds daily; changes
   accumulate in `[Unreleased]`. When cutting a formal release, move them
   into a versioned block.

### Writing process

1. Determine the last formal release tag.
2. Compare all changes from that tag to current HEAD.
3. Categorize user-facing significant changes, filtering out intra-version
   back-and-forth.
4. Write into the corresponding category under `[Unreleased]`.

---

## 5. Docs Sync Rules

`docs/` is a structural mirror of the source code. The following situations
require a docs sync at step 6:

| Source change | Docs action |
|---------------|-------------|
| Add / delete / rename directory | Mirror the same operation in `docs/`. |
| Add public interface / method | Update the interface list in the corresponding leaf README. |
| Delete / rename public interface | Update the corresponding leaf README; remove old entries. |
| Design decision change | Update the design decisions section in the corresponding README. |
| Add config key / command | Update the relevant README and `docs/usage/` docs. |
| Inter-module dependency change | Update the module README's links. |
| Add test capability / method | Update the corresponding `docs/Origo.*.Tests/` capability docs. |

**No sync needed**: purely internal implementation details, refactors that
do not change responsibilities or interfaces, and performance optimizations
that do not change external semantics.

### Sync checklist

- [ ] Is the directory structure mirrored (add / delete / rename)?
- [ ] Are leaf README interface / file lists accurate?
- [ ] Are intermediate README sub-module indexes complete?
- [ ] Are all links valid (no 404)?
- [ ] Does the design decisions section reflect current design intent?
- [ ] Do `docs/usage/` and test docs cover new scenarios / capabilities?

For documentation hierarchy, link conventions, prohibition of evolution
markers, and commit message conventions, see [`docs/META.zh.md`](docs/META.zh.md)
  (or [`docs/META.en.md`](docs/META.en.md)).

---

## 6. Release Process

1. Determine the new version number (formal semantic version, no `-nightly`
   suffix, etc.).
2. Move `[Unreleased]` content into a `## [x.y.z] - YYYY-MM-DD` block.
3. Clear `[Unreleased]`.
4. Update `<Version>` in `Directory.Build.props` — the full version string
   (including any `-nightly` suffix) must exactly match the tag name, or the
   release workflow fails its tag/version consistency check.
5. Commit and tag (tag name `vx.y.z`). Pushing the tag triggers the
   `release` workflow: packs Origo.Core / Origo.GodotAdapter /
   Origo.ConsoleBridge as NuGet packages and attaches them — together
   with a compressed archive of `docs/` as a documentation snapshot —
   to the GitHub Release. Packages are distributed as release artifacts
   (not pushed to nuget.org); consumers install them via a local package
   source pointing at the release downloads, as described in
   `README.md`.

---

## 7. Document Index

> So agents can read all relevant information without self-directed
> exploration. When changing source under `X/`, read `docs/X/README.zh.md`
> (or `.en.md`) and its upstream/downstream first.

| Entry | Path | Purpose |
|-------|------|---------|
| Manual index | [`docs/README.md`](docs/README.md) | Top-level navigation for all modules / usage / test docs. |
| Docs maintenance | [`docs/META.zh.md`](docs/META.zh.md) (or [META.en.md](docs/META.en.md)) | Documentation conventions + Git commit message conventions. |
| Changelog | [`CHANGELOG.md`](CHANGELOG.md) | User-facing change log. |
| Core module | [`docs/Origo.Core/README.md`](docs/Origo.Core/README.md) | Platform-agnostic core: SND entities, runtime, persistence, state machines, etc. |
| Source generation | [`docs/Origo.SourceGeneration/README.md`](docs/Origo.SourceGeneration/README.md) | TypedData incremental source generator. |
| Godot adapter | [`docs/Origo.GodotAdapter/README.md`](docs/Origo.GodotAdapter/README.md) | Godot 4 adapter layer. |
| ConsoleBridge | [`docs/Origo.ConsoleBridge/README.md`](docs/Origo.ConsoleBridge/README.md) | TCP remote console bridge. |
| Usage guide | [`docs/usage/README.md`](docs/usage/README.md) | From quick start to deep reference. |
| Test docs | [`docs/Origo.Core.Tests/README.md`](docs/Origo.Core.Tests/README.md) | Test coverage by capability. |
| Performance baseline | [`docs/benchmarks/baseline.zh.md`](docs/benchmarks/baseline.zh.md) (or [baseline.en.md](docs/benchmarks/baseline.en.md)) | TypedData performance snapshot and trade-offs. |
| DocSyncTool | [`tools/DocSyncTool/`](tools/DocSyncTool/) | Bilingual doc sync tool (generate, validate, init). |
| Formatting rules | [`.editorconfig`](.editorconfig) | C# code style + IDE/CA diagnostic rules. |
| CI workflows | [`.github/workflows/`](.github/workflows/) | CI / Release / CodeQL workflow definitions. |
| CI scripts | [`scripts/ci.sh`](scripts/ci.sh) | Full local CI reproduction; per-step scripts: `format.sh`, `doc-sync.sh`, `test.sh`, `benchmark.sh`, `godot-test.sh`. |
