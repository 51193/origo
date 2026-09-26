<!-- docsync-pair: architecture/shell-api-baseline -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Shell API Baseline and Gate

> [↑ Back to architecture](README.en.md) · [↔ shell/kernel boundary](shell-kernel-boundary.en.md) · [↔ API classification](shell-api-classification.en.md)

This document defines the 0.1.0 shell API baseline gate: a tracked Roslyn inventory produces deterministic JSON from a real Release build, and every unapproved shell API addition, removal, or signature change must update the baseline in the same reviewed change.

## Scope

- The inventory covers the exported surface of the `Origo.Core.Contracts`, `Origo.Core`, `Origo.GodotAdapter`, and `Origo.ConsoleBridge` shell assemblies.
- Exported types (including C# type modifiers such as `static`/`sealed`/`abstract`/`readonly`/`ref` and enum/struct kinds), public/protected members, signatures, nullable annotations, default values, generic constraints, `init`/`set` accessors, extension-method `this` parameters, public Source Generator members, and generated nested Godot signal types all enter the baseline.
- `Origo.Core.Kernel` is the kernel implementation package and stays out of the baseline; the tool fails explicitly when an exported signature references a kernel assembly type. `Origo.ConsoleBridge` is also part of the member-level baseline; its shell-only dependency and packaged consumption are additionally covered by #42.

## Gate Execution

`scripts/api-inventory.sh`:

1. Builds the four shell projects in Release; the repository-wide warnings-as-errors setting stops the gate before inventory generation on any compiler or Source Generator diagnostic error.
2. Runs the `tools/ApiInventoryTool` Roslyn metadata inventory.
3. In `verify` mode, compares each API line with `tools/ApiInventoryTool/shell-api-baseline.json`; any addition, removal, or signature change exits non-zero with the diff and the regeneration command.
4. `generate` mode is for approved changes: it rewrites the baseline JSON, which must be reviewed and committed with the implementation.

The script runs in normal CI (Ubuntu/macOS/Windows), `scripts/ci.sh`, and the Release workflow, so an unapproved change cannot pass any of those gates.

## Baseline and Ownership

| Item | Description |
|------|-------------|
| Baseline file | `tools/ApiInventoryTool/shell-api-baseline.json` |
| Produced by | `bash scripts/api-inventory.sh generate`; never hand-written |
| Owner | Framework maintainer, who confirms every diff is an intentional shell contract change during review |
| Update condition | An approved new shell API, removal, or signature change with tests and bilingual documentation updated in the same change |
| Kernel impact | Kernel implementation additions/removals do not change the baseline; a reference leak fails immediately |

There is no previous stable shell package before the first 0.1.0 release, so previous-package validation is explicitly skipped with an explanation. Afterwards, when `ORIGO_PREVIOUS_API_BASELINE` is not set, `scripts/api-inventory.sh` auto-detects the newest formal release tag reachable from HEAD, excluding the tag on the current commit and the current release tag supplied through `ORIGO_CURRENT_RELEASE_TAG`, and extracts its `shell-api-baseline.json`; the explicit environment variable overrides auto-detection. The previous-package compare fails removals and signature changes while allowing backward-compatible additions: 0.1.x allows compatible additions, and behavior breaks/removals target 0.2.0. 0.1.x promises source/behavior compatibility, not binary compatibility.

## Failure Semantics

- Build failure or Source Generator diagnostic error: the script exits before inventory generation and never leaves a partial baseline.
- Missing assembly, missing reference directory, unresolved reference, or an exported signature referencing a kernel type: the tool reports an explicit error and exits non-zero without writing `generate` output. Kernel-reference checks recurse through generic type arguments, array/pointer/function-pointer elements, indexer parameters, and method generic constraints.
- Missing baseline file: `verify` fails explicitly and instructs the developer to generate the baseline in the same reviewed change.
- Determinism: JSON is ordered by assembly name and ordinal API lines with normalized LF line endings, so repeated runs are byte-identical. `ApiInventoryTool.Tests` covers determinism, addition/removal detection, previous-package rules, kernel-reference rejection, and command-line failure paths.

## Design Decisions

### Why a JSON Baseline Before an Analyzer

The 0.1.0 shell surface is still stabilizing. A tracked JSON baseline gives a reviewable full diff in normal CI and allows evaluating a `PublicApiAnalyzers`-style gate after the baseline stabilizes; introducing an analyzer first would freeze undecided implementation details as diagnostic noise.

### Why Additions Also Require a Baseline Update

The gate means "the shell surface consumers see can only change intentionally", not "no evolution". An added API changes the consumer contract just as a removal does, so it requires explicit review and a same-commit baseline update.

### Why the Baseline Excludes Kernel Assemblies

Kernel packages make no consumer compatibility promise in 0.1.x; including the kernel export surface would freeze implementation details as a contract. A shell signature referencing a kernel type is a compile-surface leak, so it fails instead of being recorded as an allowed difference.

---
[↑ Back to Origo Manual](../README.en.md)
