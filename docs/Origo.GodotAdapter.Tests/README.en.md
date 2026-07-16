<!-- docsync-pair: Origo.GodotAdapter.Tests/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Origo.GodotAdapter.Tests

> [↑ Back to Origo.manual](../README.en.md)

## Test Strategy Overview

The tests for Origo.GodotAdapter verify the correctness of the Godot 4 adapter layer.
The adapter bridges Core's abstract interfaces with Godot engine APIs; test focus includes:
file system path handling (`res://` / `user://` virtual paths), Godot type serialization round-trips,
adapter-layer console command extensions, bootstrap orchestration, and log proxying.

Since GodotAdapter depends on the Godot engine runtime, production source files involving Godot APIs
such as `Godot.Node` / `PackedScene` (e.g., `GodotSndEntity.cs`, `GodotNodeHandle.cs`,
`GodotPackedSceneNodeFactory.cs`) are excluded by coverlet (configured in `.csproj` via `ExcludeByFile`),
and their corresponding logic cannot be directly covered in unit tests.

Behavior verification for engine-dependent files is performed by
[Origo.GodotAdapter.Integration.Tests](../Origo.GodotAdapter.Integration.Tests/README.en.md)
in a real Godot `--headless` runtime. Integration tests and unit tests complement each other:
unit tests verify Core-layer logic and type serialization; integration tests verify Godot-specific
runtime behavior (real file system, Node lifecycle, bootstrap orchestration).

## Test Layers

| Layer | Project | Runtime | Coverage Scope |
|-------|---------|--------|---------------|
| Unit tests | `Origo.GodotAdapter.Tests` | Pure .NET (`Microsoft.NET.Sdk`) | Core abstraction logic, type serialization, path handling, console commands |
| Integration tests | `Origo.GodotAdapter.Integration.Tests` | Godot `--headless` (`Godot.NET.Sdk`) | Real file I/O, Node instantiation, bootstrap properties, engine API availability |

## Capability Document Index

| Capability | Document | Files | Tests | Verification Focus |
|------------|----------|-------|-------|--------------------|
| Architecture Guardrails | [Architecture.md](Architecture.en.md) | 1 | 3 | SndContext public role interface completeness, session creation/destruction, CommandHandlerBase public visibility |
| Console | [Console.md](Console.en.md) | 4 | 22 | press_button / camera_view commands, CommandHandlerBase argument validation and guards, ProjectionHelper world→screen projection |
| File System | [FileSystem.md](FileSystem.en.md) | 1 | 3 | GodotFileSystem res:// / user:// path handling (delegated to PathUtility) |
| Logging | [Logging.md](Logging.en.md) | 1 | 9 | GodotLogger delegate injection, null handler safety and level filtering |
| Serialization | [Serialization.md](Serialization.en.md) | 4 | 50 (incl. 6 Benchmark) | 14 Godot type serialization round-trips + TypedData multi-layer inlining + performance benchmarks |
| Test Support | [TestSupport.md](TestSupport.en.md) | 2 | — (infrastructure, no [Fact]) | NullFileSystem, TestSndSceneHost, InMemorySndEntity, TestLogger, TestRuntimeHelper |

---

[↑ Back to Origo.manual](../README.en.md)
