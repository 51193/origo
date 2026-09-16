<!-- docsync-pair: Origo.TestSupport/Node/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->

# Node

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

SND 节点抽象的测试替身：`INodeHandle` 和 `INodeFactory`，支持调用计数验证和模拟失败。

## 包含文件

| 文件 | 职责 |
|------|------|
| `TestNodeHandle.cs` | 实现 `INodeHandle`。提供 `FreeCount` 计数器、`IsVisible` 状态追踪和 `Name` 属性。 |
| `TestNodeFactory.cs` | 实现 `INodeFactory`。接受可选的 `IEnumerable<string>` 资源 ID 列表用于模拟创建失败。记录所有创建的 `TestNodeHandle` 实例（`CreatedHandles`）与每次创建的请求记录列表（`Requests`）；创建失败通过构造函数注入的资源 ID 列表模拟。 |

## 设计决策

### 为什么 Node 替身与 SndSceneHost 替身分离

节点生命周期（创建 → 查询 → 释放）和场景宿主生命周期（实体容器管理）是两个正交关注点。分离替身允许测试在仅需要节点 mock 时无需引入场景宿主依赖。

---

[↑ 回到 TestSupport](../README.zh.md)
