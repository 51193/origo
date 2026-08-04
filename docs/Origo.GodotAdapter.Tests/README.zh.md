<!-- docsync-pair: Origo.GodotAdapter.Tests/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Origo.GodotAdapter.Tests

> [↑ 回到 Origo.manual](../README.zh.md)

## 测试策略概述

Origo.GodotAdapter 的测试验证 Godot 4 适配层的正确性。
适配层将 Core 的抽象接口与 Godot 引擎 API 桥接，测试重点包括：
文件系统路径处理（`res://`/`user://` 虚拟路径）、Godot 类型序列化往返、
控制台命令的适配层扩展、启动编排、日志代理。

由于 GodotAdapter 依赖 Godot 引擎运行时，**任何 Godot API 调用（含 `new Node()`、`GD.Print`）在无引擎的单元测试宿主中都会导致测试进程 SIGSEGV 崩溃**，因此直接调用引擎 API 的源文件被 coverlet 排除（在 `.csproj` 的 `ExcludeByFile` 中配置）。每个排除文件都有文档化的技术理由（见 csproj 注释）：`GodotSndEntity.cs`/`GodotSndManager.cs`/`OrigoAutoHost.cs`/`OrigoDefaultEntry*.cs`（Godot Node 子类，节点创建与场景树操作不可达）、`FileSystem/` 三文件（方法体全部是 `FileAccess`/`DirAccess` 静态调用）、`GodotNodeHandle.cs`/`GodotPackedSceneNodeFactory.cs`（需真实节点/资源）、`CameraViewCommandHandler.cs`（`ExecuteCore` 首行即 `Engine.GetMainLoop()`）。

为缩小排除面，`GodotSndManager` 的实体集合编排逻辑（增删、查找、批量恢复回滚、帧处理、击杀标记）已提取到纯 C# 的 `SndEntityCollection<T>`（`Origo.GodotAdapter/Snd/SndEntityCollection.cs`），由 `SndEntityCollectionTests` 全覆盖（98.8%）；生成代码 `TypedData.g.cs` 的 14 个 Godot 类型访问器由 `GodotTypedDataGeneratedCoverageTests` 逐类型覆盖。门禁统计范围内行覆盖率 ≥ 90%（`ThresholdStat=total`，实测约 94%）。

引擎依赖文件的行为验证由 [Origo.GodotAdapter.Integration.Tests](../Origo.GodotAdapter.Integration.Tests/README.zh.md)
在真实的 Godot `--headless` 运行时中执行。集成测试与单元测试互补：单元测试验证 Core 层逻辑与类型序列化，
集成测试验证 Godot 特定运行时行为（真实文件系统、Node 生命周期、启动编排）。

> **覆盖率门禁口径**：`Origo.GodotAdapter.Tests` 的 90% 行覆盖率门禁
> （`ThresholdStat=total`）统计**未排除**的源文件——即不依赖 Godot 运行时、
> 可被纯 .NET 单元测试覆盖的代码。排除面（引擎 API 绑定文件，见 csproj 注释
> 逐条理由）的行为由集成测试兜底。排除是技术必然（引擎调用导致 SIGSEGV），
> 而非避免写测试的捷径。

## 测试层次

| 层次 | 项目 | 运行时 | 覆盖范围 |
|------|------|--------|---------|
| 单元测试 | `Origo.GodotAdapter.Tests` | 纯 .NET（`Microsoft.NET.Sdk`） | Core 抽象逻辑、类型序列化、路径处理、控制台命令 |
| 集成测试 | `Origo.GodotAdapter.Integration.Tests` | Godot `--headless`（`Godot.NET.Sdk`） | 真实文件 I/O、Node 实例化、启动属性、引擎 API 可用性 |

## 能力文档索引

| 能力 | 文档 | 文件数 | 测试数 | 验证重点 |
|------|------|-------|-------|---------|
| 架构守卫 | [Architecture.md](Architecture.zh.md) | 1 | 5 | SndContext 公共角色接口完整性、会话创建/销毁、CommandHandlerBase 公共可见性、GodotSndEntity 生命周期 internal 守卫 |
| SND 实体 | [Snd.md](Snd.zh.md) | 3 | 40 | SndEntityCollection 全能力与批量恢复回滚、TypedDataInitializer 强制加载、节点扩展契约 |
| 控制台 | [Console.md](Console.zh.md) | 5 | 28 | press_button/camera_view/tree_debug 命令、CommandHandlerBase 参数校验与守卫、ProjectionHelper 世界→屏幕投影 |
| 文件系统 | [FileSystem.md](FileSystem.zh.md) | 1 | 3 | GodotFileSystem 的 res:// / user:// 路径处理（委托给 PathUtility） |
| 日志 | [Logging.md](Logging.zh.md) | 1 | 9 | GodotLogger 委托注入、null handler 安全与级别过滤 |
| 序列化 | [Serialization.md](Serialization.zh.md) | 5 | 57（含 6 Benchmark） | 14 种 Godot 类型序列化往返 + 生成访问器逐类型覆盖 + TypedData 多层内联 + 性能基准 |
| 测试辅助 | [TestSupport.md](TestSupport.zh.md) | 2 | —（基础设施，无 [Fact]） | NullFileSystem、TestSndSceneHost、InMemorySndEntity、TestLogger、TestRuntimeHelper |

---

[↑ 回到 Origo.manual](../README.zh.md)
