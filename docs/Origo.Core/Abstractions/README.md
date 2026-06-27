# Abstractions

> [↑ 回到 Origo.Core](../README.md)

## 模块能力

Origo.Core 的稳定公共抽象层。所有接口在此层定义为平台无关的契约，由下游模块（Core 实现层、Godot 适配层、测试层）具体实现。遵循接口隔离原则（ISP），每个子模块提供一组内聚的接口。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Blackboard](Blackboard/README.md) | 通用键值黑板接口，保留类型信息 | `IBlackboard`：SetValue/Get + 序列化 |
| [Console](Console/README.md) | 控制台输入输出抽象 | `IConsoleInputSource`（轮询）+ `IConsoleOutputChannel`（发布-订阅）|
| [Entity](Entity/README.md) | SND 实体的五项能力接口 + 独立的生命周期接口 | `ISndEntity` = `ISndDataAccess` + `ISndNodeAccess` + `ISndStrategyAccess` + `ISndActiveStrategyAccess` + `ISndObserverStrategyAccess`；`IEntityLifecycle` 为独立接口（框架内部使用）|
| [FileSystem](FileSystem/README.md) | 平台无关文件系统抽象 | `IFileSystem`：13 个文件/目录操作，含路径拼接和父目录。策略不直接使用此接口，而是通过 `ISndFileAccess`（经 `IDataSourceIoGateway` 边界）|
| [Lifecycle](Lifecycle/README.md) | 会话管理抽象接口 | `ISessionManager`（会话生命周期）+ `ISessionRun`（会话运行时门面） |
| [Logging](Logging/README.md) | 引擎无关日志接口 | `ILogger` + `LogLevel` 枚举（Debug/Info/Warning/Error）|
| [Node](Node/README.md) | 抽象引擎节点操作 | `INodeFactory` + `INodeHandle` + `INodeHost`(internal) |
| [Runtime](Runtime/README.md) | 抽象调度接口 | `IScheduler`：Enqueue/Tick/Clear |
| [Scene](Scene/README.md) | SND 场景访问与宿主 | `ISndSceneAccess` + `ISndSceneHost`（SpawnEntity/FindByName/ProcessAll）|
| [Snd](Snd/README.md) | ISndContext 11 个角色接口 | IStateMachineContext 也继承其中部分 |
| [StateMachine](StateMachine/README.md) | 字符串栈状态机体系 | `IStateMachine` + `IStateMachineContext` + `IStateMachineContainer` |

## 接口层级

```
IBlackboard  IConsole*  IFileSystem  ILogger  IScheduler  INode*

ISessionManager  ISessionRun → IStateMachineContainer

ISndEntity ─── ISndDataAccess + ISndNodeAccess + ISndStrategyAccess
                + ISndActiveStrategyAccess + ISndObserverStrategyAccess

IEntityLifecycle                (独立接口，框架内部，非 ISndEntity 子接口)

ISndContext ─── ISndBlackboardAccess + ISndSessionAccess + ISndDeferredActions
               + ISndTemplateAccess + ISndConsoleAccess + ISndStateMachineAccess
               + ISndSaveOperations + ISndLifecycleOperations + ISndEntityOperations
               + ISndFileAccess + ISndArchiveFileAccess

ISndSceneHost ─── ISndSceneAccess

IStateMachine ⟷ IStateMachineContext ⟷ IStateMachineContainer
                    │ (inherits ISndBlackboardAccess + ISndDeferredActions)
```

## 设计原则

- **接口隔离**：大接口拆分为小接口，消费者只依赖需要的部分（如策略只依赖 `ISndDataAccess`，不依赖 `ISndNodeAccess`）
- **平台无关**：所有接口仅使用 `System.*` 类型（`object` 替代 `Godot.Node`）
- **public 白名单**：不为"可能未来有用"提前公开接口，每个 public 接口必须有明确的跨程序集消费者
- **internal 实现接口**：如 `INodeHost` 为 internal，仅在 Core 内部使用

## 本层文件

本目录仅包含子目录，无直接 `.cs` 文件。

---
[↑ 回到 Origo.Core](../README.md)
