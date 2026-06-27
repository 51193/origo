# Node (Abstractions)

> [↑ 回到 Abstractions](../README.md) · [↔ 实现: GodotAdapter/Snd](../../../Origo.GodotAdapter/Snd/README.md)

## 概述

定义抽象引擎节点操作接口体系。Core 层通过 `INodeHandle` 触发基础节点行为（可见性、释放），通过 `INodeFactory` 创建节点实例，通过 `INodeHost`（internal）管理节点的恢复与导出。所有接口均不暴露具体引擎类型——`INodeHandle` 不包含任何引擎原生节点的引用。

## 包含文件

| 文件 | 职责 |
|------|------|
| `INodeFactory.cs` | 按资源标识创建节点实例 |
| `INodeHandle.cs` | 抽象节点句柄：Name / Free / SetVisible |
| `INodeHost.cs` | internal：节点容器行为——恢复、回收、导出元数据 |

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

### INodeHost (internal)

| 成员 | 说明 |
|------|------|
| `GetNode(name)` | 按名获取节点句柄 |
| `GetNodeNames()` | 枚举已挂载节点名称 |
| `Recover(NodeMetaData)` | 从元数据恢复节点 |
| `Release()` | 回收全部节点 |
| `SerializeMetaData()` | 导出当前节点元数据 |

## 设计决策

### 为什么 INodeHost 是 internal

`INodeHost` 是 SND 实体内部管理节点的契约，不是对外公开的能力。外部策略代码通过 `ISndEntity`（组合了 `ISndNodeAccess`）访问节点，不需要感知节点容器的恢复/回收生命周期。internal 可见性防止策略代码绕过实体直接操作节点池。

### 为什么 INodeHandle 不暴露原生节点对象

Core 层通过 `INodeHandle` 的方法（`Free` / `SetVisible`）操作节点，不持有、不暴露任何引擎特定类型。需要原生节点时，适配层 `SndEntityNodeExtensions`（命名空间 `Origo.GodotAdapter.Snd`，文件 `Origo.GodotAdapter/SndEntityNodeExtensions.cs`）提供扩展方法：`GetNativeNode()` 将 `INodeHandle` 提取为 `Godot.Node?`（句柄非 `GodotNodeHandle` 时返回 null），`GetNodeFromSnd<T>()` 遍历 Godot 场景树取强类型节点。引擎节点访问统一经此适配层扩展显式声明引擎依赖，`INodeHandle` 本身不通过 `object` 暴露引擎类型，使 Core 与引擎类型保持隔离。

---
[↑ 回到 Abstractions](../README.md)
