<!-- docsync-pair: docs/Origo.TestSupport/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Origo.TestSupport

供所有测试项目共享的测试基础设施。提供统一的测试替身（Test Double）、
内存文件系统、性能报告工具和共享测试策略基类。

## 模块

- **`Logging/TestLogger`** — 内存日志采集器，支持按级别过滤和消息记录
- **`Reporting/PerfReporter`** — 性能基准报告工具，支持单方法报告和多方法对比
- **`FileSystem/TestMemoryFileSystem`** — `IFileSystem` 的内存实现，支持完整文件系统操作
- **`Node/TestNodeHandle`** — `INodeHandle` 的测试替身
- **`Node/TestNodeFactory`** — `INodeFactory` 的测试替身，可模拟创建失败
- **`Scene/TestSndSceneHost`** — `ISndSceneHost` 的测试替身（含 `DummySndEntity`）
- **`Strategies/SharedTestStrategies`** — 共享测试策略抽象基类
- **`Observer/TestObserverEvents`** — 观察者事件采集工具

## 使用

所有测试项目通过 `InternalsVisibleTo` 获得访问权限。
在 Core.Tests 中通过 `global using Origo.TestSupport;` 全局导入。
