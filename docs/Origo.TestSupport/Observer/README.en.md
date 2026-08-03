<!-- docsync-pair: Origo.TestSupport/Observer/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Observer

> [↑ Back to TestSupport](../README.en.md)

## Overview

Event collection infrastructure for observer strategy testing. Provides a typed event record type for verifying the invocation sequence and arguments of observer hooks (`OnMounted`, `OnDataChanged`, `OnUnmounted`).

## File Inventory

| File | Responsibility |
|------|---------------|
| `TestObserverEvents.cs` | Defines the `TestObserverEvent` record (event type, target name, data key, old value, new value) and the static `EventCollector` collector (backed by `AsyncLocal`, isolated per test context). Event types are lowercase strings (`"on_mounted"` / `"on_unmounted"` / `"on_data_changed"`). |

## Usage Pattern

```csharp
var events = new List<TestObserverEvent>();
EventCollector.Events = events;
// ... mount observer, trigger data changes ...
Assert.Contains(events, e => e.EventType == "on_data_changed");
```

---

[↑ Back to TestSupport](../README.en.md)
