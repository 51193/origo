# Entity (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Snd/Entity](../../Snd/Entity/README.en.md)

## Overview
Framework-internal SND entity lifecycle contract. `IEntityLifecycle` is an `internal` interface defined separately for framework/adapter-layer shared implementation. The public entity role interfaces following ISP (`ISndDataAccess`, `ISndNodeAccess`, `ISndStrategyAccess`, `ISndActiveStrategyAccess`, `ISndObserverStrategyAccess`, composed by `ISndEntity`) live in [Origo.Core.Contracts/Abstractions/Entity](../../../Origo.Core.Contracts/Abstractions/Entity/README.en.md).

## Included Files

| File | Responsibility |
|------|------|
| `IEntityLifecycle.cs` | `internal` framework-internal lifecycle: phased recovery/hook/teardown methods. Implemented by `SndEntity` (kernel in-memory entity) and adapter-layer entities (bridging to an inner `SndEntity`); adapter and test projects access it via `InternalsVisibleTo` |

## Interface Details

### IEntityLifecycle

**`internal` framework-internal interface** for two-phase batch orchestration by `SndEntityFactory` and `SessionRun`, also implemented by adapter-layer entities (such as `GodotSndEntity`) for bridge delegation. Business code must not call it directly and cannot reference it outside the Kernel assembly (adapter and test projects access it via `InternalsVisibleTo`).

| Method | Phase | Description |
|------|------|------|
| `RecoverForLifecycle(meta)` | Phase 1 | Recover Name + Data + Node + EntityStrategy + ActiveStrategy; does not trigger any hooks. On failure, rolls back atomically across phases: acquired strategy references are returned to the pool and created nodes are freed before the exception propagates |
| `FireAfterSpawnHooks()` | Phase 2 | Trigger AfterSpawn |
| `FireAfterLoadHooks()` | Phase 2 | Trigger AfterLoad |
| `FireBeforeSaveHooks()` | Phase 2 | Trigger BeforeSave |
| `FireBeforeQuitHooks()` | Phase 2 | Trigger BeforeQuit |
| `FireBeforeDeadHooks()` | Phase 2 | Trigger BeforeDead |
| `ReleaseStrategiesOnly()` | Phase 3 | Release passive + active + observer strategy references (no hooks fired) |
| `TeardownOnly()` | Phase 3 | Release Node + Data resources |
| `TeardownObserverBindings()` | Phase 3 | Unload all observer bindings of this entity via the host `ObserverTopology`: unsubscribe target data channels, fire `OnUnmounted`, and release strategy-pool references |
| `BuildMetaData()` | Serialization | Build metadata (no BeforeSave) |

Implementers: `SndEntity` (kernel in-memory entity) and adapter-layer entities (such as `GodotSndEntity`, which bridges by delegating to an inner `SndEntity`).

## Design Decisions

### Why IEntityLifecycle defined separately

Lifecycle hook triggering is controlled by the framework layer. Putting `RecoverForLifecycle`, `FireXxxHooks`, `ReleaseStrategiesOnly`, `TeardownOnly` in a separate interface:
- keeps `ISndEntity` (business-facing) free of lifecycle orchestration capability
- lets `SndEntityFactory` and `SessionRun` orchestrate batches uniformly through `IEntityLifecycle`
- lets Godot adapter's `GodotSndEntity` implement the interface by delegating to an inner `SndEntity`

---
[↑ Back to Abstractions](../README.en.md)
