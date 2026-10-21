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
| Update condition | An approved new shell API, removal, or signature change with tests and English documentation updated in the same change |
| Kernel impact | Kernel implementation additions/removals do not change the baseline; a reference leak fails immediately |

There is no previous stable shell package before the first 0.1.0 release, so previous-package validation is explicitly skipped with an explanation. Afterwards, when `ORIGO_PREVIOUS_API_BASELINE` is not set, `scripts/api-inventory.sh` auto-detects the newest formal release tag not newer than the current source version, excluding the tag on the current commit and the current release tag supplied through `ORIGO_CURRENT_RELEASE_TAG`, and extracts its `shell-api-baseline.json`; the explicit environment variable overrides auto-detection. A tag-only release commit is a metadata-only child of the main commit it was cut from, so the helper accepts such a tag even when it is not an ancestor of HEAD. The previous-package compare fails removals and signature changes while allowing backward-compatible additions: 0.1.x allows compatible additions, and behavior breaks/removals target 0.2.0. 0.1.x promises source/behavior compatibility, not binary compatibility.

## Failure Semantics

- Build failure or Source Generator diagnostic error: the script exits before inventory generation and never leaves a partial baseline.
- Missing assembly, missing reference directory, unresolved reference, or an exported signature referencing a kernel type: the tool reports an explicit error and exits non-zero without writing `generate` output. Kernel-reference checks recurse through generic type arguments, array/pointer/function-pointer elements, indexer parameters, and method generic constraints.
- Missing baseline file: `verify` fails explicitly and instructs the developer to generate the baseline in the same reviewed change.
- Determinism: JSON is ordered by assembly name and ordinal API lines with normalized LF line endings, so repeated runs are byte-identical. `ApiInventoryTool.Tests` covers determinism, addition/removal detection, previous-package rules, kernel-reference rejection, and command-line failure paths; the per-method breakdown is under [Test Capability](#test-capability) below.

## Test Capability

`tools/ApiInventoryTool.Tests` verifies the tool end to end (xUnit, 14 test methods):

| Test File | Verification Focus |
|-----------|-------------------|
| `BaselineContentTests.cs` | Contents and limits of the tracked `shell-api-baseline.json` |
| `InventoryBuilderTests.cs` | Inventory building, kernel-reference rejection, and the verify/compare rules |
| `ProgramTests.cs` | Command-line validation and the exit codes of generate, verify, and compare |
| `TestAssemblyEmitter.cs` | Helper that compiles throwaway assemblies with Roslyn for the cases above |

### BaselineContentTests

| Test Method | Verified Behavior |
|-------------|-------------------|
| `TrackedBaseline_ContainsGeneratedSurfaceAndExcludesKernel` | The tracked baseline parses into four shell assemblies, carries the generated Contracts accessor, the adapter's generated nested `MethodName` / `PropertyName` / `SignalName` types, and the ConsoleBridge server surface, and no API line in any assembly references `Origo.Core.Kernel`. |

### InventoryBuilderTests

| Test Method | Verified Behavior |
|-------------|-------------------|
| `Build_IsDeterministicAndSorted` | The same assembly compiled with its declarations in the opposite order serializes to byte-identical JSON and the same ordered API lines. |
| `Verify_ReportsAddedRemovedAndChangedApi` | Verify mode reports removed-or-changed API lines and added API lines separately. |
| `ComparePrevious_AllowsAdditionsAndRejectsRemovals` | Previous-package comparison accepts additions and rejects a removal. |
| `Build_RejectsMissingAssemblyExplicitly` | A missing assembly path fails the build with a "was not found" error. |
| `Build_RejectsForbiddenKernelReference` | A shell signature exposing an `Origo.Core.Kernel` type fails with "exposes kernel type". |
| `Build_CollectsShellMemberKindsAndConstraints` | Enum, delegate, struct, and record kinds, generic constraints (`where T : class, new()`), nested types, a struct property, an event, a static field, and a protected member all enter the inventory. |
| `Build_ReportsForbiddenMemberKinds` | Kernel references in methods, properties, fields, and events are each reported. |
| `Build_ReportsEmptyNameAndMissingReferenceDirectory` | An empty assembly name, a missing reference directory, and a mismatched assembly name each fail explicitly. |
| `Build_ResolvesTypeKindsWhenTheAssemblyReferencesSystemRuntime` | With `System.Runtime` available, enum and readonly-struct kinds resolve instead of falling back to a reference type. |
| `Build_RecordsTypeModifiersInitAccessorsAndExtensionThis` | `static`, `abstract`, `sealed`, and `readonly` type modifiers, `init` accessors, and extension `this` parameters are recorded. |
| `Build_ReportsForbiddenArrayIndexerAndMethodConstraintReferences` | Kernel references reached through array elements, indexers, and method generic constraints are reported. |

### ProgramTests

| Test Method | Verified Behavior |
|-------------|-------------------|
| `GenerateVerifyAndCompare_ReturnExpectedExitCodes` | Generate writes the baseline with exit 0, verify prints `baseline: OK` with exit 0, a clean compare prints `validation: OK` with exit 0, and a mismatch exits 1. |
| `CommandLineValidation_ReturnsFailureForBadInput` | No arguments, an unknown command, a missing `--assembly`, a malformed `--assembly`, and a missing assembly file each exit 1, while `--help` exits 0. |

## Design Decisions

### Why a JSON Baseline Before an Analyzer

The 0.1.0 shell surface is still stabilizing. A tracked JSON baseline gives a reviewable full diff in normal CI and allows evaluating a `PublicApiAnalyzers`-style gate after the baseline stabilizes; introducing an analyzer first would freeze undecided implementation details as diagnostic noise.

### Why Additions Also Require a Baseline Update

The gate means "the shell surface consumers see can only change intentionally", not "no evolution". An added API changes the consumer contract just as a removal does, so it requires explicit review and a same-commit baseline update.

### Why Release Tags Are Selected by Version, Not Ancestry

A release tag points at a tag-only release commit whose parent is the main commit
the release was cut from. That commit is a sibling of the next release commit, not
its ancestor. Requiring ancestry alone would silently skip the immediately
preceding release and compare the new surface against an older baseline, which
could let a removal introduced in the skipped release pass. The helper therefore
selects the newest formal tag not newer than the current source version and accepts a
non-ancestor tag only when it is the known metadata-only `chore(release): sync
version stamps to vX.Y.Z` commit whose parent is reachable from HEAD.

### Why the Baseline Excludes Kernel Assemblies

Kernel packages make no consumer compatibility promise in 0.1.x; including the kernel export surface would freeze implementation details as a contract. A shell signature referencing a kernel type is a compile-surface leak, so it fails instead of being recorded as an allowed difference.

---
[↑ Back to Origo Manual](../README.en.md)
