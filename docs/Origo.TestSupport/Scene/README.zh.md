<!-- docsync-pair: Origo.TestSupport/Scene/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->

# Scene

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

`ISndSceneHost` 的测试替身。提供两种轻量实现：`TestSndSceneHost`（内置最小 `DummySndEntity`，与生产宿主契约对齐）与 `StubSndSceneHost`（零依赖、无策略/节点，供 `LevelBuilder` 与数据流转测试使用）。

## 包含文件

| 文件 | 职责 |
|------|------|
| `TestSndSceneHost.cs` | 实现 `ISndSceneHost`，用 `List<ISndEntity>` 管理实体。通过 `BuildMetaList()` 导出实体元数据列表，并提供 `ClearAllCount` 计数。内置 `DummySndEntity` 提供最小化的 Name 和元数据支持。**与生产宿主契约对齐**：`GetEntities()` 返回快照（不可下转为可变后备列表），`RemoveEntity` 对不存在的实体抛 `InvalidOperationException`（与 `SndEntityCollection` 一致），避免测试盲区。 |
| `StubSndSceneHost.cs` | 轻量存根场景宿主，使用内嵌 `StubSndEntity`（无策略/节点）：节点访问抛异常，策略/观察者操作静默 no-op，仅支持基础键值数据。供 `LevelBuilder` 离线构建与无需完整 SndWorld/Context 的数据流转测试使用。 |

## 使用模式

```csharp
var host = new TestSndSceneHost();
var entity = host.CreateEntity(new SndMetaData { Name = "test" });
Assert.Single(host.GetEntities());
```

---

[↑ 回到 TestSupport](../README.zh.md)
