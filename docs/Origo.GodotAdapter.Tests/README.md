# Origo.GodotAdapter.Tests

> [↑ 回到 Origo.manual](../README.md)

## 测试策略概述

Origo.GodotAdapter 的测试验证 Godot 4 适配层的正确性。
适配层将 Core 的抽象接口与 Godot 引擎 API 桥接，测试重点包括：
文件系统路径处理（`res://`/`user://` 虚拟路径）、Godot 类型序列化往返、
控制台命令的适配层扩展、启动编排、日志代理。

由于 GodotAdapter 依赖 Godot 引擎运行时，部分涉及 `Godot.Node`/`PackedScene` 等 Godot API 的生产源文件
（如 `GodotSndEntity.cs`、`GodotNodeHandle.cs`、`GodotPackedSceneNodeFactory.cs`）被 coverlet 排除在外
（在 `.csproj` 的 `ExcludeByFile` 中配置），其对应逻辑无法在测试中直接覆盖。

## 能力文档索引

| 能力 | 文档 | 文件数 | 测试数 | 验证重点 |
|------|------|-------|-------|---------|
| 架构守卫 | [Architecture.md](Architecture.md) | 1 | 3 | SndContext 公共角色接口完整性、会话创建/销毁、CommandHandlerBase 公共可见性 |
| 启动编排 | [Bootstrap.md](Bootstrap.md) | 1 | 2 | GodotSndBootstrap.BindRuntimeAndContext 守卫与四参数契约 |
| 控制台 | [Console.md](Console.md) | 2 | 11 | press_button 命令、CommandHandlerBase 参数校验与守卫 |
| 文件系统 | [FileSystem.md](FileSystem.md) | 1 | 17 | GodotPathResolver/GodotFileSystem 的 res:// / user:// 路径处理与遍历保护 |
| 日志 | [Logging.md](Logging.md) | 1 | 9 | GodotLogger 委托注入、null handler 安全与级别过滤 |
| 序列化 | [Serialization.md](Serialization.md) | 4 | 50（含 6 Benchmark） | 14 种 Godot 类型序列化往返 + TypedData 多层内联 + 性能基准 |
| 测试辅助 | [TestSupport.md](TestSupport.md) | 2 | —（基础设施，无 [Fact]） | NullFileSystem、TestSndSceneHost、InMemorySndEntity、TestLogger、TestRuntimeHelper |

---

[↑ 回到 Origo.manual](../README.md)
