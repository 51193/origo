<!-- docsync-pair: Origo.Core/Abstractions/Snd/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Snd (Abstractions)

> [↑ 回到 Abstractions](../README.zh.md) · [↔ 实现: Snd](../../Snd/README.zh.md)

## 概述

ISndContext 的角色接口拆分。10 个窄接口按职责分解，遵循接口隔离原则（ISP）。ISndContext 不继承任何角色接口，所有能力通过类型化 companion 属性访问。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ISndBlackboardAccess.cs` | 系统级 + 流程级黑板访问（2 成员） |
| `ISndDeferredActions.cs` | 延迟动作队列：入队 + 冲刷 + 计数（3 成员） |
| `ISndTemplateAccess.cs` | 模板克隆（1 成员） |
| `ISndConsoleAccess.cs` | 控制台命令提交/处理/输出订阅（4 成员） |
| `ISndStateMachineAccess.cs` | 流程级状态机容器访问（1 成员）。返回 `IStateMachineContainer?`（Abstractions 层接口），而非具体 `StateMachineContainer` |
| `ISndSaveOperations.cs` | 存档列表/读/写 + 关卡切换 + continue 目标 + meta 贡献者注册（8 成员） |
| `ISndLifecycleOperations.cs` | Continue/Initial/MainMenu 生命周期入口（4 成员） |
| `ISndFileAccess.cs` | 文件访问：结构化读写 + 强类型读写 + 存在检查（5 成员）。所有文件内容读写统一通过 `IDataSourceIoGateway` 边界，策略无需自行处理原始文本解析 |
| `ISndArchiveFileAccess.cs` | 存档内文件访问：结构化读写 + 强类型读写 + 存在检查 + 删除（6 成员）。路径相对于存档活动目录的 extra/ 子目录，随存档生命周期 |

> `ISndSessionAccess` 和 `ISndEntityOperations` 现已不再使用。`CurrentSession`、`IsFrontSession`、`SessionManager`、`RequestKillAll`/`RequestKillEntity` 不在 `ISndContext` 上。`IsFrontSession` 在 `ISessionRun` 上（经 `entity.OwningSession.IsFrontSession` 访问）。`SessionManager` 可通过 `entity.OwningSession.SessionManager` 或 `OrigoRuntime.SessionManager`（public）访问。实体销毁经 `entity.OwningSession.RequestKillEntity(name)` 或 `ISessionRun.RequestKillEntity(name)` 执行。

## ISndContext Companion 属性

ISndContext 不继承任何角色接口，所有能力通过 10 个 companion 属性访问：

| Companion 属性 | 类型 | 职责 |
|---------------|------|------|
| `Blackboard` | `ISndBlackboardAccess` | 系统级 + 流程级黑板访问 |
| `Deferred` | `ISndDeferredActions` | 延迟动作队列：入队 + 冲刷 + 计数 |
| `Template` | `ISndTemplateAccess` | 模板克隆 |
| `ConsoleAccess` | `ISndConsoleAccess` | 控制台命令提交/处理/输出订阅 |
| `StateMachines` | `ISndStateMachineAccess` | 流程级状态机容器访问 |
| `Save` | `ISndSaveOperations` | 存档列表/读/写 + 关卡切换 + continue 目标 + meta 贡献者注册 |
| `Lifecycle` | `ISndLifecycleOperations` | Continue/Initial/MainMenu 生命周期入口 |
| `FileAccess` | `ISndFileAccess` | 静态资源文件访问（结构化读写 + 强类型读写 + 存在检查） |
| `ArchiveFileAccess` | `ISndArchiveFileAccess` | 存档内文件访问（结构化读写 + 强类型读写 + 存在检查 + 删除） |
| `StateMachineContext` | `IStateMachineContext` | 状态机上下文（黑板访问 + 延迟动作 + 会话/场景访问） |

策略钩子保持 `ISndContext ctx` 全量参数，通过 `ctx.Blackboard.SystemBlackboard`、`ctx.Save.RequestLoadGame(...)` 等二级访问使用各项能力。

## 与 IStateMachineContext 的关系

`IStateMachineContext` 继承 `ISndBlackboardAccess` 和 `ISndDeferredActions` 两个共享角色接口，避免了两个接口中的成员重复定义：

```
IStateMachineContext : ISndBlackboardAccess + ISndDeferredActions
                     + SessionBlackboard + SceneAccess
```

## 设计决策

### 为什么拆分 ISndContext

将接口拆分为 10 个窄接口，每个消费者可按需依赖窄接口：

- 仅需黑板访问的代码可依赖 `ISndBlackboardAccess`
- 仅需延迟队列的代码可依赖 `ISndDeferredActions`
- 仅需存档操作的代码可依赖 `ISndSaveOperations`
- 等等

策略钩子（`LifecycleStrategyBase` 的 8 个虚方法）保持 `ISndContext ctx` 全量参数——策略作为一等公民，应能访问框架全部能力。

### 为什么 ISndContext 使用 companion 属性而非接口继承

ISndContext 本身是统一业务门面，不继承任何角色接口。所有能力通过类型化 companion 属性暴露：

- **消除命名冲突**：多个角色接口可能包含同名成员（如不同接口的 `Clear()` 方法），接口继承在 C# 中必须通过显式接口实现消除歧义，companion 属性自然隔离命名空间（`ctx.Blackboard.Clear()` vs `ctx.Save.xxx()`）
- **更清晰的调用语义**：`ctx.Save.RequestLoadGame(...)` 明确表达"通过存档服务执行加载"，优于直接 `ctx.RequestLoadGame(...)`
- **组合优于继承**：companion 模式允许未来在不改变 ISndContext 签名的前提下添加/替换能力提供者

### 为什么 SessionManager 不在 ISndContext 上

`ISessionManager` 是跨会话操作的入口。策略通过 `entity.OwningSession.SessionManager` 访问，实体自身知道所属 session，无需通过全局上下文按 key 查找。将其留在 `ISndContext` 上鼓励了 `ctx.SessionManager` 的用法，这要求调用方知道目标 session 的 key——不如 `entity.OwningSession.SessionManager` 安全。

### 为什么移除 ISndSessionAccess 和 ISndEntityOperations

- `CurrentSession` 从公共接口移除：策略应使用 `entity.OwningSession` 访问所属会话，而非经全局 `ctx.CurrentSession` 反查。
- `IsFrontSession` 移除：便捷属性，可从 `SessionManager.ForegroundSession` 推导。
- `RequestKillAll`/`RequestKillEntity` 从 `ISndContext` 移除：实体销毁统一经 `entity.OwningSession.RequestKillEntity(name)` 或 `ISessionRun.RequestKillEntity(name)` 执行，不再通过全局上下文。

### 为什么 IStateMachineContext 也继承了角色接口

`IStateMachineContext` 的 `SystemBlackboard`、`ProgressBlackboard`、`EnqueueBusinessDeferred` 与 `ISndContext` 语义完全一致，故通过继承 `ISndBlackboardAccess` + `ISndDeferredActions` 复用，避免跨接口重复定义；`SessionBlackboard` + `SceneAccess` 为状态机特有成员。

### 为什么 GetProgressStateMachines() 返回 IStateMachineContainer

Abstractions 层接口的返回值不得引用 Runtime 层具体实现类型。`IStateMachineContainer` 定义在 `Origo.Core.Abstractions.StateMachine` 中，返回此抽象接口而非具体的 `StateMachineContainer`，确保 `ISndStateMachineAccess` 的消费者不传递性依赖到 Runtime 层内部实现（`StackStateMachine`、`SndStrategyPool` 等）。

### 为什么 ISndFileAccess 暴露 DataSourceNode 而非裸文件文本

所有文件操作通过三个基础接口完成——`IDataSourceIoGateway`（内容 I/O）、`IFileMetaAccess`（文件元数据）、`IPathResolver`（路径运算）。`ISndFileAccess` 暴露的方法分别委托到对应接口：

- `ReadFile` / `WriteFile` → `IDataSourceIoGateway.ReadTree` / `WriteTree` → 结构化 `DataSourceNode` 树
- `ReadObject<T>` / `WriteObject<T>` → 在 Gateway 基础上集成 `DataSourceConverterRegistry` → 强类型对象
- `FileExists` → `IFileMetaAccess.FileExists`

策略不应直接调用 `IFileSystem`（已完全内部化）或自行解析原始 JSON/Map 文本——后缀路由、编解码策略与 I/O 错误语义统一在 Gateway 一侧治理。路径拼接（`CombinePath`、`GetParentDirectory`）和目录检查（`DirectoryExists`）由框架内部的 `IPathResolver` 和 `IFileMetaAccess` 提供，不通过 `ISndFileAccess` 暴露给策略层。

---

[↑ 回到 Abstractions](../README.zh.md)
