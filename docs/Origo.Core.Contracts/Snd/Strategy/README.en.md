# Strategy (Contracts)

> [↑ Back to Snd Contracts](../README.en.md)

Stable strategy base classes and attributes used by consumer strategy code. Strategy pool and manager implementations live in `Origo.Core.Kernel`.

## Included Files

| File | Responsibility |
|------|----------------|
| `ActiveStrategyBase.cs` | Abstract base for externally invoked strategies: `Invoke(entity, ctx, input)` with no frame or lifecycle participation |
| `ActiveStrategyJsonBase.cs` | JSON-payload active strategy base: deserializes `TInput` into `Execute`, serializes the result, and returns `err:Invalid request` for non-JSON input |
| `ActiveStrategyResults.cs` | Conventional active-strategy results: `Ok()` → `"ok"` and `Err(message)` → `"err:<message>"` |
| `BaseStrategy.cs` | Root base class of every strategy, carrying pool identity and the stateless constraint validated at registration: mutable instance fields and writable instance properties are rejected; a readonly field is allowed only when it holds a deeply immutable value (for example an injected logger) |
| `LifecycleStrategyBase.cs` | Entity lifecycle strategy base: `AfterSpawn` / `AfterLoad` / `AfterAdd` / `Process` / `BeforeRemove` / `BeforeSave` / `BeforeQuit` / `BeforeDead` virtual hooks |
| `ObserveDataAttribute.cs` | Repeatable class attribute declaring one observed data key (`DataKey`) for an observer strategy |
| `ObserverStrategyBase.cs` | Observer strategy base: `OnMounted` / `OnDataChanged` / `OnUnmounted` callbacks over the observed target entity |
| `StrategyIndexAttribute.cs` | Strategy identity: `Index` plus `Before` / `After` partial-order declarations read during auto-discovery |

---
[↑ Back to Snd Contracts](../README.en.md)
