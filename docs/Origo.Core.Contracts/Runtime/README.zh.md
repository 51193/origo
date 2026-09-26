<!-- docsync-pair: Origo.Core.Contracts/Runtime/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Runtime

> [↑ 回到 Origo.Core.Contracts](../README.zh.md)

## 模块能力

运行时扩展契约层。当前包含控制台命令处理工具扩展：命令处理器接口、调用模型与参数校验基类。
这些契约由 Core 实现层、GodotAdapter 与游戏代码实现或派生，不依赖运行时实现。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Console](Console/README.zh.md) | 控制台工具扩展契约 | `IConsoleCommandHandler` + `CommandInvocation` + `ConsoleCommandHandlerBase` |

## 本层文件

本目录仅包含子目录，无直接 `.cs` 文件。

---
[↑ 回到 Origo.Core.Contracts](../README.zh.md)
