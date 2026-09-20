<!-- docsync-pair: Origo.Core.Contracts/Abstractions/Runtime/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Runtime (Abstractions)

> [↑ 回到 Abstractions](../README.zh.md) · [↔ 实现: Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.zh.md)

## 概述

定义帧驱动抽象接口。`IOrigoFrameDriver` 是宿主环境与 Core 之间的帧边界——适配层通过 `DriveFrame(delta)` 移交帧控制权，Core 内部按固定顺序编排（实体处理→业务队列→杀实体→系统队列→控制台）。kernel 内部的调度契约与实现见 [Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.zh.md)。

## 包含文件

| 文件 | 职责 |
|------|------|
| `IOrigoFrameDriver.cs` | 对外暴露的帧边界接口：`DriveFrame(double delta)` |

## 接口成员

### IOrigoFrameDriver

| 成员 | 说明 |
|------|------|
| `DriveFrame(double delta)` | 宿主环境帧边界入口。Core 内部按固定顺序编排：实体帧处理 → 业务延迟队列 → 清理待杀实体 → 系统延迟队列 → 控制台 pump。适配层不应直接调用 `FlushEndOfFrameDeferred` 或 `ProcessPending`，只应调用此方法 |

## 设计决策

### 为什么帧驱动独立于调度实现

`IOrigoFrameDriver` 是对外暴露的帧边界抽象——适配层通过它移交帧控制权，不感知内部队列顺序、实体处理管线等编排细节。调度队列契约与实现属于 kernel 内部，位于 [Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.zh.md)，两者职责正交：一个定义帧边界，一个管理队列。

### 为什么不提供取消单个动作的能力

在单线程帧循环模型中，帧内排队的动作一般是一次性的轻量事务，不需要取消。如果需要条件执行，应由策略在 Enqueue 前自行判断，或在 Action 内部做早期退出。引入取消机制会显著增加队列实现复杂度，但不解决实际业务问题。

---
[↑ 回到 Abstractions](../README.zh.md)
