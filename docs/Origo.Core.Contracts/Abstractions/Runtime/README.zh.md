<!-- docsync-pair: Origo.Core.Contracts/Abstractions/Runtime/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Runtime (Abstractions)

> [↑ 回到 Abstractions](../README.zh.md) · [↔ 实现: Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.zh.md)

## 概述

定义帧驱动与稳定 host/runtime 抽象。`IOrigoFrameDriver` 是宿主环境与 Core 之间的帧边界——适配层通过 `DriveFrame(delta)` 移交帧控制权，Core 内部按固定顺序编排（实体处理→业务队列→杀实体→系统队列→控制台）。`IOrigoRuntime` 是 host 构造后的稳定 runtime 契约，`ISndWorldAccess` 是其 SND world 配置与转换访问面。具体实现位于 Kernel 的 Runtime/Snd；kernel 内部的调度契约与实现见 [Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.zh.md)。

## 包含文件

| 文件 | 职责 |
|------|------|
| `IOrigoFrameDriver.cs` | 对外暴露的帧边界接口：`DriveFrame(double delta)` |
| `IOrigoRuntime.cs` | 稳定 host/runtime 契约：metadata、logger、world、blackboard、console channel 与 session |
| `ISndWorldAccess.cs` | 稳定 SND world 访问：策略注册、类型映射、metadata 转换与 data-source gateway |

## 接口成员

### IOrigoFrameDriver

| 成员 | 说明 |
|------|------|
| `DriveFrame(double delta)` | 宿主环境帧边界入口。Core 内部按固定顺序编排：实体帧处理 → 业务延迟队列 → 清理待杀实体 → 系统延迟队列 → 控制台 pump。适配层不应直接调用 `FlushEndOfFrameDeferred` 或 `ProcessPending`，只应调用此方法 |

### IOrigoRuntime

| 成员 | 说明 |
|------|------|
| `Meta` | 框架元数据 |
| `Logger` | 运行时日志服务 |
| `SndWorld` | 稳定 SND world 访问面 |
| `SystemBlackboard` | 系统级黑板，生命周期覆盖整个运行期 |
| `ConsoleInput` | 控制台输入队列；host 未注入时为 null |
| `ConsoleOutputChannel` | 控制台输出通道；host 未注入时为 null |
| `SessionManager` | 当前 session manager |
| `RegisterConsoleCommandHandler(handler)` | 通过运行时 console router 注册 `IConsoleCommandHandler`；缺任一 console channel 时显式失败，不静默丢弃 |

### ISndWorldAccess

| 成员 | 说明 |
|------|------|
| `ConverterRegistry` | typed data-source 序列化 converter registry |
| `DataSourceIo` | data-source I/O gateway |
| `GetRegisteredStrategyIndices()` | 返回全部已注册策略 index |
| `IsStrategyRegistered(index)` | 判断策略 index 是否已注册 |
| `RegisterStrategy<TStrategy>(factory)` | 注册策略工厂；Bootstrap 冻结后失败 |
| `RegisterTypeMappings(registerMappings)` | 追加稳定的类型名映射 |
| `CloneMetaData(meta)` | 深拷贝实体 metadata |
| `ResolveTemplate(templateAlias)` | 解析模板 alias |
| `ReadMetaNode(node)` / `ReadMetaListNode(node)` | 读取单实体或 metadata 列表 |
| `WriteMetaNode(meta)` / `WriteMetaListNode(metaDataList)` | 写入单实体或 metadata 列表 |
| `ReadTypedDataMap(node)` | 读取 typed-data map |
| `ResolveMetaListFromJsonArray(root)` | 将 JSON array 节点解析为 metadata 列表 |

## 设计决策

### 为什么 console handler 注册在 `IOrigoRuntime` 上

`IConsoleCommandHandler` 与 `ConsoleCommandHandlerBase` 是 shell tooling extension；注册必须经过 runtime 的 console router，不能由策略或适配层自行解析、执行命令而绕过控制台 pump 与参数校验。Core 与 Adapter host 共用 `IOrigoRuntime.RegisterConsoleCommandHandler` 这一稳定入口；若 host 未注入 console input/output，注册立即失败而不是静默丢弃 handler。

### 为什么帧驱动独立于调度实现

`IOrigoFrameDriver` 是对外暴露的帧边界抽象——适配层通过它移交帧控制权，不感知内部队列顺序、实体处理管线等编排细节。调度队列契约与实现属于 kernel 内部，位于 [Origo.Core.Kernel/Scheduling](../../../Origo.Core.Kernel/Scheduling/README.zh.md)，两者职责正交：一个定义帧边界，一个管理队列。

### 为什么不提供取消单个动作的能力

在单线程帧循环模型中，帧内排队的动作一般是一次性的轻量事务，不需要取消。如果需要条件执行，应由策略在 Enqueue 前自行判断，或在 Action 内部做早期退出。引入取消机制会显著增加队列实现复杂度，但不解决实际业务问题。

---
[↑ 回到 Abstractions](../README.zh.md)
