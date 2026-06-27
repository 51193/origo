# Origo.GodotAdapter.Tests

> [↑ 回到 Origo.manual](../README.md)

## 测试策略概述

Origo.GodotAdapter 的测试验证 Godot 4 适配层的正确性。
适配层将 Core 的抽象接口与 Godot 引擎 API 桥接，测试重点包括：
文件系统路径处理（`res://`/`user://` 虚拟路径）、Godot 类型序列化往返、
控制台命令的适配层扩展、启动编排、日志代理。

由于 GodotAdapter 依赖 Godot 引擎运行时，部分涉及 `Godot.Node`/`PackedScene` 的测试
被 coverlet 排除在外（在 `.csproj` 的 `ExcludeByFile` 中配置）。

## 能力文档索引

| 能力 | 文档 | 验证重点 |
|------|------|---------|
| 架构守卫 | [Architecture.md](Architecture.md) | SndContext 通过公共接口提供完整工作流和会话管理 |
| 启动编排 | [Bootstrap.md](Bootstrap.md) | OrigoAutoHost + OrigoDefaultEntry 启动流程 |
| 控制台 | [Console.md](Console.md) | press_button 命令、CommandHandlerBase |
| 文件系统 | [FileSystem.md](FileSystem.md) | GodotFileSystem 的 res:// / user:// 路径处理 |
| 日志 | [Logging.md](Logging.md) | GodotLogger 委托注入和 null handler |
| 序列化 | [Serialization.md](Serialization.md) | 14 种 Godot 引擎类型的序列化往返 + TypedData 多层内联 |
| 测试辅助 | [TestSupport.md](TestSupport.md) | NullFileSystem、TestSndSceneHost、TestLogger、TestRuntimeHelper |

---

[↑ 回到 Origo.manual](../README.md)
