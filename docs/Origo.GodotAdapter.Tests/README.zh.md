<!-- docsync-pair: Origo.GodotAdapter.Tests/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Origo.GodotAdapter.Tests

> [↑ 回到 Origo.manual](../README.zh.md)

## 测试策略概述

Origo.GodotAdapter 的测试验证 Godot 4 适配层的正确性。
适配层将 Core 的抽象接口与 Godot 引擎 API 桥接，测试重点包括：
文件系统路径处理（`res://`/`user://` 虚拟路径）、Godot 类型序列化往返、
控制台命令的适配层扩展、启动编排、日志代理。

由于 GodotAdapter 依赖 Godot 引擎运行时，部分涉及 `Godot.Node`/`PackedScene` 等 Godot API 的生产源文件
（如 `GodotSndEntity.cs`、`GodotNodeHandle.cs`、`GodotPackedSceneNodeFactory.cs`）被 coverlet 排除在外
（在 `.csproj` 的 `ExcludeByFile` 中配置），其对应逻辑无法在单元测试中直接覆盖。

引擎依赖文件的行为验证由 [Origo.GodotAdapter.Integration.Tests](../Origo.GodotAdapter.Integration.Tests/README.zh.md)
在真实的 Godot `--headless` 运行时中执行。集成测试与单元测试互补：单元测试验证 Core 层逻辑与类型序列化，
集成测试验证 Godot 特定运行时行为（真实文件系统、Node 生命周期、启动编排）。

> **覆盖率门禁口径**：`Origo.GodotAdapter.Tests` 的 90% 行覆盖率门禁
> （`ThresholdStat=total`）只统计**未排除**的源文件——即不依赖 Godot 运行时、
> 可被纯 .NET 单元测试覆盖的代码。排除面（上述桥接/启动/命令处理文件）
> 约占全程序集行数的一半，其行为由集成测试兜底；因此"93%+ 行覆盖"的表述
> 指门禁统计范围内的行覆盖，而非全程序集覆盖。移除排除面会使门禁统计到
> 无法在无引擎环境下测试的代码行，覆盖率骤降至约 47%，故排除是刻意设计。

## 测试层次

| 层次 | 项目 | 运行时 | 覆盖范围 |
|------|------|--------|---------|
| 单元测试 | `Origo.GodotAdapter.Tests` | 纯 .NET（`Microsoft.NET.Sdk`） | Core 抽象逻辑、类型序列化、路径处理、控制台命令 |
| 集成测试 | `Origo.GodotAdapter.Integration.Tests` | Godot `--headless`（`Godot.NET.Sdk`） | 真实文件 I/O、Node 实例化、启动属性、引擎 API 可用性 |

## 能力文档索引

| 能力 | 文档 | 文件数 | 测试数 | 验证重点 |
|------|------|-------|-------|---------|
| 架构守卫 | [Architecture.md](Architecture.zh.md) | 1 | 5 | SndContext 公共角色接口完整性、会话创建/销毁、CommandHandlerBase 公共可见性 |
| 控制台 | [Console.md](Console.zh.md) | 4 | 22 | press_button/camera_view 命令、CommandHandlerBase 参数校验与守卫、ProjectionHelper 世界→屏幕投影 |
| 文件系统 | [FileSystem.md](FileSystem.zh.md) | 1 | 3 | GodotFileSystem 的 res:// / user:// 路径处理（委托给 PathUtility） |
| 日志 | [Logging.md](Logging.zh.md) | 1 | 9 | GodotLogger 委托注入、null handler 安全与级别过滤 |
| 序列化 | [Serialization.md](Serialization.zh.md) | 4 | 50（含 6 Benchmark） | 14 种 Godot 类型序列化往返 + TypedData 多层内联 + 性能基准 |
| 测试辅助 | [TestSupport.md](TestSupport.zh.md) | 2 | —（基础设施，无 [Fact]） | NullFileSystem、TestSndSceneHost、InMemorySndEntity、TestLogger、TestRuntimeHelper |

---

[↑ 回到 Origo.manual](../README.zh.md)
