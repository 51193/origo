# Scene (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Snd/Scene](../../Snd/Scene/README.en.md)

## Overview
Defines the Core layer's abstract capabilities for orchestrating SND scenes. `ISndSceneReadAccess` is the public read-only view; `ISndSceneAccess`, `ISndSceneHost`, and `IOwningSessionBindable` are `internal` — visible only to the Core session lifecycle and adapter assemblies granted `InternalsVisibleTo`. The context-binding interface `ISndContextAttachableSceneHost` lives in [Snd/Scene](../../Snd/Scene/README.en.md).

## Included Files

| File | Responsibility |
|------|------|
| `ISndSceneAccess.cs` | Internal scene serialization access: BuildMetaList / RecoverFromMetaList (no hooks) |
| `ISndSceneHost.cs` | Internal scene host (inherits ISndSceneAccess + ISndSceneReadAccess): entity container management |
| `IOwningSessionBindable.cs` | Internal owning-session binding for auto-binding entities |

## Interface Details

> `ISndSceneReadAccess` (the public read-only scene contract) lives in [Origo.Core.Contracts/Abstractions/Scene](../../../Origo.Core.Contracts/Abstractions/Scene/README.en.md).

### ISndSceneAccess (internal)

| Member | Description |
|------|------|
| `BuildMetaList()` | Collect entity metadata (no BeforeSave) |
| `RecoverFromMetaList(metaList)` | Recover entities from metadata (no AfterLoad) |

### ISndSceneHost : ISndSceneAccess

| Own Members | Description |
|------|------|
| `CreateEntity(SndMetaData)` | Create entity (no hooks) |
| `GetEntities()` | All alive entities |
| `FindByName(name)` | Find entity by name |
| `ProcessAll(delta)` | Frame update |
| `RequestKillEntity(name)` | Mark pending kill |
| `RemoveEntity(name)` | Remove + release engine resources (no hooks) |
| `RemoveAllEntities()` | Clear collection (no hooks) |

### IOwningSessionBindable

| Member | Description |
|------|------|
| `SetOwningSession(ISessionRun session)` | Called by `SessionRun` at construction; binds the session to the host, after which every entity the host creates is automatically bound to the session's `ISndEntity.OwningSession` |

## Design Decisions

The design decisions for the scene abstractions live in [Origo.Core.Contracts/Abstractions/Scene](../../../Origo.Core.Contracts/Abstractions/Scene/README.en.md#design-decisions).

---
[↑ Back to Abstractions](../README.en.md)
