<!-- docsync-pair: Origo.Core.Tests/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Origo.Core.Tests

> [↑ 回到 Origo.manual](../README.zh.md)
> [↔ 测试文档元指令](META-TEST.zh.md)

## 测试策略概述

Origo.Core 的测试遵循"**面向行为、面向文档契约**"原则：

- **不测试 internal 实现细节**：每个测试验证 `usage/` 或模块文档中描述的某条行为契约，而非代码的内部形状。原则：若可通过 `ISndContext`/`ISessionManager` 等公共接口验证行为，则不应使用 `InternalsVisibleTo` 直接访问 internal 类型。详见 [测试文档元指令 — InternalsVisibleTo 白名单原则](META-TEST.zh.md#internalsvisibleto-白名单原则)。
- **正确路径、错误路径、边界路径同等覆盖**：每个能力文档按三类路径组织测试方法。
- **使用 TestMemoryFileSystem**：所有文件 I/O 测试使用内存文件系统（`TestMemoryFileSystem`），不涉及真实磁盘操作。策略测试上下文（`StrategyTestContext`）内置 `MemoryFileSystem` 全链路，支持 ISndFileAccess 行为验证。
- **策略隔离测试**：`StrategyTestScenario` 框架允许在完全无运行时的环境下测试单个策略的生命周期。

## 测试辅助设施

测试项目通过 `TestSupport/` 文件提供以下核心辅助设施：

| 设施 | 类型 | 用途 |
|------|------|------|
| `TestMemoryFileSystem` | `IFileSystem` 实现 | 内存文件系统，支持完整的读写/枚举/复制/重命名/删除，用于 I/O 测试 |
| `TestSndSceneHost` | `ISndSceneHost` 实现 | 无渲染的场景宿主，维护实体列表，记录 Spawn/ClearAll 调用 |
| `TestLogger` | `ILogger` 实现 | 收集日志到列表中，支持按级别（Debug/Info/Warning/Error）分类查询 |
| `TestNodeFactory` | `INodeFactory` 实现 | 可注入失败资源的节点工厂 |
| `DummySndEntity` | `ISndEntity` 实现 | 内存中的实体实现，提供 SetData/GetData/TryGetData |
| `NullSndContext` | `ISndContext` 空对象实现 | 纯运行时单测用的空上下文；查询返回空对象，变更操作（存读档/关卡切换等）显式抛异常以满足 fail-fast |
| `StrategyStateTestsCollection` | xUnit `[CollectionDefinition]` | 定义 `StrategyStateTests` 串行集合（`DisableParallelization`），供含静态可变状态的策略测试类串行运行，防止跨测试污染 |
| `TestFactory` | 静态工厂类 | 快速创建 OrigoRuntime / SndWorld / ProgressRun / ConverterRegistry 等常用组合 |
| `GameplaySimulationHarness` | Fluent Builder + Harness | 一键创建完整帧驱动游戏模拟环境：OrigoRuntime + SndContext + 后台游戏会话（syncProcess=true），支持 DriveFrame/RunFrames/SpawnEntity/GetEntityData/SaveAndReload |
| `TestStrategies` | 抽象基类集合 | `SharedFrameCounterStrategy`、`SharedEchoActiveStrategy`、`SharedKillProbeStrategy`、`SharedNoopLifecycleStrategy`、`SharedNoopStateMachineStrategy` — 供集成测试文件通过 1 行 sealed 子类引用，消除重复策略定义 |
| `TestObserverEvents` | 结构化事件记录 | `TestObserverEvent` record（EventType/TargetName/DataKey/OldValue/NewValue）+ `EventCollector` 静态 AsyncLocal 收集器 + `SharedDataChangeObserverStrategy` 抽象基类 — 观察者测试断言从子串匹配升级为类型化字段精确比较 |
| `PerfReporter` | 静态工具类 | 性能测试输出格式化：Compare/Report 方法，打印时间/吞吐/分配对比。支持双通道输出（`Console.Out` + `ITestOutputHelper`），确保 CI 和本地均可看到结果 |
| `ConsoleInputBuffer` | `IConsoleInputSource` 实现 | 控制台输入队列（Core 生产代码，测试中直接使用） |
| `ConsoleOutputChannel` | `IConsoleOutputChannel` 实现 | 控制台输出通道（Core 生产代码，测试中直接使用） |

框架内部的测试辅助设施（如 `SndEntity` 的生命周期方法）为 `internal`，通过 `InternalsVisibleTo` 暴露给测试项目；独立的 `Origo.TestSupport` 程序集则提供可公开复用的测试支撑类型（TestMemoryFileSystem、TestSndSceneHost、TestLogger 等）。

## 能力文档索引

测试按 **被测试的能力** 分组，每个文档对应一种独立能力：

| 能力 | 文档 | 验证重点 |
|------|------|---------|
| 架构守卫 | [Architecture.md](Architecture.zh.md) | 分层隔离（Core 不引用 Godot）、接口组合（ISndContext 纯组合）、策略无状态校验 |
| 测试替身 | [Abstractions.md](Abstractions.zh.md) | TestMemoryFileSystem / NullLogger / TestMemoryFileSystemAdditional 的正确性 |
| 黑板 | [Blackboard.md](Blackboard.zh.md) | Set/Get/TryGet/Clear/SerializeAll/DeserializeAll 全生命周期 + 键校验 |
| 数据观察者 | [DataObserver.md](DataObserver.zh.md) | Subscribe/Unsubscribe/Notify/多订阅者/重入安全/Clear |
| 数据源 | [DataSource.md](DataSource.zh.md) | DataSourceNode 创建/访问/懒展开、JSON 编解码、Map 编解码、类型转换器注册、TypedData 转换器、SndMetaData 转换器、IDisposable |
| 日志 | [Logging.md](Logging.zh.md) | LogMessageBuilder 结构化构建（prefix/suffix/elapsed） |
| 网格 | [Grid.md](Grid.zh.md) | GridCoordinateSystem 单/双轴转换、A* 寻路、GridParser 坐标解析 |
| 随机数 | [Random.md](Random.zh.md) | XorShift128+ 种子确定性、噪声图生成 |
| 规划 | [Planning.md](Planning.zh.md) | PlanExecutionStrategyBase：意图驱动计划执行、Action 策略自动插拔、步骤推进 |
| 类型序列化 | [TypeStringMapping.md](TypeStringMapping.zh.md) | TypeStringMapping 双向映射、BCL 预注册、冲突检测 |
| 调度 | [Scheduling.md](Scheduling.zh.md) | ConcurrentActionQueue 入队/排空/并发安全/递归深度保护 |
| 控制台 | [Console.md](Console.zh.md) | 命令解析器/路由器/输入队列/输出通道、14 个内置命令处理（11 Core + 3 GodotAdapter）、类型推断 |
| 运行时核心 | [Runtime-Core.md](Runtime-Core.zh.md) | OrigoRuntime 构造、控制台注入、帧延迟动作执行 |
| 会话生命周期 | [Session-Lifecycle.md](Session-Lifecycle.zh.md) | 会话创建/销毁/切换、Dispose 语义、前后台协议一致、拓扑编解码 |
| 持久化：存储 | [Save-Storage.md](Save-Storage.zh.md) | 两阶段写入、write_in_progress marker 契约、关卡三件套完整性、路径策略、快照读写、幂等去重 |
| 持久化：序列化 | [Save-Serialization.md](Save-Serialization.zh.md) | BlackboardSerializer、SndSceneSerializer、SaveContext 编排 |
| 持久化：元数据 | [Save-Meta.md](Save-Meta.zh.md) | ISaveMetaContributor、SaveMetaMerger、meta.map 编解码 |
| SND 实体 | [Snd-Entity.md](Snd-Entity.zh.md) | SndEntity CRUD、AfterLoad 钩子、AutoInitializer 恢复、批量生命周期、所属会话绑定 |
| SND 元数据 | [Snd-Metadata.md](Snd-Metadata.zh.md) | TypedData struct 值语义与 IEquatable、SndMetaData 深拷贝、SG 输出验证、Fluent 构建、TypedData 集成 |
| 性能基准 | [Benchmarks.md](Benchmarks.zh.md) | `[Category=Benchmark]` 套件（`benchmark.sh` 独立运行）：TypedData 真实模拟 + 实体生命周期 + Observer 拓扑 + DataSourceNode + Blackboard + Save + 并发队列 + 随机数 + Strategy 性能 |
| SND 场景 | [Snd-Scene.md](Snd-Scene.zh.md) | MemorySndSceneHost 与 FullMemorySndSceneHost 的 Spawn/FindByName/LoadFromMetaList/ClearAll/CreateEntity/RemoveEntity/RequestKillEntity、NullNodeFactory |
| SND 策略 | [Snd-Strategy.md](Snd-Strategy.zh.md) | 策略优先级排序、池引用计数/回收、实体策略生命周期钩子、观察者策略、主动策略 Invoke、策略池 Get/Release 与 Process 缩放性能测量 |
| SND 上下文 | [Snd-Context.md](Snd-Context.zh.md) | SndContext save/load/continue 工作流、LevelBuilder、模板解析、Archetype 加载 |
| SND 扩展 | [Snd-Extensions.md](Snd-Extensions.zh.md) | EnsureStrategy 惰性策略挂载（幂等）、TryGetNumeric 跨数值类型读取、InvokeStrategy 泛型调用 |
| 文件访问 | [Snd-FileAccess.md](Snd-FileAccess.zh.md) | ISndFileAccess 在 SndContext 上的 DataSourceNode 读写往返、强类型往返、overwrite 语义、错误/边界路径 |
| 策略测试上下文文件访问 | [StrategyTestContext-FileAccess.md](StrategyTestContext-FileAccess.zh.md) | ISndFileAccess 在 StrategyTestContext 上的内存文件系统行为、DataSourceNode 和强类型往返 |
| 存档文件访问 | [Snd-ArchiveFileAccess.md](Snd-ArchiveFileAccess.zh.md) | ISndArchiveFileAccess 在 SndContext 上的 extra/ 子目录文件操作、DeleteFile、路径穿越防护、save/load 往返 |
| 状态机 | [StateMachine.md](StateMachine.zh.md) | StackStateMachine 压栈/出栈/恢复/FlushAfterLoad、空栈/空串/Dispose 边界测试、容器 CreateOrGet/序列化 |
| 策略测试框架 | [StrategyTestScenario.md](StrategyTestScenario.zh.md) | 三阶段模式（configure/run/assert）、EntityStrategy harness、ActiveStrategy harness |
| 帧驱动集成测试 | [Testing/Integration/Integration.md](Testing/Integration/Integration.zh.md) | GameplaySimulationHarness 完整运行时模拟：SndContext → Bootstrap → DriveFrame 帧循环 → 实体处理/黑板交互/延迟动作 |
| 集合差异比较 | [Utility.md](Utility.zh.md) | DiffUtility 泛型集合差异比较（added/removed）+ 去重语义 |
| 框架元信息 | [Meta.md](Meta.zh.md) | OrigoMeta 记录：默认横幅非空、ToString 含名称与版本、值相等语义 |

---

[↑ 回到 Origo.manual](../README.zh.md)
