<!-- docsync-pair: Origo.TestSupport/Observer/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Observer

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

观察者策略测试的事件采集基础设施。提供类型化的事件记录类型，用于验证观察者钩子（`OnMounted`、`OnDataChanged`、`OnUnmounted`）的调用时序和参数。

## 包含文件

| 文件 | 职责 |
|------|------|
| `TestObserverEvents.cs` | 定义 `TestObserverEvent` 记录（事件类型、目标名、数据键、旧值、新值）与静态 `EventCollector` 收集器（基于 `AsyncLocal`，随测试上下文隔离）。事件类型为小写字符串（`"on_mounted"` / `"on_unmounted"` / `"on_data_changed"`）。 |

## 使用模式

```csharp
var events = new List<TestObserverEvent>();
EventCollector.Events = events;
// ... mount observer, trigger data changes ...
Assert.Contains(events, e => e.EventType == "on_data_changed");
```

---

[↑ 回到 TestSupport](../README.zh.md)
