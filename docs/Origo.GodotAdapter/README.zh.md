<!-- docsync-pair: Origo.GodotAdapter/README -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Origo.GodotAdapter

> [↑ 回到 Origo.manual](../README.zh.md)

## 模块概述

**Origo.GodotAdapter** 是 Origo 框架的 Godot 4 适配层。负责将 Core 层的平台无关抽象与 Godot 引擎的具体 API 对接，包括文件系统（通过 `FileAccess`/`DirAccess`）、日志输出（通过 `GD.Print`）、节点生命周期（通过 `Node`/`PackedScene`）以及引擎类型序列化（`Vector2`、`Transform3D` 等 14 种类型）。

## 子系统一览

| 子系统 | 能力 | 详情 |
|--------|------|------|
| [Bootstrap](Bootstrap/README.zh.md) | 启动编排 | OrigoAutoHost → OrigoDefaultEntry → Runtime 创建 + 策略发现 + Context 绑定 |
| [Console](Console/README.zh.md) | Godot 控制台命令 | press_button / tree_debug / camera_view 命令 + 适配层 CommandHandlerBase |
| [FileSystem](FileSystem/README.zh.md) | Godot 文件系统 | IFileSystem 实现：FileAccess/DirAccess + res:// 和 user:// 支持 |
| [Logging](Logging/README.zh.md) | Godot 日志 | ILogger 实现：委托注入 GD.Print/PushWarning/PushError |
| [Serialization](Serialization/README.zh.md) | Godot 类型序列化 | 14 种 Godot 类型 → DataSourceNode 转换器 |
| [Snd](Snd/README.zh.md) | Godot SND 实体 | ISndSceneHost 实现：GodotSndManager + GodotSndEntity + PackedSceneNodeFactory |
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
  ├── RegisterConsoleCommandHandlers()       // 适配层命令
  ├── new SndContext(...)                    // 传入启动配置
  ├── SndManager.BindContext(sndContext)
  └── sndContext.Bootstrap()                 // Core 内部按序执行：
        ├── 策略发现 (reflection scan, skip Godot assemblies)
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
- **不冲刷延迟管线**：帧循环中不绕过 Core 直接调用 internal 的 `FlushEndOfFrameDeferred`
- **`OrigoAutoHost._Process` 为唯一帧入口**：在其中依次委托 Core 的 `ProcessAll` → `FlushEndOfFrameDeferred` → `Console.ProcessPending`，适配层仅做调度，不做决策

所有这些编排由 Core 层的会话生命周期（`SessionManager` / `SessionRun`）统一负责。详细分离原则见 [架构总览](../usage/architecture-overview.zh.md#适配层与-core-层分离原则)。

### 桥接模式

`GodotSndEntity` 是桥接模式的体现：它同时实现 `ISndEntity`（Core 公开接口）和 `IEntityLifecycle`（Core internal 接口），内部持有 `SndEntity` 实例并全部透明委托。它本身不包含任何业务逻辑——仅作为 Godot Node 与 Core SndEntity 之间的适配器。


### 使用注意事项（常见坑）

以下为集成 Origo 到 Godot 项目时的常见问题与约定，均已在示例项目验证：

- **Export 属性覆写时机**：`OrigoDefaultEntry` 子类在构造函数中设置 `ConfigPath` 等
  Export 属性不会生效（Godot 场景实例化时会重新赋默认值）。必须在 `_Ready` 中、
  `base._Ready()` 调用**之前**赋值。
- **命令行运行不重新编译 C#**：`godot --path .` 直接运行加载的是上次构建的 DLL。
  编辑器模式会自动构建；命令行方式需先 `dotnet build`（或使用
  `dotnet build && godot --path .`）。
- **UI 根节点吞掉 3D 点击**：覆盖全屏的 Control 默认 `mouse_filter = Stop`，会拦截所有
  鼠标事件导致 3D 交互（如棋盘点击）失效。UI 根节点应设 `MouseFilter = Ignore`，
  子面板保持默认 Stop 以正常响应按钮。
- **输入事件阶段选择**：右键按下等事件可能在被 `_UnhandledInput` 接收前已被 GUI 系统
  消费。需要全局兜底的输入（如摄像机拖动/缩放）应使用 `_Input`；场景对象交互用
  `_UnhandledInput`。
- **`LookAt` 垂直视角共线**：相机位于目标正上方时 `LookAt(target, Vector3.Up)` 会每帧
  报 "Target and up vectors are colinear" 警告。俯仰接近垂直时应改用水平 up 向量
  （如 `Vector3.Back`）。
- **锚点布局**：`SetAnchorsPreset(RightWide)` 只设置锚点不设置偏移，面板宽度为 0。
  应使用 `SetAnchorsAndOffsetsPreset` 并显式设置 `OffsetLeft` 等。
- **headless 视口**：headless 模式下视口尺寸为 0，`Camera3D.UnprojectPosition` 等
  屏幕↔世界换算返回无效值。依赖真实视口的测试（如鼠标点击链路）需在 GUI 模式运行。

## 与 Core 的桥接

| Core 接口 | Adapter 实现 | 文件 |
|-----------|------------|------|
| `IFileSystem` | `GodotFileSystem` | [FileSystem/](FileSystem/README.zh.md) |
| `ILogger` | `GodotLogger` | [Logging/](Logging/README.zh.md) |
| `ISndSceneHost`（internal） / `ISndSceneReadAccess`（public） | `GodotSndManager` | [Snd/](Snd/README.zh.md) |
| `INodeFactory` | `GodotPackedSceneNodeFactory` | [Snd/](Snd/README.zh.md) |
| `INodeHandle` | `GodotNodeHandle` | [Snd/](Snd/README.zh.md) |
| `IConsoleCommandHandler` | `CommandHandlerBase` + 子类 | [Console/](Console/README.zh.md) |

## TypedData 多层内联

Origo.GodotAdapter 引用 `Origo.SourceGeneration` 源码生成器，通过 `[assembly: SndInlineTypes(startKind: 128, ...)]` 在程序集中注册 14 种 Godot 引擎类型。编译时 SG 自动生成扩展方法（`TryGetVector2` / `AsVector3` 等，`TypedDataLayeredExtensions` 类为 `internal`）、`[ModuleInitializer]` 注册逻辑和 KindResolver/Converter 桥接。

- **程序集加载即注册**：GodotAdapter 程序集被引用或加载时，其生成的 `[ModuleInitializer]` 自动执行 Kind / Converter / TypeMap 注册，无需额外初始化入口。测试通过引用公开类型强制程序集加载。
- **Kind 范围 128–141**：不与 Core 层 1–13 冲突，确保 Core 创建的 `(TypedData)42` 不会在 GodotAdapter 中被误解析为 `Vector2`。

详见 [Origo.SourceGeneration 文档](../Origo.SourceGeneration/README.zh.md)。

---
[↑ 回到 Origo.manual](../README.zh.md)
