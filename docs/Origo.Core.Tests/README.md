# Origo.Core.Tests

> [↑ 回到 Origo.manual](../README.md)
> [↔ 测试文档元指令](META-TEST.md)

## 测试策略概述

Origo.Core 的测试遵循"**面向行为、面向文档契约**"原则：

- **不测试 internal 实现细节**：每个测试验证 `usage/` 或模块文档中描述的某条行为契约，而非代码的内部形状。原则：若可通过 `ISndContext`/`ISessionManager` 等公共接口验证行为，则不应使用 `InternalsVisibleTo` 直接访问 internal 类型。详见 [测试文档元指令 — InternalsVisibleTo 白名单原则](META-TEST.md#internalsvisibleto-白名单原则)。
- **正确路径、错误路径、边界路径同等覆盖**：每个能力文档按三类路径组织测试方法。
- **使用 TestFileSystem**：所有文件 I/O 测试使用内存文件系统（`TestFileSystem`），不涉及真实磁盘操作。策略测试上下文（`StrategyTestContext`）内置 `MemoryFileSystem` 全链路，支持 ISndFileAccess 行为验证。
- **策略隔离测试**：`StrategyTestScenario` 框架允许在完全无运行时的环境下测试单个策略的生命周期。`TestContextBuilder` 提供集成测试场景下的 SndContext 构造。

## 测试辅助设施

测试项目通过 `TestSupport/` 文件提供以下核心辅助设施：

| 设施 | 类型 | 用途 |
|------|------|------|
| `TestFileSystem` | `IFileSystem` 实现 | 内存文件系统，支持完整的读写/枚举/复制/重命名/删除，用于 I/O 测试 |
| `TestSndSceneHost` | `ISndSceneHost` 实现 | 无渲染的场景宿主，维护实体列表，记录 Spawn/ClearAll 调用 |
| `TestLogger` | `ILogger` 实现 | 收集日志到列表中，支持按级别（Debug/Info/Warning/Error）分类查询 |
| `TestNodeFactory` | `INodeFactory` 实现 | 可注入失败资源的节点工厂 |
| `DummySndEntity` | `ISndEntity` 实现 | 内存中的实体实现，提供 SetData/GetData/TryGetData |
| `TestFactory` | 静态工厂类 | 快速创建 OrigoRuntime / SndWorld / ProgressRun / ConverterRegistry 等常用组合 |
| `TestContextBuilder` | Fluent Builder | 构造 `SndContext` 实例（集成测试），提供合理默认值和可选覆盖，替代重复的 10 行构造模式 |
| `GameplaySimulationHarness` | Fluent Builder + Harness | 一键创建完整帧驱动游戏模拟环境：OrigoRuntime + SndContext + 后台游戏会话（syncProcess=true），支持 DriveFrame/RunFrames/SpawnEntity/GetEntityData/SaveAndReload |
| `TestStrategies` | 抽象基类集合 | `SharedFrameCounterStrategy`、`SharedEchoActiveStrategy`、`SharedKillProbeStrategy`、`SharedNoopLifecycleStrategy`、`SharedNoopStateMachineStrategy` — 供集成测试文件通过 1 行 sealed 子类引用，消除重复策略定义 |
| `TestObserverEvents` | 结构化事件记录 | `TestObserverEvent` record（EventType/TargetName/DataKey/OldValue/NewValue）+ `EventCollector` 静态 AsyncLocal 收集器 + `SharedDataChangeObserverStrategy` 抽象基类 — 观察者测试断言从子串匹配升级为类型化字段精确比较 |
| `PerfReporter` | 静态工具类 | 性能测试输出格式化：Compare/Report 方法，打印时间/吞吐/分配对比。支持双通道输出（`Console.Out` + `ITestOutputHelper`），确保 CI 和本地均可看到结果 |
| `ConsoleInputBuffer` | `IConsoleInputSource` 实现 | 控制台输入队列（Core 生产代码，测试中直接使用） |
| `ConsoleOutputChannel` | `IConsoleOutputChannel` 实现 | 控制台输出通道（Core 生产代码，测试中直接使用） |

所有测试辅助设施均为 `internal`，通过 `InternalsVisibleTo` 暴露给测试项目。

## 能力文档索引

测试按 **被测试的能力** 分组，每个文档对应一种独立能力：

| 能力 | 文档 | 验证重点 |
|------|------|---------|
| 架构守卫 | [Architecture.md](Architecture.md) | 分层隔离（Core 不引用 Godot）、接口组合（ISndContext 纯组合）、策略无状态校验 |
| 测试替身 | [Abstractions.md](Abstractions.md) | TestFileSystem / NullLogger / TestFileSystemAdditional 的正确性 |
| 黑板 | [Blackboard.md](Blackboard.md) | Set/Get/TryGet/Clear/SerializeAll/DeserializeAll 全生命周期 + 键校验 |
| 数据观察者 | [DataObserver.md](DataObserver.md) | Subscribe/Unsubscribe/Notify/多订阅者/重入安全/Clear |
| 数据源 | [DataSource.md](DataSource.md) | DataSourceNode 创建/访问/懒展开、JSON 编解码、Map 编解码、类型转换器注册、TypedData 转换器、SndMetaData 转换器、IDisposable |
| 日志 | [Logging.md](Logging.md) | LogMessageBuilder 结构化构建（prefix/suffix/elapsed） |
| 网格 | [Grid.md](Grid.md) | GridCoordinateSystem 单/双轴转换、A* 寻路、GridParser 坐标解析 |
| 随机数 | [Random.md](Random.md) | XorShift128+ 种子确定性、噪声图生成 |
| 规划 | [Planning.md](Planning.md) | PlanExecutionStrategyBase：意图驱动计划执行、Action 策略自动插拔、步骤推进 |
| 类型序列化 | [TypeStringMapping.md](TypeStringMapping.md) | TypeStringMapping 双向映射、BCL 预注册、冲突检测 |
| 调度 | [Scheduling.md](Scheduling.md) | ConcurrentActionQueue 入队/排空/并发安全/递归深度保护 |
| 控制台 | [Console.md](Console.md) | 命令解析器/路由器/输入队列/输出通道、13 个内置命令处理（11 Core + 2 GodotAdapter）、类型推断 |
| 运行时核心 | [Runtime-Core.md](Runtime-Core.md) | OrigoRuntime 构造、控制台注入、帧延迟动作执行 |
| 会话生命周期 | [Session-Lifecycle.md](Session-Lifecycle.md) | 会话创建/销毁/切换、Dispose 语义、前后台协议一致、拓扑编解码 |
| 持久化：存储 | [Save-Storage.md](Save-Storage.md) | 两阶段写入、write_in_progress marker 契约、关卡三件套完整性、路径策略、快照读写、幂等去重 |
| 持久化：序列化 | [Save-Serialization.md](Save-Serialization.md) | BlackboardSerializer、SndSceneSerializer、SaveContext 编排 |
| 持久化：元数据 | [Save-Meta.md](Save-Meta.md) | ISaveMetaContributor、SaveMetaMerger、meta.map 编解码 |
| SND 实体 | [Snd-Entity.md](Snd-Entity.md) | SndEntity CRUD、AfterLoad 钩子、AutoInitializer 恢复、批量生命周期、所属会话绑定 |
| SND 元数据 | [Snd-Metadata.md](Snd-Metadata.md) | TypedData struct 值语义与 IEquatable、SndMetaData 深拷贝、SG 输出验证、Fluent 构建、TypedData 集成 |
| 真实模拟性能基准 | [Benchmarks.md](Benchmarks.md) | `[Category=Benchmark]` 套件（`benchmark.sh` 独立运行）：字典查找/插入、数值强转链、观察者通知、异构字典迭代，生成 `TypedData` vs 装箱字典 |
| SND 场景 | [Snd-Scene.md](Snd-Scene.md) | MemorySndSceneHost 与 FullMemorySndSceneHost 的 Spawn/FindByName/LoadFromMetaList/ClearAll/CreateEntity/RemoveEntity/RequestKillEntity、NullNodeFactory |
| SND 策略 | [Snd-Strategy.md](Snd-Strategy.md) | 策略优先级排序、池引用计数/回收、实体策略生命周期钩子、观察者策略、主动策略 Invoke、策略池 Get/Release 与 Process 缩放性能测量 |
| SND 上下文 | [Snd-Context.md](Snd-Context.md) | SndContext save/load/continue 工作流、LevelBuilder、模板解析、Archetype 加载 |
| SND 扩展 | [Snd-Extensions.md](Snd-Extensions.md) | EnsureStrategy 惰性策略挂载（幂等）、TryGetNumeric 跨数值类型读取、InvokeStrategy 泛型调用 |
| 文件访问 | [Snd-FileAccess.md](Snd-FileAccess.md) | ISndFileAccess 在 SndContext 上的 DataSourceNode 读写往返、强类型往返、overwrite 语义、错误/边界路径 |
| 策略测试上下文文件访问 | [StrategyTestContext-FileAccess.md](StrategyTestContext-FileAccess.md) | ISndFileAccess 在 StrategyTestContext 上的内存文件系统行为、DataSourceNode 和强类型往返 |
| 存档文件访问 | [Snd-ArchiveFileAccess.md](Snd-ArchiveFileAccess.md) | ISndArchiveFileAccess 在 SndContext 上的 extra/ 子目录文件操作、DeleteFile、路径穿越防护、save/load 往返 |
| 状态机 | [StateMachine.md](StateMachine.md) | StackStateMachine 压栈/出栈/恢复/FlushAfterLoad、空栈/空串/Dispose 边界测试、容器 CreateOrGet/序列化 |
| 策略测试框架 | [StrategyTestScenario.md](StrategyTestScenario.md) | 三阶段模式（configure/run/assert）、EntityStrategy harness、ActiveStrategy harness |
| 帧驱动集成测试 | [Integration/README.md](Integration/README.md) | GameplaySimulationHarness 完整运行时模拟：SndContext → Bootstrap → DriveFrame 帧循环 → 实体处理/黑板交互/延迟动作 |
| 集合差异比较 | [Utility.md](Utility.md) | DiffUtility 泛型集合差异比较（added/removed）+ 去重语义 |

---

[↑ 回到 Origo.manual](../README.md)
