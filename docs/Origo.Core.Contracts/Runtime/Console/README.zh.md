<!-- docsync-pair: Origo.Core.Contracts/Runtime/Console/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Console (Contracts)

> [↑ 回到 Runtime](../README.zh.md) · [↔ 实现: Origo.Core/Runtime/Console](../../../Origo.Core/Runtime/Console/README.zh.md)

## 模块能力

控制台命令处理的工具扩展契约：命令处理器接口、命令调用模型与参数校验基类。Core 的路由
实现、Game 代码和 GodotAdapter 命令处理器都面向这些契约编译；实际命令泵仍由
`IOrigoFrameDriver.DriveFrame` 统一驱动。

## 包含文件

| 文件 | 职责 |
|------|------|
| `IConsoleCommandHandler.cs` | 命令处理器接口：Name / HelpText / 参数范围 / TryExecute |
| `CommandInvocation.cs` | 命令调用模型：Command + PositionalArgs + NamedArgs |
| `ConsoleCommandHandlerBase.cs` | 命令处理器基类：参数数量校验与 ExecuteCore 调度 |
| `ConsoleMessages.cs` | internal 常量：共享的用户可见错误消息 |

## 设计决策

### 为什么参数校验放在基类

`ConsoleCommandHandlerBase.TryExecute` 在执行前统一校验位置参数数量，失败时返回带
HelpText 的明确错误。把校验集中到基类避免每个命令处理器重复实现，并确保所有派生命令
在参数错误时有一致的 fail-fast 行为。

### 为什么命令调用模型是纯数据

`CommandInvocation` 只承载解析后的命令名、位置参数和命名参数，不执行解析、路由或命令
副作用。解析由 Core 的 parser 负责，路由由 Core 的 router 负责，保持契约层无行为依赖。

---
[↑ 回到 Runtime](../README.zh.md)
