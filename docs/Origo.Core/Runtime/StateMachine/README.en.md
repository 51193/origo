<!-- docsync-pair: Origo.Core/Runtime/StateMachine/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# StateMachine (Runtime)

> [↑ Back to Runtime](../README.en.md) · [↔ Implementation: StateMachine](../../StateMachine/README.en.md)

## Overview
Runtime wrapper for the state machine container. `StateMachineContainer` implements `IStateMachineContainer`, managing multiple `StackStateMachine` instances by key.

## Included Files

| File | Responsibility |
|------|------|
| `StateMachineContainer.cs` | Container: CreateOrGet / TryGet / Remove / Clear / serialization |

## Module Details

### StateMachineContainer : IStateMachineContainer
- Exposed as `IStateMachineContainer` to `ISessionRun`; internal methods for Runtime layer use
- `ForEachMachine` unifies iteration for batch operations

**Core Operations**: `CreateOrGet`, `TryGet`, `Remove`, `Clear`, `SerializeToNode`, `DeserializeFromNode`

**Batch Operations**: `FlushAllAfterLoad`, `PopAllRuntime`, `PopAllOnQuit`

**Deserialization Strategy**:
1. Parse payload, validate entries
2. Create new machines and RestoreStackWithoutHooks
3. Dispose new machines on exception
4. On success, atomically replace old machines

## Design Decisions

### Why key order is preserved during serialization
After save restore, hooks replay in original order. `_machineOrder` tracks insertion order.

### Why deserialization uses atomic replacement
Full replacement ensures each state machine's reference count is consistent. Partial replacement risks ref-count chaos.

### Why CreateOrGet throws on strategy index mismatch
Push/Pop strategy indices are the core behavior definition. Silently returning different behavior would be dangerous.

### Why PopAllRuntime remains an internal batch operation
`PopAllOnQuit` is invoked by the session-quit pipeline; `PopAllRuntime` provides the matching runtime bulk-clear semantic. It is intentionally absent from `IStateMachineContainer` and remains an internal batch utility on the concrete container for framework orchestration and tests, preventing business code from mistaking "pop all machines" for a cross-session cleanup shortcut.

---
[↑ Back to Runtime](../README.en.md)
