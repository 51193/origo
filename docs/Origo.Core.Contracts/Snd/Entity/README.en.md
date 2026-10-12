# Entity (Contracts)

> [↑ Back to Snd Contracts](../README.en.md)

Internal entity capability contracts shared by public strategy/planning base classes and the kernel/adapter implementations through `InternalsVisibleTo`.

## Included Files

| File | Responsibility |
|------|----------------|
| `ISndEntityRawSubscription.cs` | `internal` raw `TypedData`-level subscription surface (`SubscribeDataRaw` / `UnsubscribeDataRaw`) used by the kernel `ObserverTopology` to wire observer strategies into target data changes |
| `ISndEntityStrategyQuery.cs` | `internal` query surface adding `HasStrategyMounted(index)`, letting strategy base classes detect an already-mounted strategy without depending on the concrete entity implementation |

---
[↑ Back to Snd Contracts](../README.en.md)
