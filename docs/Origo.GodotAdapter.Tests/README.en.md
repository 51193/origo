<!-- docsync-pair: Origo.GodotAdapter.Tests/README -->
<!-- docsync-revision: 13 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.GodotAdapter.Tests

> [↑ Back to Origo.manual](../README.en.md)

## Test Strategy Overview

The tests for Origo.GodotAdapter verify the correctness of the Godot 4 adapter layer.
The adapter bridges Core's abstract interfaces with Godot engine APIs; test focus includes:
file system path handling (`res://` / `user://` virtual paths), Godot type serialization round-trips,
adapter-layer console command extensions, bootstrap orchestration, and log proxying.

Since GodotAdapter depends on the Godot engine runtime, **any Godot API call (including `new Node()` and `GD.Print`) SIGSEGVs the test process** in the engine-less unit-test host, so source files that call engine APIs directly are excluded by coverlet (configured in `.csproj` via `ExcludeByFile`). Every excluded file has a documented technical reason (see the csproj comment): `GodotSndEntity.cs` / `GodotSndManager.cs` / `OrigoAutoHost.cs` / `OrigoDefaultEntry*.cs` (Godot Node subclasses; node creation and scene-tree operations are unreachable), `FileSystem/` (every member body is a `FileAccess` / `DirAccess` static call), `GodotNodeHandle.cs` / `GodotPackedSceneNodeFactory.cs` (require live nodes/resources), `CameraViewCommandHandler.cs` (`ExecuteCore`'s first statement is `Engine.GetMainLoop()`).

To shrink the exclusion surface, `GodotSndManager`'s entity-collection orchestration (add/remove, lookup, batch recovery rollback, frame processing, kill marking) is extracted into pure C# `SndEntityCollection<T>` (`Origo.GodotAdapter/Snd/SndEntityCollection.cs`), fully covered by `SndEntityCollectionTests` (98.8%); the 14 Godot-type accessors in the generated `TypedData.g.cs` are covered per-type by `GodotTypedDataGeneratedCoverageTests`. Line coverage within the gated scope is ≥ 90% (`ThresholdStat=total`, currently measured ≈94%).

Behavior verification for engine-dependent files is performed by
[Origo.GodotAdapter.Integration.Tests](../Origo.GodotAdapter.Integration.Tests/README.en.md)
in a real Godot `--headless` runtime. Integration tests and unit tests complement each other:
unit tests verify Core-layer logic and type serialization; integration tests verify Godot-specific
runtime behavior (real file system, Node lifecycle, bootstrap orchestration).

> **Coverage gate scope**: The 90% line coverage gate (`ThresholdStat=total`) of
> `Origo.GodotAdapter.Tests` measures **non-excluded** source files — i.e., code
> that does not depend on the Godot runtime and can be unit-tested in pure .NET.
> The exclusion surface (engine API binding files, one reason per file in the
> csproj comment) is backed by integration tests. Exclusion is a technical
> necessity (engine calls SIGSEGV), not a shortcut to avoid writing tests.

## Test Layers

| Layer | Project | Runtime | Coverage Scope |
|-------|---------|--------|---------------|
| Unit tests | `Origo.GodotAdapter.Tests` | Pure .NET (`Microsoft.NET.Sdk`) | Core abstraction logic, type serialization, path handling, console commands |
| Integration tests | `Origo.GodotAdapter.Integration.Tests` | Godot `--headless` (`Godot.NET.Sdk`) | Real file I/O, Node instantiation, bootstrap properties, engine API availability |

## Capability Document Index

| Capability | Document | Files | Tests | Verification Focus |
|------------|----------|-------|-------|--------------------|
| Architecture Guardrails | [Architecture.md](Architecture.en.md) | 1 | 7 | SndContext public role interface completeness, session creation/destruction, CommandHandlerBase public visibility, GodotSndEntity internal lifecycle guard, GodotSndManager write-path sealing |
| SND Entities | [Snd.md](Snd.en.md) | 3 | 23 | SndEntityCollection full capability with batch recovery rollback, TypedDataInitializer forced loading, node extension contracts |
| Console | [Console.md](Console.en.md) | 5 | 28 | press_button / camera_view / tree_debug commands, CommandHandlerBase argument validation and guards, ProjectionHelper world→screen projection |
| File System | [FileSystem.md](FileSystem.en.md) | 1 | 3 | GodotFileSystem res:// / user:// path handling (delegated to PathUtility) |
| Logging | [Logging.md](Logging.en.md) | 1 | 9 | GodotLogger delegate injection, null handler safety and level filtering |
| Serialization | [Serialization.md](Serialization.en.md) | 5 | 57 (incl. 6 Benchmark) | 14 Godot type serialization round-trips + per-type generated accessor coverage + TypedData multi-layer inlining + performance benchmarks |
| Test Support | [TestSupport.md](TestSupport.en.md) | 2 | — (infrastructure, no [Fact]) | NullFileSystem, TestSndSceneHost, InMemorySndEntity, TestLogger, TestRuntimeHelper |

---

[↑ Back to Origo.manual](../README.en.md)
