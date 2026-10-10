# StateMachine (Contracts)

> [↑ Back to Origo.Core.Contracts](../README.en.md)

Stable state-machine strategy base and its per-operation context; concrete stack machine implementation lives in `Origo.Core.Kernel`.

## Included Files

| File | Responsibility |
|------|----------------|
| `StateMachineStrategyBase.cs` | Strategy base bound to a string-stack state machine: `OnPushRuntime` / `OnPushAfterLoad` / `OnPopRuntime` / `OnPopBeforeQuit` hooks taking `IStateMachineContext` |
| `StateMachineStrategyContext.cs` | `readonly struct` callback context for one state-machine operation: `MachineKey`, `BeforeTop`, and `AfterTop` stack-top snapshots |

---
[↑ Back to Origo.Core.Contracts](../README.en.md)
