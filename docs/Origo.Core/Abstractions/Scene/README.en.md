<!-- docsync-pair: Origo.Core/Abstractions/Scene/README -->
<!-- docsync-revision: 3 -->
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

### IOwningSessionBindable

| Member | Description |
|------|------|
| `SetOwningSession(ISessionRun session)` | Called by `SessionRun` at construction; binds the session to the host, after which every entity the host creates is automatically bound to the session's `ISndEntity.OwningSession` |

## Design Decisions

### Why separate ISndSceneAccess and ISndSceneHost
The state machine context only needs build/recovery; session management needs entity container operations. ISP separation keeps dependencies precise.

### Why scene host does not trigger strategy hooks
All hook orchestration is handled by the session lifecycle (`SndEntityFactory` / `SessionRun`), keeping adapter layer out of strategy lifecycle management and enabling batch operations between create/recover and hook trigger phases.

### Why CreateEntity performs no duplicate-name check
`CreateEntity` keeps minimal semantics and does not enforce name uniqueness at the interface level; the framework's spawn path does not force uniqueness either. The interface does not take on business-validation duties, leaving them to upper-layer business rules when needed.

### Why kill is split into RequestKillEntity (mark) and RemoveEntity (disassemble)
`RequestKillEntity` immediately marks the entity as pending kill (`IsPendingKill = true`) without physically removing it. This lets subsequent same-frame operations judge the entity's liveness via `IsPendingKill`, avoiding duplicate operations on a deferred kill. Physical destruction happens at end of frame via `SessionRun.KillPending()` (invoked per session by `SessionManager.KillPendingAllSessions()`, after the business queue and before the system queue): observer bindings are torn down bidirectionally first, then `BeforeDead` hooks fire in batch, strategies are released, and finally each entity is removed via `RemoveEntity`.

---
[↑ Back to Abstractions](../README.en.md)
