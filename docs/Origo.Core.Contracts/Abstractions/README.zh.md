<!-- docsync-pair: Origo.Core.Contracts/Abstractions/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Abstractions

> [↑ 回到 Origo.Core.Contracts](../README.zh.md)

## 模块能力

Origo.Core.Contracts 的基础抽象层：定义平台实现方需要实现的叶级接口。所有接口
只使用 `System.*` 类型，不引用 Core 运行时实现或具体适配器。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Console](Console/README.zh.md) | 控制台输入输出抽象 | `IConsoleInputSource` + `IConsoleOutputChannel` |
| [FileSystem](FileSystem/README.zh.md) | 平台文件系统与路径抽象 | `IFileSystem` + `IPathResolver` |
| [Logging](Logging/README.zh.md) | 引擎无关日志接口 | `ILogger` + `ILogger<TCategory>` + `LogLevel` |

## 本层文件

本目录仅包含子目录，无直接 `.cs` 文件。

---
[↑ 回到 Origo.Core.Contracts](../README.zh.md)
