# Session Model

> [↑ Back to usage](README.en.md)

## Overview

Origo supports a model with both **foreground sessions** and **background sessions** coexisting. Both session types are expressed through the same `ISessionRun` interface; the difference lies only in internal implementation and the `IsFrontSession` flag.

## Foreground vs. Background

| Property | Foreground Session | Background Session |
|----------|-------------------|-------------------|
| Key name | `__foreground__` (fixed) | User-defined (e.g., `"dungeon"`) |
| Count | At most one | Multiple allowed |
| Scene host | GodotSndManager (engine rendering) | FullMemorySndSceneHost (no rendering) |
| Strategy access | `entity.OwningSession` (direct to owning session) | `entity.OwningSession` (direct to owning session) |
| State machine | Session-level StateMachineContainer | Session-level StateMachineContainer |
| Blackboard | Independent SessionBlackboard | Independent SessionBlackboard |

## Interfaces

Both session types are expressed through the same two contracts; the full
member lists live in the `ISessionManager / ISessionRun` section of
[agent-reference](agent-reference.en.md).

- `ISessionRun` — one mounted session: its session blackboard, level id, and
  front/background flag; its session-scoped state machines; its
  `ISessionManager` for cross-session access; and the entity operations scoped
  to that session (`FindByName`, `GetEntities`, `Spawn`, `SpawnMany`,
  `RequestKillEntity`).
- `ISessionManager` — the session container: foreground access, background
  session creation and destruction by key, `ProcessAllSessions` for background
  iteration, and `KillPendingAllSessions` for frame-end draining.

## Creating Background Sessions

```csharp
var bgSession = sessionManager.CreateBackgroundSession(
    key: "dungeon",
    levelId: "dungeon_level",
    syncProcess: true);  // Participate in Process frame updates

// Background sessions have independent entities, blackboards, and state machines
bgSession.Spawn(dungeonEntityMeta);
bgSession.SessionBlackboard.SetValue("explored", true);
```

**levelId identity and uniqueness:** Level IDs are lowercase, path-safe tokens (the same identity rule as save IDs) because they name `level_{id}/`; at any given time a levelId can only be held by one session. Mixed-case or trailing-dot level IDs are rejected before any session is created. If attempting to create a background session while a foreground or another background session already uses that levelId, `CreateBackgroundSession` throws `InvalidOperationException`. Replacing the foreground slot itself is not a concurrent conflict: `CreateForegroundSession` validates conflicts with other sessions first, then destroys the old foreground and mounts the replacement.

Before executing, `SwitchForeground` **automatically checks** whether a background session holds the target `levelId`. If a conflict exists, it saves that background session's data to `current/`, destroys it, and then creates a new foreground session. The caller does not need to manually destroy the background session.

If you need explicit flow control (e.g., you don't want to save the background's current state), you can still manually destroy the background before calling switch:

```csharp
// Method 1: Automatic (recommended) — SwitchForeground handles save + destroy internally
var bg = sessionManager.CreateBackgroundSession("gen", "game", false);
bg.Spawn(new SndMetaData { Name = "entity" });
bg.SessionBlackboard.SetValue("data", 1);

// Direct switch; SwitchForeground automatically saves and destroys bg
ctx.Save.RequestSwitchForegroundLevel("game");
((IOrigoFrameDriver)runtime).DriveFrame(0); // host frame boundary executes deferred requests

// Method 2: Manual control — caller explicitly saves and destroys step by step for finer control
var bg = sessionManager.CreateBackgroundSession("gen", "game", false);
bg.Spawn(new SndMetaData { Name = "entity" });

ctx.Save.RequestSaveGameAuto();
((IOrigoFrameDriver)runtime).DriveFrame(0); // host frame boundary executes deferred requests

sessionManager.DestroySession("gen");
ctx.Save.RequestSwitchForegroundLevel("game");
((IOrigoFrameDriver)runtime).DriveFrame(0); // host frame boundary executes deferred requests
```

## Session Topology

Relationships between concurrent sessions are encoded/decoded via `SessionTopology`. The encoding format (stored in the Progress blackboard):

```
key=levelId=syncProcess,key=levelId=syncProcess
```

Example: `__foreground__=town=false,dungeon=dungeon_level=true,farm=farm_level=false`

The foreground entry is always first and background entries are written in ordinal session-key order; the same session set produces the same topology string and payload hash regardless of creation order. Due to the levelId uniqueness constraint, no two entries in the topology will point to the same levelId.

On load recovery, `SessionTopologyCodec` parses this string and rebuilds all background sessions. The topology and the persisted level payload set must agree exactly: a duplicated levelId, a referenced level without a payload, or a payload level not referenced by the topology fails the strict read before any session is mounted. This ensures a corrupted save cannot silently drop or re-key a background level.

## State Machine Context

State machine strategy hooks access the blackboard via `IStateMachineContext`:

```csharp
public interface IStateMachineContext : ISndBlackboardAccess, ISndDeferredActions
{
    IBlackboard SystemBlackboard { get; }     // System level (inherits ISndBlackboardAccess)
    IBlackboard? ProgressBlackboard { get; }  // Progress level (inherits ISndBlackboardAccess)
    IBlackboard? SessionBlackboard { get; }   // Current session level
    ISndSceneReadAccess SceneAccess { get; }      // Current session scene
    void EnqueueBusinessDeferred(Action action);           // Inherits ISndDeferredActions
    int GetPendingPersistenceRequestCount();               // Inherits ISndDeferredActions
    bool IsPersistenceIdle { get; }                        // Inherits ISndDeferredActions
}
```

`SessionStateMachineContext` is a session-level adapter that ensures foreground/background session state machine hooks receive their respective `SessionBlackboard` and `SceneAccess` — no semantic divergence between foreground and background.

## Lifecycle

```
Create background session:
  SessionManager.CreateBackgroundSession(key, levelId)
    → Validate key and levelId do not conflict with existing sessions
    → Create SessionRun
    → Inject FullMemorySndSceneHost (SessionManager internally news it)
    → Mount into _sessions dictionary
    → Optional: restore SessionBlackboard + state machines + entities from save

Create foreground session:
  SessionManager holds the adapter-injected scene host at construction time
  SessionManager.CreateForegroundSession(levelId)    // foreground host injected at SessionManager construction
    → Use the stored adapter scene host to create SessionRun
    → Mount into _sessions dictionary under `__foreground__`
    → The adapter scene host is only used by the foreground session; background sessions always use FullMemorySndSceneHost

Running:
  SessionManager.ProcessAllSessions(delta)
    → Iterate all background sessions with syncProcess=true (and the foreground session)
    → Drive entity frame processing of SceneHost through SessionRun internally

Level switching:
  RequestSwitchForegroundLevel(newLevelId)
    → Enqueued in system deferred queue, FIFO (after Save)
    → PersistForegroundLevelState (explicitly persist old foreground level data to current/)
    → PersistAndDestroyBackgroundIfExists (if a background session holds the target levelId, save then destroy)
    → ResetForeground(true) (destroy current foreground; Dispose does not implicitly persist)
    → LoadAndMountForeground (create new foreground, parse new level data from current/)
    → PersistProgress (write full session topology to current/)

Destroy:
  SessionManager.DestroySession(key)
    → Dispose SessionRun (only cleanup resources: unmount, pop state machines, clean entities and blackboard)
    → Remove from dictionary
    → Release the levelId held by that session

Exit:
  SndContext.Shutdown() (or OrigoHost.Dispose / GodotSndManager._ExitTree / OrigoDefaultEntry._ExitTree)
    → ProgressRun dispose
    → Destroy all background sessions and foreground session in order
    → Clean up current/ temp directory
    → Does not trigger persistence (saving is the application layer's responsibility via explicit RequestSaveGame)
```

## Design Principles

- Foreground and background share the same interface, no type branching
- The management layer fully manages lifecycles; the session layer only exposes internal state
- Background sessions get full strategy capabilities via `FullMemorySndSceneHost`, but without rendering
- Level switching executes in the system deferred queue, FIFO alongside Save operations. When `RequestSaveGameAuto` is followed by `RequestSwitchForegroundLevel` in the same frame, Save writes to `current/` first, and Switch finds the data when loading
- **levelId uniqueness**: Each levelId is held by at most one session at any time. If a background session conflicts with the target foreground levelId, `SwitchForeground` automatically saves and destroys that background session before switching
- **Dispose does not persist**: `ISessionRun.Dispose` and `ProgressRun.Dispose` are only responsible for resource cleanup; they trigger no persistence operations. Old foreground data during level switching is persisted by `SwitchForeground`'s explicit `PersistSession` call. To save progress before exit, the application layer should explicitly call `RequestSaveGame`

## Related Documents

- [Persistence Flow](persistence-flow.en.md) — Saving/loading session data
- [State Machine](state-machine.en.md) — Session-level state machines

---
[↑ Back to usage](README.en.md)
