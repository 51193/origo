<!-- docsync-pair: Origo.TestSupport/Scene/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Scene

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

`ISndSceneHost` 的测试替身。使用内存列表管理实体，包含内置的 `DummySndEntity`（最小化 `ISndEntity` 实现）。

## 包含文件

| 文件 | 职责 |
|------|------|
| `TestSndSceneHost.cs` | 实现 `ISndSceneHost`，用 `List<ISndEntity>` 管理实体。记录所有 `CreateEntity` 调用历史（`MetaList` 属性）和 `ClearAllCount` 计数。内置 `DummySndEntity` 提供最小化的 Name 和元数据支持。 |

## 使用模式

```csharp
var host = new TestSndSceneHost();
var entity = host.CreateEntity(new SndMetaData { Name = "test" });
Assert.Single(host.GetEntities());
```

---

[↑ 回到 TestSupport](../README.zh.md)
