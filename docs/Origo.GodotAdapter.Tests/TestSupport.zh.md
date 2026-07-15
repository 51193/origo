<!-- docsync-pair: Origo.GodotAdapter.Tests/TestSupport -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# TestSupport

> [↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)

## 被测行为概览

`TestSupport/` 目录提供测试基础设施（测试替身与静态工厂），本身不含 `[Fact]`/`[Theory]` 测试方法，
供 Console / Architecture 等能力测试复用，用于在无 Godot 引擎运行时的前提下搭建 `OrigoRuntime`/会话环境。

## 测试辅助设施

| 设施 | 类型 | 用途 |
|------|------|------|
| `NullFileSystem` | `IFileSystem` 实现 | 最小文件系统替身：所有 I/O 操作抛出 `NotSupportedException`，路径操作返回基础拼接，枚举返回空 |
| `TestSndSceneHost` | `ISndSceneHost` 实现 | 内存场景宿主，维护实体字典，支持按名称查找、`AddEntity`、`RemoveEntity` 与 `RequestKillEntity`（重复 kill 抛异常） |
| `InMemorySndEntity` | `ISndEntity` 实现 | 内存实体替身，基于字典存取 `SetData`/`GetData`/`TryGetData`，策略/节点/观察者方法为空操作 |
| `TestLogger` | `ILogger` 实现 | 按级别收集日志到列表（Debugs/Infos/Warnings/Errors），条目格式 `[tag] message` |
| `PerfReporter` | `PerfReporter` | 性能比对表格输出器（`ReportTable`/`CompareTable`），同时写控制台与 xUnit 测试输出，供 `GodotTypedDataPerformanceTests` 使用 |
| `TestRuntimeHelper` | 静态工厂类 | `CreateRuntime()` 快速创建 `OrigoRuntime` + `TestSndSceneHost`（内置 `NullFileSystem`）；`BootstrapForegroundSession()` 经内存文件系统装载主菜单入口存档并 flush 延迟队列 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 无 | 这些为测试基础设施（无 `[Fact]`），其行为由复用它们的能力测试（Console / Architecture）间接验证，不单列覆盖目标 | — |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
