<!-- docsync-pair: Origo.Core/Abstractions/Lifecycle/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Lifecycle (Abstractions)

> [↑ Back to Abstractions](../README.en.md) · [↔ Implementation: Lifecycle](../../Runtime/Lifecycle/README.en.md)

## Overview
Defines abstract interfaces for session management. `ISessionManager` and `ISessionRun` provide the strategy layer with session access capabilities decoupled from concrete implementations. These interfaces reside in the Abstractions layer, ensuring `ISndEntity` (which declares `OwningSession`) does not depend on the Runtime layer.

## Included Files

| File | Responsibility |
|------|------|
| `ISessionManager.cs` | Session manager: create/destroy/find sessions, manage full lifecycle of foreground and background sessions |
| `ISessionRun.cs` | Session runtime (does not inherit `IDisposable`; destruction goes through `ISessionManager.DestroySession` only): SessionBlackboard + entity operation facade (FindByName/GetEntities/Spawn/SpawnMany/RequestKillEntity) + SessionManager + LevelId + IsFrontSession + state machine container |

## ISessionManager Members

| Member | Description |
|------|------|
| `ForegroundKey` | Reserved key for foreground session (`"__foreground__"`) |
| `CanCreateSessions` | Whether sessions can currently be created. `EmptySessionManager` returns `false` |
| `ForegroundSession` | Current foreground session; null when none active |
| `Keys` | All mounted session keys |
| `TryGet(key)` | Get session by key |
| `Contains(key)` | Check if session exists |
| `CreateBackgroundSession(key, levelId, syncProcess)` | Create background level session |
| `DestroySession(key)` | Dispose and remove session |
| `ProcessAllSessions(delta, includeForeground)` | Frame update on Process-participating sessions |
| `KillPendingAllSessions()` | Trigger observer unbind + BeforeDead hooks + physical removal for all sessions |

## ISessionRun Members

| Member | Description |
|------|------|
| `SessionBlackboard` | Session-level blackboard |
| `LevelId` | Level unique identifier |
| `IsFrontSession` | Whether this is the foreground session |
| `GetSessionStateMachines()` | Session-level state machine container |
| `SessionManager` | Owning ISessionManager for cross-session access |
| `FindByName(name)` | Find entity by name in current session |
| `GetEntities()` | All alive entities in current session |
| `Spawn(meta)` | Create entity + trigger AfterSpawn |
| `SpawnMany(metaList)` | Batch create + uniform AfterSpawn |
| `RequestKillEntity(name)` | Mark entity as pending destruction |

## Design Decisions

### Why ISessionRun/ISessionManager in Abstractions layer
Defined here so `ISndEntity.OwningSession` can reference `ISessionRun` without depending on Runtime layer. Concrete implementations remain in Runtime.

### Why GetSessionStateMachines() returns IStateMachineContainer
Returns the Abstractions-layer interface, not concrete `StateMachineContainer`, maintaining abstraction consistency.

### Why CanCreateSessions property
`EmptySessionManager` (Null Object) violates LSP when its `CreateBackgroundSession` throws. `CanCreateSessions` lets consumers check capability first.

---
[↑ Back to Abstractions](../README.en.md)
