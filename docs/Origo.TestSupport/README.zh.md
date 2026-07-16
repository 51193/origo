<!-- docsync-pair: docs/Origo.TestSupport/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Origo.TestSupport

供所有测试项目共享的测试基础设施。提供统一的测试替身（Test Double）、
内存文件系统、性能报告工具和共享测试策略基类。

## 模块

| 子模块 | 说明 |
|--------|------|
| [FileSystem](FileSystem/README.zh.md) | `IFileSystem` 纯内存测试替身 |
| [Logging](Logging/README.zh.md) | `ILogger` 内存日志采集器 |
| [Node](Node/README.zh.md) | `INodeHandle` / `INodeFactory` 测试替身 |
| [Observer](Observer/README.zh.md) | 观察者事件采集基础设施 |
| [Reporting](Reporting/README.zh.md) | 性能基准报告工具 |
| [Scene](Scene/README.zh.md) | `ISndSceneHost` 测试替身 |
| [Strategies](Strategies/README.zh.md) | 共享测试策略基类和索引常量 |

## 使用

所有测试项目通过 `InternalsVisibleTo` 获得访问权限。
在 Core.Tests 中通过 `global using Origo.TestSupport;` 全局导入。
