# Affected checks: shorten feedback without weakening the quality contract

> [↑ Back to the Agent Friendly investigation](README.en.md)

Investigation date: 2026-09-18; implementation checked: 2026-09-27. This report separates observations, implementation contracts, and remaining risks. `scripts/check.sh`, its planner, and the JSON evidence are implemented without weakening the complete workflow in [AGENTS.md](../../../AGENTS.md).

## 1. Current behavior and concrete costs

**Observation:** [test.sh](../../../scripts/test.sh) restores and builds all of `Origo.sln` in Release, then executes non-Benchmark tests. [ci.sh](../../../scripts/ci.sh) runs script lint, formatting, tests, benchmarks, and Godot integration in order. Individual scripts already provide partial entry points during development; `scripts/check.sh` provides a consistent planner answering which projects and supporting facilities a change must check.

`-m:1` has a documented purpose: parallel test processes on Windows can trigger an xUnit v3 assembly-info child-process exit race. **Removing serialization is not an appropriate Agent Friendly optimization.** Reduce unrelated projects first while preserving the safe execution policy. Core and Adapter also have different coverage exclusions. Godot native calls are tested by a separate headless runner, so passing xUnit does not establish passing engine behavior. See [Core tests](../../Origo.Core.Tests/README.en.md) and [Godot integration tests](../../Origo.GodotAdapter.Integration.Tests/README.en.md).

**Inference:** An agent fixing `SavePayloadReader.cs` first needs to establish whether a real save/load regression reproduces. Repeating the complete solution lengthens hypothesis testing. Filtering by test names containing `Save`, however, can miss runtime restoration, strategy hooks, and observer topology. The goal is to reduce feedback cost using evidence, without claiming an unmeasured speedup.

## 2. Explicit contracts for three check levels

| Mode | Contents | What a successful result establishes |
|---|---|---|
| `quick` | Required formatting, target-project compilation, and a specified real-path regression; verify actual test execution count | Local feedback on the current hypothesis, without project coverage or complete-chain proof |
| `affected` | Complete non-Benchmark suites for affected projects with their existing coverage gates, plus relevant generator or Godot checks | The scope selected by explicit dependency contracts passes; selection can still miss impacts |
| `full` | Current post-commit `ci.sh`, followed by commit-message lint; the CI OS matrix retains its role | The repository's required final completion gates pass |

Projects enable `CollectCoverage` by default. A filtered `quick` run must **explicitly disable coverage collection for that local run** and report `coverage: not-measured`. It must not lower thresholds, present subset coverage as project coverage, or present quick success as CI success. `affected` runs complete non-Benchmark suites of selected projects, preserving their ≥90% line coverage gates and exclusions rather than merging unrelated subsets. Microsoft's VSTest documentation supports project selection and `--filter`; it also warns that zero matching tests can return success by default. Therefore zero executed tests must be an explicit failure gate. [dotnet test with VSTest](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest)

Actual usage:

```bash
# Read-only plan; by default the base is origin/main...HEAD's merge-base
bash scripts/check.sh plan --base <commit> --output /tmp/check-plan.json
# Real local path; project and filter are explicit
bash scripts/check.sh quick --project Origo.Core.Tests/Origo.Core.Tests.csproj --filter 'FullyQualifiedName~Save'
# Complete suites selected by impact contracts; stale/full plans stop
bash scripts/check.sh affected --base <commit>
# Final gate: complete ci.sh plus commit-message lint
bash scripts/check.sh full
```

## 3. Selection: project graph plus explicit impact contracts

First collect the Git baseline-to-HEAD diff, staged and unstaged changes, and untracked files, including renames and deletions. Record a baseline and working-tree fingerprint; changed fingerprints require replanning before execution. Second calculate reverse dependency closure from the **MSBuild-evaluated project graph**, preserving generator edges such as `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`, rather than inspecting only ordinary DLL references. MSBuild's static graph establishes project build dependencies, but does not describe every runtime reflection, resource-path, or behavioral-test impact. [MSBuild static graph](https://github.com/dotnet/msbuild/blob/main/documentation/specs/static-graph.md)

Third add version-controlled impact contracts. Shared changes to `Directory.Build.props`, `Directory.Packages.props`, `global.json`, the solution, `.editorconfig`, or primary CI scripts require full checks. Godot scenes, resources, `project.godot`, and engine bridges affect headless tests. Shared TestSupport changes affect every referencing test project. Generator changes affect generator tests, Core, GodotAdapter, and their consumers. Documentation changes are not attributable to a test project: review links and content manually, then run the explicit `full` gate.

Unknown paths, unevaluable projects, missing contracts, empty test selections, generator diagnostics, and configuration mismatches must fail with an explanation. The planner may report `requiresFull: true`, followed by an explicit full invocation from the user or prescribed command. It must not silently select no checks after a parsing failure. A directory named `Save` is insufficient evidence for a test boundary; manual contracts require historical-change replay.

| Actual repository path example | Minimum conservative selection and rationale |
|---|---|
| `Origo.Core.Kernel/Save/Storage/SavePayloadReader.cs` | Complete Core.Tests suite; retain downstream tests from Core's reverse dependencies, including GodotAdapter; ConsoleBridge depends only on Contracts and is not a downstream Core/Kernel consumer. Real Godot restoration requires integration coverage. Initially selecting all Core consumers is acceptable; narrow only after evidence |
| `Origo.SourceGeneration/TypedDataGenerator.HomeGeneration.cs` | SourceGeneration.Tests, Core.Tests, Adapter.Tests, and headless TypedData registration; generated API can change without a handwritten output diff |
| `Origo.GodotAdapter/Bootstrap/OrigoDefaultEntry.Bootstrap.cs` | Adapter.Tests plus Godot headless; native calls in this file are excluded from pure .NET coverage and require real `_Ready`, subsequent frames, and failed-startup validation |
| `Origo.TestSupport/FileSystem/TestMemoryFileSystem.cs` | Every TestSupport-referencing suite selected from the graph, rather than only filesystem tests |
| English module documentation | Manual link and content review followed by the explicit `full` gate; add compilable-example checks if implemented, because valid links do not establish correct code |

A conservative, broad project set is a reasonable initial cost. Consider module-level suite labels only when project size warrants it. Avoid manually maintaining hundreds of test names as a second dependency system.

## 4. Reviewable machine output

This excerpt illustrates the actual planner contract; each run also records the current fingerprint:

```json
{
  "schemaVersion": 1,
  "baseCommit": "cdba5e4",
  "worktreeFingerprint": "<content-hash>",
  "configuration": "Release",
  "changedPaths": ["Origo.GodotAdapter/Bootstrap/OrigoDefaultEntry.Bootstrap.cs"],
  "selectedProjects": ["Origo.GodotAdapter.Tests/Origo.GodotAdapter.Tests.csproj"],
  "additionalGates": ["godot"],
  "reasons": [
    "Origo.GodotAdapter/Bootstrap/OrigoDefaultEntry.Bootstrap.cs changes the Godot contract"
  ],
  "coverage": "existing-project-gates",
  "requiresFull": false,
  "finalGateRequired": true
}
```

stdout carries JSON; stderr carries diagnostics. Failures identify the path, rule, and next action. `requiresFull: false` means the affected plan can execute; `finalGateRequired: true` makes the eventual full gate unconditional. The shell never executes arbitrary command text from natural language or unvalidated JSON.

## 5. Evidence and how to detect omitted checks

The planner's fixed samples are executable in `scripts/check_test.py`: C#
additions/deletions, generator changes and their Core/Adapter consumers, Godot
adapter changes, documentation full-gate behavior, stale fingerprints,
Benchmark exclusion, and empty-plan full-gate rejection. Test execution reports both `executed-tests` and
`elapsed-seconds` on stderr; JSON remains on stdout, so a caller can record
scope, reasons, counts, and timings without parsing human diagnostics. The
repository's final evidence remains `bash scripts/check.sh full`, which runs
the unchanged CI pipeline and commit lint.

Build historical samples with genuine regressions involving persistence, deferred queues, observer restoration, generated Kind registration, Godot startup, TestSupport, and global build configuration. Fix each baseline and patch, run affected and full, and compare **failure sets**, not merely zero exit codes. Deliberately break upstream interfaces, remove generator output, modify `.tscn` files, and change shared properties to establish that relevant downstream checks are selected. Cover renames/deletions, untracked files, zero matching tests, and stale plans too.

Across real tasks record omission rate, unnecessary selections, cold/warm median and P95 durations, and separate restore/build/test/Godot costs. Initial acceptance can require zero omissions on known failing samples, explicit failure for unknown inputs, and preservation of full CI. That is not a mathematical guarantee against future omissions. Time saved during development should exceed maintenance costs for the graph, impact contracts, and samples before adding finer granularity.

This facility primarily helps Origo maintainers. Game developers also need local loops for their strategies, saves, scenes, and gameplay acceptance. Both can share planning infrastructure, but cannot share a rule table assuming identical game structure.

## 6. Recommended order and limits

The implementation provides read-only `plan`, project-level `affected`, and explicit-filter `quick` feedback; `full` still calls the existing `ci.sh` and commit-message lint. Human documentation continues to describe cross-module designs and test behavior; the planner routes execution evidence. See [Machine API inventory](api-inventory.en.md) for API and generator facts.
