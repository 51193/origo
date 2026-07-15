<!-- docsync-pair: Origo.Core/Abstractions/Scene/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Scene (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Snd/Scene](../../Snd/Scene/README.en.md)

## Overview
Defines the Core layer's abstract capabilities for orchestrating SND scenes. `ISndSceneAccess` provides minimal build/recovery operations (no hooks), `ISndSceneHost` adds entity container management, and `IOwningSessionBindable` allows session binding for automatic owning session assignment.

## Included Files

| File | Responsibility |
|------|------|
| `ISndSceneAccess.cs` | Minimal scene access: BuildMetaList / RecoverFromMetaList (no hooks) |
| `ISndSceneHost.cs` | Scene host (inherits ISndSceneAccess): entity container management |
| `IOwningSessionBindable.cs` | SetOwningSession for auto-binding entities |

## Interface Details

### ISndSceneAccess

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

## Design Decisions

### Why separate ISndSceneAccess and ISndSceneHost
The state machine context only needs build/recovery; session management needs entity container operations. ISP separation keeps dependencies precise.

### Why scene host does not trigger strategy hooks
All hook orchestration is handled by the session lifecycle (`SndEntityFactory` / `SessionRun`), keeping adapter layer out of strategy lifecycle management and enabling batch operations between create/recover and hook trigger phases.

---
[↑ Back to Abstractions](../README.en.md)
