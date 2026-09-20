<!-- docsync-pair: Origo.Core/Abstractions/README -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Abstractions

> [↑ 回到 Origo.Core](../README.zh.md)

## 模块能力

Origo.Core 的稳定公共抽象层。所有接口在此层定义为平台无关的契约，由下游模块（Core 实现层、Godot 适配层、测试层）具体实现。遵循接口隔离原则（ISP），每个子模块提供一组内聚的接口。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Entity](Entity/README.zh.md) | SND 实体内部生命周期契约 | `IEntityLifecycle`(internal)；公开实体角色接口位于 Contracts |
| [Node](Node/README.zh.md) | 节点容器内部契约 | `INodeHost`(internal)；`INodeFactory` / `INodeHandle` 位于 Contracts |
| [Scene](Scene/README.zh.md) | SND 场景编排内部契约 | `ISndSceneAccess` / `ISndSceneHost` / `IOwningSessionBindable`(internal)；只读访问契约位于 Contracts |

> 公共实体、会话、场景、状态机、SND companion 与叶级契约位于稳定契约包
> [Origo.Core.Contracts](../../Origo.Core.Contracts/Abstractions/README.zh.md)。

## 接口层级

```
IBlackboard  IConsole*  IFileSystem  ILogger  INode*  (Contracts)
IOrigoFrameDriver  IScheduler (Contracts / Kernel)

ISessionManager  ISessionRun → IStateMachineContainer

ISndEntity ─── ISndDataAccess + ISndNodeAccess + ISndStrategyAccess
                + ISndActiveStrategyAccess + ISndObserverStrategyAccess

IEntityLifecycle                (独立 internal 接口，框架内部，非 ISndEntity 子接口)

ISndContext ··· 伴生属性 › ISndBlackboardAccess + ISndDeferredActions
               + ISndTemplateAccess + ISndConsoleAccess + ISndStateMachineAccess
               + ISndSaveOperations + ISndLifecycleOperations
               + ISndFileAccess + ISndArchiveFileAccess
               + IStateMachineContext

ISndSceneHost（internal）─── ISndSceneAccess（internal）

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
[↑ 回到 Origo.Core](../README.zh.md)
