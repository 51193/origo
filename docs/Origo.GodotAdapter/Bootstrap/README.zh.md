<!-- docsync-pair: Origo.GodotAdapter/Bootstrap/README -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Bootstrap

> [↑ 回到 Origo.GodotAdapter](../README.zh.md)

## 概述

Godot 适配层的启动与编排。通过 `Origo.Core.Kernel.Ports.AdapterHostKernelPort` 创建完整运行时栈（kernel `OrigoRuntime` + 内部 `GodotSndManager`），注册 Godot 特有的类型映射、序列化转换器和命令处理器。所有 Godot 特有的依赖在适配层注入，Core 层不感知。

## 包含文件

| 文件 | 职责 |
|------|------|
| `OrigoAutoHost.cs` | Godot Node，准备 GodotFileSystem、类型映射和场景宿主后，调用 `AdapterHostKernelPort` 创建运行时、system blackboard、console 通道与 observer topology。公开 `Runtime` 为 `IOrigoRuntime`；`_Process` 委托给 `IOrigoFrameDriver.DriveFrame(delta)` |
| `OrigoDefaultEntry.cs` | 继承 OrigoAutoHost，持有启动配置属性（`AutoDiscoverStrategies`、`_godotSkipPrefixes`（`private static readonly` 字段）、`SceneAliasMapPath` 等），公开 `Context` 供表现层读取统一业务门面，并提供 `ConfigureStrategies` 等受保护启动钩子 |
| `OrigoDefaultEntry.Bootstrap.cs` | partial class，`_Ready` 实现：ConfigureStrategies(`ISndWorldAccess`) → 通过 port 注册命令处理器 → 通过 `AdapterHostKernelPort.CreateContext` 创建并绑定 SndContext → 调用 `Bootstrap()`；任何步骤失败都会标记启动失败（`MarkBootstrapFailed`），使下一帧 fail-fast |

## 启动流程

```
OrigoDefaultEntry._Ready()
  └── base._Ready()                          // OrigoAutoHost
       └── AdapterHostKernelPort.CreateRuntime(...)
            ├── GodotFileSystem + GodotJsonConverterRegistry 注册回调
            ├── kernel 创建 TypeStringMapping/Registry/IO/Meta/Path
            ├── PersistentBlackboard(...) → LoadFromDisk()
            ├── ConsoleInputBuffer / ConsoleOutputChannel（可按 options 注入）
            ├── kernel 创建 OrigoRuntime
            └── ISndSceneHostRuntimeBinder.BindRuntimeDependencies(...)  // 绑定 world/logger 与 observer topology
  ├── ConfigureStrategies(Runtime.SndWorld)  // ISndWorldAccess；手动策略注册（Bootstrap 冻结前）
  ├── RegisterConsoleCommandHandlers()       // port 注册适配层命令处理器
  ├── AdapterHostKernelPort.CreateContext(...)  // 传入启动配置
  ├── Context = sndContext                   // 暴露给表现层/游戏代码
  └── sndContext.Bootstrap()                 // Core 内部编排：

SndContext.Bootstrap() 内部顺序：
  1. 策略发现       (OrigoAutoInitializer.DiscoverAndRegisterStrategies)
  2. 排序校验与注册冻结 (SndStrategyPool.SealRegistration)
  3. 场景别名加载   (SndWorld.LoadSceneAliases)
  4. SND 模板加载   (SndWorld.LoadTemplates)
  5. 入口存档加载   (RequestLoadMainMenuEntrySave)
```

## 设计决策

### 为什么 kernel port 分两步创建（Runtime 再 Context）

`SndWorld` 隐藏 `Origo` 重构细节。Runtime 先由 `AdapterHostKernelPort.CreateRuntime` 创建，并立即通过 `ISndSceneHostRuntimeBinder` 绑定 world/logger 与 per-scene observer topology；`ISndContext` 随后由同一 port 的 `CreateContext` 创建并绑定到场景宿主。这样适配层既不复制 Core 的构造顺序，也能在 Context 就绪前保持场景宿主可用。

### 为什么 OrigoDefaultEntry 是 partial class

启动逻辑（`OrigoDefaultEntry.Bootstrap.cs`）与导出属性定义（`OrigoDefaultEntry.cs`）分离。Godot 在场景编辑器中展示的 [Export] 属性在主文件中更清晰，而编排逻辑在独立文件中，便于维护。

### 为什么策略发现过滤 Godot 前缀

`OrigoAutoInitializer.DiscoverAndRegisterStrategies` 扫描当前 AppDomain 中所有程序集。Godot 和 GodotSharp 的程序集包含大量非策略类，过滤前缀避免无效扫描和注册错误。此前缀通过 `SndContextParameters.DiscoverySkipPrefixes` 传入 Core，而非在适配层硬编码。


### 为什么策略注册必须在 Bootstrap 前完成

生命周期策略的 `Before` / `After` 约束在完整注册图上校验，注册表必须在任何实体创建前冻结。`SndContext.Bootstrap()` 在策略发现后调用 `SndStrategyPool.SealRegistration()`：未知目标、非生命周期目标、自引用或环立即抛异常，冻结后 `SndWorld.RegisterStrategy` 抛异常。默认的 `AutoDiscoverStrategies` 会扫描带 `[StrategyIndex]` 的类型；需要手动注册的策略可在派生入口中覆写 `ConfigureStrategies(ISndWorldAccess)`，该钩子在 `Bootstrap()` 之前调用。策略顺序的完整契约见 [Snd/Strategy](../../Origo.Core/Snd/Strategy/README.zh.md)。

### 为什么公开 Context

`OrigoAutoHost` 公开稳定的 `IOrigoRuntime`，而表现层常见需求（存档列表、continue 可用性、生命周期入口、模板与黑板查询）都集中在 `ISndContext`。`Context` 与宿主入口保持同一生命周期，在 `_Ready()` 中创建并赋值，`ConfigureSaveMetadataContributors` 收到同一实例。

### 为什么启动编排集中在 SndContext.Bootstrap()

适配层不应直接调用 `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`、`LoadSceneAliases()`、`LoadTemplates()`、`RequestLoadMainMenuEntrySave()`；策略发现与 JSON 实体列表 spawn 现在已是编译器层面的 `internal`，仅 `SndContext.Bootstrap` 可达。模板/别名 map 的运行期重载应使用公开 companion：`ctx.Template.LoadTemplates(...)` / `ctx.Template.LoadSceneAliases(...)`。这些是 Core 内部编排操作——策略发现与排序校验必须在 Core 层执行，别名/模板加载是 Core 配置解析，入口存档加载是 Core 生命周期入口。适配层仅通过 `AdapterContextOptions` 传入配置参数，port 构造参数并绑定 context，`Bootstrap()` 确保这些操作以正确的依赖顺序在正确的层中完成。

---
[↑ 回到 Origo.GodotAdapter](../README.zh.md)
