# Strategy (Kernel)

> [↑ Back to Snd Kernel](../README.en.md)

Kernel strategy pool, managers, ordering, and observer topology implementation.

## Included Files

| File | Responsibility |
|------|----------------|
| `ActiveStrategyManager.cs` | `internal` per-entity active-strategy container: `Dictionary` index lookup for O(1) access, independent of the passive manager and excluded from frame traversal |
| `LifecycleStrategyOrder.cs` | `internal` validates `Before` / `After` ordering declarations and projects a deterministic order over the complete lifecycle registry |
| `ObserverBindingEntry.cs` | `internal` single observer binding: observer/target identity, the pooled strategy instance, observed data keys, and the wrapper callbacks used on cleanup |
| `ObserverStrategyMetadata.cs` | `internal` cached reader of the `[ObserveData]` declarations on an observer strategy type |
| `ObserverTopology.cs` | `internal` per-scene-host observer binding topology: bidirectional mount/unmount orchestration, save/load recovery, and teardown on death |
| `SndStrategyManager.cs` | `internal` per-entity passive strategy container: partial-order frame iteration, hook triggering, and all-or-nothing recovery |
| `SndStrategyPool.cs` | `internal` ref-counted strategy pool: explicit registration, generic `GetStrategy{TBase}` acquisition, and fail-fast on index/type mismatch |

Observer teardown aggregates per-binding failures: if one binding's `OnUnmounted` or unsubscribe throws, the remaining bindings are still unsubscribed and their pool references released, then the first failure (or an `AggregateException` for several) is rethrown. `Unmount` applies the same rule within one binding. A binding removed during that cleanup is also skipped by any data-notification pass already in flight: the target's subscription list carries a removed flag, so no `OnDataChanged` fires for a binding after its `OnUnmounted` cleanup ran. Spawn-failure rollback additionally removes incoming bindings that already-fired sibling entities mounted onto the rolled-back entity (before and after the outgoing-binding teardown, bounded three attempts per pass), so a persisted `ObserverIndices` entry only references a missing entity in the pathological case where an `OnUnmounted` hook keeps remounting forever: cleanup then gives up after the bound and strict save recovery rejects that save on load.

---
[↑ Back to Snd Kernel](../README.en.md)
