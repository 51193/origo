<!-- docsync-pair: Origo.Core.Kernel/Abstractions/Node/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Node (Core Internal Host)

> [↑ 回到 Abstractions](../README.zh.md) · [↔ 消费者契约: Origo.Core.Contracts/Node](../../../Origo.Core.Contracts/Abstractions/Node/README.zh.md)

## 概述

Core 内部的节点容器契约 `INodeHost`：管理节点恢复、回收与元数据导出。面向消费者和适配层的
`INodeFactory` / `INodeHandle` 位于 [Origo.Core.Contracts/Abstractions/Node](../../../Origo.Core.Contracts/Abstractions/Node/README.zh.md)。

## 包含文件

| 文件 | 职责 |
|------|------|
| `INodeHost.cs` | internal：节点容器行为——恢复、回收、导出元数据 |

## 接口详细

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

`INodeHost` 是 SND 实体内部管理节点的契约，不是对外公开的能力。外部策略代码通过
`ISndEntity`（组合 `ISndNodeAccess`）访问节点，不需要感知节点容器的恢复/回收生命周期。
internal 可见性防止策略代码绕过实体直接操作节点池。

---
[↑ 回到 Abstractions](../README.zh.md)
