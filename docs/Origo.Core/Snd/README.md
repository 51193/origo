# Snd

> [↑ 回到 Origo.Core](../README.md)

## 模块能力

SND（Strategy + Node + Data）实体系统的完整实现。这是 Origo 的核心业务模型——所有游戏实体通过策略表达行为逻辑，通过数据存储可变状态，通过节点映射引擎表现层。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Entity](Entity/README.md) | 运行时实体聚合根 | SndEntity + 数据/节点/策略三个内部管理器 |
| [Metadata](Metadata/README.md) | 实体元数据模型 | TypedData / SndMetaData / NodeMetaData / StrategyMetaData / DataMetaData / SndMetaFluentBuilder |
| [Scene](Scene/README.md) | 场景宿主与运行时门面 | SndRuntime + FullMemorySndSceneHost + StubSndSceneHost |
| [Strategy](Strategy/README.md) | 策略系统核心 | BaseStrategy → LifecycleStrategyBase \| ActiveStrategyBase \| ObserverStrategyBase。策略池、被动/主动/观察者三类管理器 + 泛型调用扩展 |
| [Archetype](Archetype/README.md) | 数值配方加载 | SndArchetypeLoader：键值对文件解析与类型推断 |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `ISndContext.cs` | SND 上下文组合接口：继承 10 个角色接口（[详见 Abstractions/Snd](../Abstractions/Snd/README.md)） |
| `SndContext.cs` | 默认 ISndContext 实现（全局/流程级）。`Bootstrap()` 方法执行完整启动流程：策略发现→别名/模板加载→入口存档加载。实现 `ISndFileAccess`，将文件读写委托给 `SndWorld.DataSourceIo` + `ConverterRegistry` |
| `SndContextParameters.cs` | SndContext 构造参数对象。含 `AutoDiscoverStrategies`、`DiscoverySkipPrefixes`、`SceneAliasMapPath`、`SndTemplateMapPath` 等启动配置属性 |
| `NullSndContext.cs` | 测试用空上下文实现，`ISndFileAccess` 方法均抛 `InvalidOperationException` |
| `SessionSndContext.cs` | 会话级上下文适配器，`ISndFileAccess` 方法委托给全局 ISndContext |
| `SndWorld.cs` | SND 世界：策略池 + 类型映射 + 转换器注册表 + 模板/别名 |
| `SndDefaults.cs` | `internal` — SND 系统默认值常量 |
| `SndMappings.cs` | 场景别名解析 + 模板注册与解析 |
| `SndTemplateResolver.cs` | 模板解析器：支持 JSON 数组和 .map 简写两种模板格式 |
| `TryGetNumericExtensions.cs` | 实体数据数值类型兼容读取扩展：桥接 int/float 等类型存取不匹配 |
| `ActiveStrategyExtensions.cs` | 泛型 ActiveStrategy 调用扩展：消除 `InvokeStrategy` 侧的 JSON 序列化样板 |
| `LevelBuilder.cs` | 离线关卡构建工具 |

## 实体模型

```
SndEntity (聚合根)
├── SndDataManager
│   ├── DataObserverManager (数据键 → 订阅回调，供观察者策略接线)
│   └── Dictionary<string, TypedData> (数据存储，唯一权威状态)
├── SndNodeManager : INodeHost
│   ├── Dictionary<string, INodeHandle> (节点存储)
│   └── INodeFactory (节点创建，由适配层注入)
├── SndStrategyManager (被动策略)
│   ├── List<StrategyEntry> (按优先级排序，每帧遍历)
│   └── SndStrategyPool (全局策略池引用)
├── ActiveStrategyManager (主动策略)
│   ├── Dictionary<string, ActiveStrategyBase> (O(1) 按索引查找)
│   └── SndStrategyPool (共享同一池实例)
└── ObserverStrategyManager (观察者策略)
    ├── 观察者绑定拓扑 (target → observerIndices)
    └── 数据变更接线（经 ISndEntityRawSubscription）+ 绑定序列化/恢复
```

## 策略生命周期钩子（顺序）

1. **AfterSpawn** — 实体新生成后
2. **AfterLoad** — 实体从存档恢复后
3. **AfterAdd** — 策略动态添加到实体后
4. **Process** — 每帧执行（按优先级）
5. **BeforeRemove** — 策略从实体移除前
6. **BeforeSave** — 序列化存档前
7. **BeforeQuit** — 实体正常退出前
8. **BeforeDead** — 实体销毁前

> **批量生命周期（batch orchestration）：** `SpawnEntity`、`RecoverFromMetaList`、`RemoveAllEntities` 为整体操作，不逐实体触发 AfterSpawn / AfterLoad / BeforeDead 钩子。钩子统一由上层（SaveContext.BuildSndScene / RecoverSndScene、SessionRun 生命周期）在批量操作完成后按优先级集中触发。

## 观察系统

SND 的观察统一由观察者策略（`ObserverStrategyBase`）承载，自观察和跨实体观察使用同一套挂载 API 与同一绑定拓扑：

- **声明观察键**：`[ObserveData("hp")]` 特性声明策略关心的数据键（支持多重声明）
- **响应数据变更**：实现 `OnDataChanged(entity, ctx, target, dataKey, oldValue, newValue)`
- **挂载/卸载回调**：`OnMounted` / `OnUnmounted`
- **自观察**：`entity.MountObserverStrategy(entity.Name, "my_game.hp_watcher")`
- **跨实体观察**：先 `SceneHost.FindByName` 解析目标，再 `observer.MountObserverStrategy(target, "...")`

观察者绑定拓扑通过 `StrategyMetaData.ObserverIndices` 随实体序列化，读档时由 `ObserverStrategyManager` 自动恢复接线，无需在 `AfterLoad` 中手动重连；目标或观察方死亡时自动卸载。公开接口见 [ISndObserverStrategyAccess](../Abstractions/Entity/README.md#isndobserverstrategyaccess)，策略类型见 [Strategy](Strategy/README.md)，实现细节见 [SndEntity](Entity/README.md#sndentity聚合根)。

## 核心原则

- **策略无状态**：策略实例共享，可变状态在实体 Data 中
- **节点解耦**：Core 不持有引擎节点引用，通过 `INodeHandle` 抽象操作
- **元数据驱动**：实体的创建/恢复/序列化全部通过 `SndMetaData` 中介
- **内联存储**：`TypedData` 为值类型（struct），通过 Source Generator 生成的判别联合在 `_inlineBits` 中内联存储 ≤ 8 字节的值类型，零装箱零堆分配
- **统一观察**：自观察与跨实体观察走同一套观察者策略 API，绑定拓扑随实体持久化、读档自动恢复，死亡时自动卸载

## 启动流程

`SndContext.Bootstrap()` 按固定顺序执行 Core 的全部初始化操作：

1. **策略发现**：若 `SndContextParameters.AutoDiscoverStrategies` 为 true，通过 `OrigoAutoInitializer.DiscoverAndRegisterStrategies()` 扫描程序集中的 `[StrategyIndex]` 注解类型，使用 `DiscoverySkipPrefixes` 过滤适配层程序集
2. **场景别名加载**：若 `SceneAliasMapPath` 非空，调用 `SndWorld.LoadSceneAliases()`
3. **SND 模板加载**：若 `SndTemplateMapPath` 非空，调用 `SndWorld.LoadTemplates()`
4. **入口存档加载**：调用 `RequestLoadMainMenuEntrySave()`

适配层仅通过 `SndContextParameters` 传入配置，不需要知道上述步骤的执行顺序和内部实现。

### 为什么启动编排集中在 SndContext.Bootstrap()

适配层不应直接调用 `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`、`LoadSceneAliases()`、`LoadTemplates()`、`RequestLoadMainMenuEntrySave()`。这些是 Core 内部编排操作——策略发现必须在 Core 层执行（使用适配层提供的 skip prefixes），别名/模板加载是 Core 配置解析，入口存档加载是 Core 生命周期入口。统一在 `Bootstrap()` 中执行确保这些操作以正确的依赖顺序在正确的层中完成。

---
[↑ 回到 Origo.Core](../README.md)
