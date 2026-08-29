<!-- docsync-pair: Origo.Core/Snd/Companions/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Companions

> [↑ Back to Snd](../README.en.md)

## Overview
The companion object layer for `SndContext`. Each companion is `internal sealed class` implementing one role interface exposed by `ISndContext`. Most companions hold a back-reference to `SndContext` to access framework internal state; `SndContextFileAccess` / `SndContextArchiveFileAccess` are the exception — they inject their I/O dependencies directly (`IDataSourceIoGateway`, `IFileMetaAccess`, `IPathResolver`, etc.) and do not reference `SndContext`. `SndContextTemplateAccess` additionally loads template/alias maps and resolves entity-list JSON files through the I/O dependencies held by `SndContext`.

## Included Files

| File | Implements | Corresponding Property |
|------|-----------|------------------------|
| `SndContextBlackboardAccess.cs` | `ISndBlackboardAccess` | `ISndContext.Blackboard` |
| `SndContextDeferredActions.cs` | `ISndDeferredActions` | `ISndContext.Deferred` |
| `SndContextTemplateAccess.cs` | `ISndTemplateAccess` | `ISndContext.Template` |
| `SndContextConsoleAccess.cs` | `ISndConsoleAccess` | `ISndContext.ConsoleAccess` |
| `SndContextStateMachineAccess.cs` | `ISndStateMachineAccess` | `ISndContext.StateMachines` |
| `SndContextSaveOperations.cs` | `ISndSaveOperations` | `ISndContext.Save` |
| `SndContextLifecycleOperations.cs` | `ISndLifecycleOperations` | `ISndContext.Lifecycle` |
| `SndContextStateMachineContext.cs` | `IStateMachineContext` | `ISndContext.StateMachineContext` |

`SndContextFileAccess.cs` and `SndContextArchiveFileAccess.cs` reside at the `Snd/` layer.

## Design Decisions

### Why companion objects
`SndContext` aggregates 10+ role capabilities. Companion objects keep `ISndContext` clean — it only exposes companion properties without inheriting any role interface. Most companions hold a back-reference to access framework internal state; the two file-access companions inject their I/O dependencies directly (no `SndContext` reference).

### Why companions are internal
Framework implementation details. Strategies access via `ISndContext` companion properties; marking as `internal` prevents hard dependencies on concrete implementation types.

### Why companions back-reference SndContext
Companions need access to `SndContext` internal state (`_systemRun`, `_progressRun`, etc.). Direct reference avoids exposing internals as public interface members.

---
[↑ Back to Snd](../README.en.md)
