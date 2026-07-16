<!-- docsync-pair: Origo.TestSupport/Observer/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Observer

> [↑ Back to TestSupport](../README.en.md)

## Overview

Event collection infrastructure for observer strategy testing. Provides a typed event record type for verifying the invocation sequence and arguments of observer hooks (`OnMounted`, `OnDataChanged`, `OnUnmounted`).

## File Inventory

| File | Responsibility |
|------|---------------|
| `TestObserverEvents.cs` | Defines `TestObserverEvent` record (event type, target name, data key, old value, new value) and a `TestObserverEvents` collector class backed by a thread-safe `ConcurrentQueue`. |

## Usage Pattern

```csharp
var events = new TestObserverEvents();
// ... mount observer, trigger data changes ...
Assert.Contains(events.Events, e => e.EventType == "OnDataChanged");
```

---

[↑ Back to TestSupport](../README.en.md)
