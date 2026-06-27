# Origo.GodotAdapter

> [↑ 回到 Origo.manual](../README.md)

## 模块概述

**Origo.GodotAdapter** 是 Origo 框架的 Godot 4 适配层。负责将 Core 层的平台无关抽象与 Godot 引擎的具体 API 对接，包括文件系统（通过 `FileAccess`/`DirAccess`）、日志输出（通过 `GD.Print`）、节点生命周期（通过 `Node`/`PackedScene`）以及引擎类型序列化（`Vector2`、`Transform3D` 等 14 种类型）。

## 子系统一览

| 子系统 | 能力 | 详情 |
|--------|------|------|
| [Bootstrap](Bootstrap/README.md) | 启动编排 | OrigoAutoHost → OrigoDefaultEntry → Runtime 创建 + 策略发现 + Context 绑定 |
| [Console](Console/README.md) | Godot 控制台命令 | press_button / tree_debug 命令 + 适配层 CommandHandlerBase |
| [FileSystem](FileSystem/README.md) | Godot 文件系统 | IFileSystem 实现：FileAccess/DirAccess + res:// 和 user:// 支持 |
| [Logging](Logging/README.md) | Godot 日志 | ILogger 实现：委托注入 GD.Print/PushWarning/PushError |
| [Serialization](Serialization/README.md) | Godot 类型序列化 | 14 种上帝类型 → DataSourceNode 转换器 |
| [Snd](Snd/README.md) | Godot SND 实体 | ISndSceneHost 实现：GodotSndManager + GodotSndEntity + PackedSceneNodeFactory + TypedDataInitializer |
| — | TypedData 内联 | Source Generator 为 14 种 Godot 类型生成扩展方法与 Kind 注册 |

## 启动流程

```
OrigoDefaultEntry._Ready()
  ├── base._Ready()                          // OrigoAutoHost
  │   └── CreateRuntime()
  │       ├── GodotFileSystem
  │       ├── GodotSndManager
  │       ├── GodotJsonConverterRegistry 注册
  │       └── OrigoRuntime
  ├── 自动发现策略 (reflection scan, skip Godot assemblies)
  ├── SndContext 创建 (注入 Runtime + FileSystem + saveRoot + config)
  ├── SndManager.BindContext(sndContext)
  ├── LoadSceneAliases / LoadTemplates
  └── RequestLoadMainMenuEntrySave
```

## 架构约束

- **不承载核心业务规则**：所有业务逻辑在 Core 层，适配层仅做"翻译"
- **不反向依赖**：Core 绝不引用 GodotAdapter 的任何类型
- **Godot 类型仅在适配层出现**：`Godot.Vector*`、`Godot.Node` 等不会出现在 Core 层

### 策略生命周期隔离

适配层不参与策略生命周期管理的任何环节：

- **不触发策略钩子**：`GodotSndManager.CreateEntity` 仅创建实体和 Godot 节点，不调用 `AfterSpawn` / `AfterLoad` / `BeforeDead` 等钩子
- **不管理策略释放**：`RemoveEntity` 仅移除 Godot 节点和集合引用，不调用 `ReleaseStrategiesOnly`
- **不冲刷延迟管线**：帧循环中不绕过 Core 直接调用 `FlushDeferredActionsForCurrentFrame`
- **`OrigoAutoHost._Process` 为唯一帧入口**：在其中依次委托 Core 的 `ProcessAll` → `FlushEndOfFrameDeferred` → `Console.ProcessPending`，适配层仅做调度，不做决策

所有这些编排由 `SndRuntime`（Core 层）统一负责。详细分离原则见 [架构总览](../usage/architecture-overview.md#适配层与-core-层分离原则)。

### 桥接模式

`GodotSndEntity` 是桥接模式的体现：它同时实现 `ISndEntity`（Core 公开接口）和 `IEntityLifecycle`（Core internal 接口），内部持有 `SndEntity` 实例并全部透明委托。它本身不包含任何业务逻辑——仅作为 Godot Node 与 Core SndEntity 之间的适配器。

## 与 Core 的桥接

| Core 接口 | Adapter 实现 | 文件 |
|-----------|------------|------|
| `IFileSystem` | `GodotFileSystem` | [FileSystem/](FileSystem/README.md) |
| `ILogger` | `GodotLogger` | [Logging/](Logging/README.md) |
| `ISndSceneHost` | `GodotSndManager` | [Snd/](Snd/README.md) |
| `INodeFactory` | `GodotPackedSceneNodeFactory` | [Snd/](Snd/README.md) |
| `INodeHandle` | `GodotNodeHandle` | [Snd/](Snd/README.md) |
| `IConsoleCommandHandler` | `CommandHandlerBase` + 子类 | [Console/](Console/README.md) |

## TypedData 多层内联

Origo.GodotAdapter 引用 `Origo.SourceGeneration` 源码生成器，通过 `[assembly: SndInlineTypes(startKind: 128, ...)]` 在程序集中注册 14 种 Godot 引擎类型。编译时 SG 自动生成扩展方法（`TryGetVector2` / `AsVector3` 等）、`[ModuleInitializer]` 注册逻辑和 KindResolver/Converter 桥接。

- **TypedDataInitializer**（`Origo.GodotAdapter.Snd`）：公开的 `IsLoaded` 入口，访问此类型触发 GodotAdapter 程序集加载，确保所有 `[ModuleInitializer]` 执行完毕。测试项目通过此类强制加载适配层。
- **Kind 范围 128–141**：不与 Core 层 1–13 冲突，确保 Core 创建的 `(TypedData)42` 不会在 GodotAdapter 中被误解析为 `Vector2`。

详见 [Origo.SourceGeneration 文档](../Origo.SourceGeneration/README.md)。

---
[↑ 回到 Origo.manual](../README.md)
