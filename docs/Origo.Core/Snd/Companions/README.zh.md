<!-- docsync-pair: Origo.Core/Snd/Companions/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Companions

> [↑ 回到 Snd](../README.zh.md)

## 概述

`SndContext` 的 companion 对象层。每个 companion 是 `internal sealed class`，实现 `ISndContext` 暴露的一个角色接口。多数 companion 持有对 `SndContext` 的反向引用以访问框架内部状态；`SndContextFileAccess` / `SndContextArchiveFileAccess` 例外——它们直接注入 I/O 依赖（`IDataSourceIoGateway`、`IFileMetaAccess`、`IPathResolver` 等），不持有 `SndContext` 引用。`SndContextTemplateAccess` 除模板克隆外，还经 `SndContext` 的 I/O 依赖加载模板/别名 map，并解析实体列表 JSON 文件。策略通过 `ctx.Blackboard`、`ctx.Save` 等属性获取对应的 companion 实例，而非直接 cast `ISndContext` 到角色接口。

## 包含文件

| 文件 | 实现接口 | 对应属性 |
|------|---------|---------|
| `SndContextBlackboardAccess.cs` | `ISndBlackboardAccess` | `ISndContext.Blackboard` |
| `SndContextDeferredActions.cs` | `ISndDeferredActions` | `ISndContext.Deferred` |
| `SndContextTemplateAccess.cs` | `ISndTemplateAccess` | `ISndContext.Template` |
| `SndContextConsoleAccess.cs` | `ISndConsoleAccess` | `ISndContext.ConsoleAccess` |
| `SndContextStateMachineAccess.cs` | `ISndStateMachineAccess` | `ISndContext.StateMachines` |
| `SndContextSaveOperations.cs` | `ISndSaveOperations` | `ISndContext.Save` |
| `SndContextLifecycleOperations.cs` | `ISndLifecycleOperations` | `ISndContext.Lifecycle` |
| `SndContextStateMachineContext.cs` | `IStateMachineContext` | `ISndContext.StateMachineContext` |

另有 `SndContextFileAccess.cs` 和 `SndContextArchiveFileAccess.cs` 位于 `Snd/` 层，分别实现 `ISndFileAccess`（`ISndContext.FileAccess`）和 `ISndArchiveFileAccess`（`ISndContext.ArchiveFileAccess`）。

## 设计决策

### 为什么用 companion 对象而非让 SndContext 直接实现角色接口

`SndContext` 聚合了 10 种以上的角色能力。如果让 `SndContext` 直接实现所有角色接口，`SndContext` 本身将变成一个巨大的接口拼盘，且 `ISndContext` 与各角色接口之间的继承关系模糊不清——使用者无法区分"这是 SndContext 的职责"和"这是 SndContext 恰好实现的某个角色接口"。

将每个角色提取为独立的 companion 对象：
- `ISndContext` 只暴露 companion 属性，自身不继承任何角色接口
- 多数 companion 持有 `SndContext` 的反向引用，访问框架内部状态；两个文件访问 companion 直接注入 I/O 依赖（无 `SndContext` 引用）
- 使用者通过 `ctx.Blackboard.SystemBlackboard` 而非 `((ISndBlackboardAccess)ctx).SystemBlackboard` 访问能力，语义明确

### 为什么 companion 是 internal 而非 public

Companion 对象是框架实现细节。策略通过 `ISndContext` 的 companion 属性访问能力，不需要知道底层是哪个类实现了接口。将 companion 类设为 `internal` 阻止了策略代码对具体实现类型的硬依赖，同时保留了未来改变实现策略的自由（例如合并 companion 或切换委托目标）。

### 为什么 companion 反向引用 SndContext 而非通过接口注入依赖

Companion 需要访问 `SndContext` 的内部状态（如 `_systemRun`、`_progressRun`、`_saveMetaContributors` 等），这些状态是框架私有的。如果通过接口注入，需要将这些内部状态暴露为 `public` 或 `internal` 接口成员，增加了 API 表面积。直接引用 `SndContext` 让 companion 可以访问任意内部成员，而无需将它们加入公开协议。

---

[↑ 回到 Snd](../README.zh.md)
