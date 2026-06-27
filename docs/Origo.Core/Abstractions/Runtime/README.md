# Runtime (Abstractions)

> [↑ 回到 Abstractions](../README.md) · [↔ 实现: Scheduling](../../Scheduling/README.md)

## 概述

定义帧驱动抽象接口。`IOrigoFrameDriver` 是宿主环境与 Core 之间的帧边界——适配层通过 `DriveFrame(delta)` 移交帧控制权，Core 内部按固定顺序编排（实体处理→业务队列→杀实体→系统队列→控制台）。`IScheduler`（已改为 `internal`）是 Core 内部的调度队列接口。

## 包含文件

| 文件 | 职责 |
|------|------|
| `IOrigoFrameDriver.cs` | 对外暴露的帧边界接口：`DriveFrame(double delta)` |
| `IScheduler.cs` | **internal** — Core 内部调度队列接口：Enqueue / Tick / Clear |

## 接口成员

### IOrigoFrameDriver

| 成员 | 说明 |
|------|------|
| `DriveFrame(double delta)` | 宿主环境帧边界入口。Core 内部按固定顺序编排：实体帧处理 → 业务延迟队列 → 清理待杀实体 → 系统延迟队列 → 控制台 pump。适配层不应直接调用 `FlushEndOfFrameDeferred` 或 `ProcessPending`，只应调用此方法 |

### IScheduler (internal)

| 方法 | 说明 |
|------|------|
| `Enqueue(Action)` | 将动作安排在本帧或之后的某个阶段执行 |
| `Tick()` | 执行已排队动作，返回本周期执行的动作数量 |
| `Clear()` | 清空尚未执行的动作队列 |

## 设计决策

### 为什么 IOrigoFrameDriver 独立于 IScheduler

`IScheduler` 是 Core 内部的调度队列接口（已改为 `internal`），供 `OrigoRuntime` 内部子系统使用。`IOrigoFrameDriver` 是对外暴露的帧边界抽象——适配层通过它移交帧控制权，不感知 Core 内部的队列顺序、实体处理管线等编排细节。两接口职责正交：一个管理队列，一个定义帧边界。

### 为什么 IScheduler 是 internal

`IScheduler` 在 Core 层之外没有跨程序集的消费者（唯一实现者是 `internal sealed ActionScheduler`）。将其标记为 `internal` 避免对外暴露出不必要的 API 表面积，符合 public 白名单原则。

### 为什么不提供取消单个动作的能力

在单线程帧循环模型中，帧内排队的动作一般是一次性的轻量事务，不需要取消。如果需要条件执行，应由策略在 Enqueue 前自行判断，或在 Action 内部做早期退出。引入取消机制会显著增加队列实现复杂度，但不解决实际业务问题。

---
[↑ 回到 Abstractions](../README.md)
