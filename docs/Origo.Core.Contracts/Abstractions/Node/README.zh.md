<!-- docsync-pair: Origo.Core.Contracts/Abstractions/Node/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Node (Abstractions)

> [↑ 回到 Abstractions](../README.zh.md) · [↔ 实现: GodotAdapter/Snd](../../../Origo.GodotAdapter/Snd/README.zh.md)

## 概述

定义 Core 与适配层之间的抽象引擎节点契约。Core 通过 `INodeHandle` 触发基础节点行为
（可见性、释放），通过 `INodeFactory` 创建节点实例；两者都不暴露具体引擎节点类型。

节点容器接口 `INodeHost` 是 Core 内部编排契约，保留在
[Origo.Core/Abstractions/Node](../../../Origo.Core/Abstractions/Node/README.zh.md)。

## 包含文件

| 文件 | 职责 |
|------|------|
| `INodeFactory.cs` | 按资源标识创建节点实例 |
| `INodeHandle.cs` | 抽象节点句柄：Name / Free / SetVisible |

## 接口详细

### INodeFactory

| 成员 | 说明 |
|------|------|
| `Create(logicalName, resourceId)` | 创建节点并挂载到宿主，返回句柄 |

### INodeHandle

| 成员 | 说明 |
|------|------|
| `Name` | 节点逻辑名 |
| `Free()` | 释放节点资源 |
| `SetVisible(bool)` | 控制节点可见性 |

## 设计决策

### 为什么 INodeHandle 不暴露原生节点对象

Core 通过 `INodeHandle` 的方法操作节点，不持有、不暴露任何引擎特定类型。需要原生节点时，
适配层 `SndEntityNodeExtensions` 提供扩展方法：`GetNativeNode()` 将 `INodeHandle` 提取为
`Godot.Node?`，`GetNodeFromSnd<T>()` 经实体的 SND 节点注册表按逻辑名解析并强转。引擎节点
访问统一经适配层扩展显式声明依赖，`INodeHandle` 本身不通过 `object` 暴露引擎类型。

---
[↑ 回到 Abstractions](../README.zh.md)
