<!-- docsync-pair: Origo.Core/Abstractions/Scene/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Scene (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Snd/Scene](../../Snd/Scene/README.en.md)

## Overview
Defines the Core layer's abstract capabilities for orchestrating SND scenes. The four orchestration interfaces (`ISndSceneAccess`, `ISndSceneHost`, `ISndContextAttachableSceneHost`, `IOwningSessionBindable`) are `internal` — visible only to the Core session lifecycle and adapter assemblies granted `InternalsVisibleTo`. The only business-visible scene interface is the read-only `ISndSceneReadAccess`.

## Included Files

| File | Responsibility |
|------|------|
| `ISndSceneReadAccess.cs` | Public read-only scene access: `GetEntities` / `FindByName`; state-machine hooks and save-meta contributors query scenes through this |
| `ISndSceneAccess.cs` | Internal scene serialization access: BuildMetaList / RecoverFromMetaList (no hooks) |
| `ISndSceneHost.cs` | Internal scene host (inherits ISndSceneAccess + ISndSceneReadAccess): entity container management |
| `IOwningSessionBindable.cs` | Internal owning-session binding for auto-binding entities |
| `ISndContextAttachableSceneHost.cs` | Internal context binding (`BindContext`) driven by `SndContext` / `SessionRun` startup orchestration |

## Interface Details

### ISndSceneReadAccess (public)

| Member | Description |
|------|------|
| `GetEntities()` | Snapshot of all currently alive entities |
| `FindByName(name)` | Look up an entity by stable name; null when not found |

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

### Why separate read-only access from orchestration interfaces
State-machine contexts and save-meta contributors only need to query entities (`GetEntities` / `FindByName`); they must not touch create/recover/remove orchestration. Separating the public `ISndSceneReadAccess` from the internal `ISndSceneAccess` / `ISndSceneHost` prevents business code from casting `GodotSndManager` to bypass hook orchestration, while save and session systems keep full internal access.

### Why scene host does not trigger strategy hooks
All hook orchestration is handled by the session lifecycle (`SndEntityFactory` / `SessionRun`), keeping adapter layer out of strategy lifecycle management and enabling batch operations between create/recover and hook trigger phases.

### Why CreateEntity performs no duplicate-name check
`CreateEntity` keeps minimal semantics and does not enforce name uniqueness at the host-interface level; uniqueness is enforced centrally by the orchestration layers (`SndEntityFactory` and `SndSceneSerializer`) before the host is called. Hosts may assume names are unique within the scene.

### Why kill is split into RequestKillEntity (mark) and RemoveEntity (disassemble)
`RequestKillEntity` immediately marks the entity as pending kill (`IsPendingKill = true`) without physically removing it. This lets subsequent same-frame operations judge the entity's liveness via `IsPendingKill`, avoiding duplicate operations on a deferred kill. Physical destruction happens at end of frame via `SessionRun.KillPending()` (invoked per session by `SessionManager.KillPendingAllSessions()`, after the business queue and before the system queue): observer bindings are torn down bidirectionally first, then `BeforeDead` hooks fire in batch, strategies are released, and finally each entity is removed via `RemoveEntity`.

---
[↑ Back to Abstractions](../README.en.md)
