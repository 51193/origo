<!-- docsync-pair: architecture/agent-friendly/affected-checks -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Affected checks: shorten feedback without weakening the quality contract

> [↑ Back to the Agent Friendly investigation](README.en.md)

Investigation date: 2026-09-18; repository observation baseline: `cdba5e4`. This report separates observations, inferences, and proposals. The `check.sh` commands, planner, and JSON contract below are not implemented and do not change the complete workflow in [AGENTS.md](../../../AGENTS.md).

## 1. Current behavior and concrete costs

**Observation:** [test.sh](../../../scripts/test.sh) restores and builds all of `Origo.sln` in Release, then executes non-Benchmark tests. [ci.sh](../../../scripts/ci.sh) runs script lint, formatting, DocSync, tests, benchmarks, and Godot integration in order. It checks that `docs/` is committed after DocSync, making it a post-commit gate. Individual scripts already provide partial entry points during development; the missing facility is a consistent planner answering which projects and supporting facilities a change must check.

`-m:1` has a documented purpose: parallel test processes on Windows can trigger an xUnit v3 assembly-info child-process exit race. **Removing serialization is not an appropriate Agent Friendly optimization.** Reduce unrelated projects first while preserving the safe execution policy. Core and Adapter also have different coverage exclusions. Godot native calls are tested by a separate headless runner, so passing xUnit does not establish passing engine behavior. See [Core tests](../../Origo.Core.Tests/README.en.md) and [Godot integration tests](../../Origo.GodotAdapter.Integration.Tests/README.en.md).

**Inference:** An agent fixing `SavePayloadReader.cs` first needs to establish whether a real save/load regression reproduces. Repeating the complete solution lengthens hypothesis testing. Filtering by test names containing `Save`, however, can miss runtime restoration, strategy hooks, and observer topology. The goal is to reduce feedback cost using evidence, without claiming an unmeasured speedup.

## 2. Explicit contracts for three check levels

| Proposed mode | Contents | What a successful result establishes |
|---|---|---|
| `quick` | Required formatting, target-project compilation, and a specified real-path regression; verify actual test execution count | Local feedback on the current hypothesis, without project coverage or complete-chain proof |
| `affected` | Complete non-Benchmark suites for affected projects with their existing coverage gates, plus relevant DocSync, generator, or Godot checks | The scope selected by explicit dependency contracts passes; selection can still miss impacts |
| `full` | Current post-commit `ci.sh`, followed by commit-message lint; the CI OS matrix retains its role | The repository's required final completion gates pass |

Projects enable `CollectCoverage` by default. A filtered `quick` run must **explicitly disable coverage collection for that local run** and report `coverage: not-measured`. It must not lower thresholds, present subset coverage as project coverage, or present quick success as CI success. `affected` runs complete non-Benchmark suites of selected projects, preserving their ≥90% line coverage gates and exclusions rather than merging unrelated subsets. Microsoft's VSTest documentation supports project selection and `--filter`; it also warns that zero matching tests can return success by default. Therefore zero executed tests must be an explicit failure gate. [dotnet test with VSTest](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest)

Proposed usage:

```bash
# Proposed commands; this script does not currently exist
bash scripts/check.sh plan --base <commit> --worktree --json
bash scripts/check.sh quick --plan .artifacts/check-plan.json --test <regression>
bash scripts/check.sh affected --plan .artifacts/check-plan.json
# The actual final entry point remains post-commit bash scripts/ci.sh
```

## 3. Selection: project graph plus explicit impact contracts

First collect the Git baseline-to-HEAD diff, staged and unstaged changes, and untracked files, including renames and deletions. Record a baseline and working-tree fingerprint; changed fingerprints require replanning before execution. Second calculate reverse dependency closure from the **MSBuild-evaluated project graph**, preserving generator edges such as `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`, rather than inspecting only ordinary DLL references. MSBuild's static graph establishes project build dependencies, but does not describe every runtime reflection, resource-path, or behavioral-test impact. [MSBuild static graph](https://github.com/dotnet/msbuild/blob/main/documentation/specs/static-graph.md)

Third add version-controlled impact contracts. Shared changes to `Directory.Build.props`, `Directory.Packages.props`, `global.json`, the solution, `.editorconfig`, or primary CI scripts require full checks. DocSync configuration or implementation affects tool tests and documentation validation. Godot scenes, resources, `project.godot`, and engine bridges affect headless tests. Shared TestSupport changes affect every referencing test project. Generator changes affect generator tests, Core, GodotAdapter, and their consumers. Adding, deleting, or renaming `.cs` files triggers mirror file-list checks, including internal implementation files.

Unknown paths, unevaluable projects, missing contracts, empty test selections, generator diagnostics, and configuration mismatches must fail with an explanation. The planner may report `requiresFull: true`, followed by an explicit full invocation from the user or prescribed command. It must not silently select no checks after a parsing failure. A directory named `Save` is insufficient evidence for a test boundary; manual contracts require historical-change replay.

| Actual repository path example | Minimum conservative selection and rationale |
|---|---|
| `Origo.Core/Save/Storage/SavePayloadReader.cs` | Complete Core.Tests suite; retain downstream tests from Core's reverse dependencies, including ConsoleBridge and GodotAdapter; real Godot restoration requires integration coverage. Initially selecting all Core consumers is acceptable; narrow only after evidence |
| `Origo.SourceGeneration/TypedDataGenerator.HomeGeneration.cs` | SG.Tests, compilation/tests for Core and Adapter consumers, and headless TypedData registration; generated API can change without a handwritten output diff |
| `Origo.GodotAdapter/Bootstrap/OrigoDefaultEntry.Bootstrap.cs` | Adapter.Tests plus Godot headless; native calls in this file are excluded from pure .NET coverage and require real `_Ready`, subsequent frames, and failed-startup validation |
| `Origo.TestSupport/FileSystem/TestMemoryFileSystem.cs` | Every TestSupport-referencing suite selected from the graph, rather than only filesystem tests |
| `tools/DocSyncTool/Validator.cs` | DocSyncTool.Tests and repository DocSync generate/validate; commit and full checks remain required |
| Only `docs/usage/agent-reference.*.md` | DocSync generate/validate; add compilable-example checks if implemented, because valid links do not establish correct code |

A conservative, broad project set is a reasonable initial cost. Consider module-level suite labels only when project size warrants it. Avoid manually maintaining hundreds of test names as a second dependency system.

## 4. Reviewable machine output

This excerpt illustrates a proposed planner contract, not an actual execution result:

```json
{
  "schemaVersion": 1,
  "baseCommit": "cdba5e4",
  "worktreeFingerprint": "<content-hash>",
  "configuration": "Release",
  "changedPaths": ["Origo.GodotAdapter/Bootstrap/OrigoDefaultEntry.Bootstrap.cs"],
  "selectedProjects": ["Origo.GodotAdapter.Tests"],
  "additionalGates": ["godot-headless", "doc-sync"],
  "reasons": [
    {"gate": "godot-headless", "rule": "engine-bound-bootstrap"}
  ],
  "coverage": "project-suite-with-existing-thresholds",
  "requiresFull": false,
  "finalGateRequired": true
}
```

The complete contract also needs SDK/Godot versions, TFM, graph and contract hashes, command argument arrays, execution/pass counts, exit codes, durations, and artifact paths. stdout carries JSON; stderr carries diagnostics. Failures identify the path, rule, and next action. `requiresFull: false` means the affected plan can execute; `finalGateRequired: true` makes the eventual full gate unconditional. The shell must not execute arbitrary command text from natural language or unvalidated JSON.

## 5. How to detect omitted checks

Build historical samples with genuine regressions involving persistence, deferred queues, observer restoration, generated Kind registration, Godot startup, TestSupport, and global build configuration. Fix each baseline and patch, run affected and full, and compare **failure sets**, not merely zero exit codes. Deliberately break upstream interfaces, remove generator output, modify `.tscn` files, and change shared properties to establish that relevant downstream checks are selected. Cover renames/deletions, untracked files, zero matching tests, and stale plans too.

Across real tasks record omission rate, unnecessary selections, cold/warm median and P95 durations, and separate restore/build/test/Godot costs. Initial acceptance can require zero omissions on known failing samples, explicit failure for unknown inputs, and preservation of full CI. That is not a mathematical guarantee against future omissions. Time saved during development should exceed maintenance costs for the graph, impact contracts, and samples before adding finer granularity.

This facility primarily helps Origo maintainers. Game developers also need local loops for their strategies, saves, scenes, and gameplay acceptance. Both can share planning infrastructure, but cannot share a rule table assuming identical game structure.

## 6. Recommended order and limits

Start with a read-only `plan` and explanations, then project-level affected execution, and only then module-level filtering. Before implementation, synchronize AGENTS/META iteration rules to authorize quick/affected while preserving final full checks. This investigation does not replace the currently mandatory `test.sh`. Human documentation continues to describe cross-module designs and test behavior; the planner routes execution evidence. See [Machine API inventory](api-inventory.en.md) for API and generator facts.
