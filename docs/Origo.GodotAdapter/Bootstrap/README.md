# Bootstrap

> [↑ 回到 Origo.GodotAdapter](../README.md)

## 概述

Godot 适配层的启动与编排。负责创建完整的运行时栈（`OrigoRuntime` + `GodotSndManager`），注册 Godot 特有的类型映射、序列化转换器和命令处理器。所有 Godot 特有的依赖在适配层注入，Core 层不感知。

## 包含文件

| 文件 | 职责 |
|------|------|
| `OrigoAutoHost.cs` | Godot Node，创建运行时：GodotFileSystem + TypeStringMapping + ConverterRegistry + PersistentBlackboard + ConsoleInput/Output。`_Process` 委托给 `IOrigoFrameDriver.DriveFrame(delta)` |
| `OrigoDefaultEntry.cs` | 继承 OrigoAutoHost，持有启动配置属性（`AutoDiscoverStrategies`、`GodotSkipPrefixes`、`SceneAliasMapPath` 等） |
| `OrigoDefaultEntry.Bootstrap.cs` | partial class，`_Ready` 实现：注册命令处理器 → 创建 SndContext → 调用 `Bootstrap()` |
| `GodotSndBootstrap.cs` | 工具方法：将 Runtime 依赖和 SessionContext 分步绑定到 GodotSndManager |

## 启动流程

```
OrigoDefaultEntry._Ready()
  └── base._Ready()                          // OrigoAutoHost
       └── CreateRuntime()
            ├── new GodotFileSystem()
            ├── CreateAndSetupSndManager()
            │    ├── new GodotSndManager()
            │    ├── GodotJsonConverterRegistry.RegisterTypeMappings(...)
            │    ├── DataSourceFactory.CreateDefaultRegistry(...)
            │    ├── GodotJsonConverterRegistry.RegisterDataSourceConverters(...)
            │    └── new PersistentBlackboard(...) → LoadFromDisk()
            ├── new ConsoleInputBuffer()
            ├── new ConsoleOutputChannel()
            └── new OrigoRuntime(...)
       └── sndManager.BindRuntimeDependencies(world, logger)
  ├── RegisterConsoleCommandHandlers()       // 适配层命令处理器
  ├── new SndContext(new SndContextParameters(...) {  // 传入启动配置
  │       AutoDiscoverStrategies = ...,
  │       DiscoverySkipPrefixes = ...,
  │       SceneAliasMapPath = ...,
  │       SndTemplateMapPath = ...
  │   })
  ├── SndManager.BindContext(sndContext)
  └── sndContext.Bootstrap()                 // Core 内部编排：

SndContext.Bootstrap() 内部顺序：
  1. 策略发现       (OrigoAutoInitializer.DiscoverAndRegisterStrategies)
  2. 场景别名加载   (SndWorld.LoadSceneAliases)
  3. SND 模板加载   (SndWorld.LoadTemplates)
  4. 入口存档加载   (RequestLoadMainMenuEntrySave)
```

## 设计决策

### 为什么分两步绑定（RuntimeDependencies 再 Context）

`SndWorld` 在 `OrigoRuntime` 构造时创建，但 `ISndContext` 在更晚的阶段（配置、策略发现之后）才创建。两步绑定让 GodotSndManager 在 Runtime 创建后即可与 World 交互（如预加载），在 Context 就绪后再获得持久化和场景能力。

### 为什么 OrigoDefaultEntry 是 partial class

启动逻辑（`OrigoDefaultEntry.Bootstrap.cs`）与导出属性定义（`OrigoDefaultEntry.cs`）分离。Godot 在场景编辑器中展示的 [Export] 属性在主文件中更清晰，而编排逻辑在独立文件中，便于维护。

### 为什么策略发现过滤 Godot 前缀

`OrigoAutoInitializer.DiscoverAndRegisterStrategies` 扫描当前 AppDomain 中所有程序集。Godot 和 GodotSharp 的程序集包含大量非策略类，过滤前缀避免无效扫描和注册错误。此前缀通过 `SndContextParameters.DiscoverySkipPrefixes` 传入 Core，而非在适配层硬编码。

### 为什么启动编排集中在 SndContext.Bootstrap()

适配层不应直接调用 `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`、`LoadSceneAliases()`、`LoadTemplates()`、`RequestLoadMainMenuEntrySave()`。这些是 Core 内部编排操作——策略发现必须在 Core 层执行，别名/模板加载是 Core 配置解析，入口存档加载是 Core 生命周期入口。适配层仅通过 `SndContextParameters` 传入配置参数，`Bootstrap()` 确保这些操作以正确的依赖顺序在正确的层中完成。

---
[↑ 回到 Origo.GodotAdapter](../README.md)
