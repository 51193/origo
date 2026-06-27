# Snd (Abstractions)

> [↑ 回到 Abstractions](../README.md) · [↔ 实现: Snd](../../Snd/README.md)

## 概述

ISndContext 的角色接口拆分。11 个窄接口按职责分解，遵循接口隔离原则（ISP）。ISndContext 本身作为组合接口继承全部角色。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ISndBlackboardAccess.cs` | 系统级 + 流程级黑板访问（2 成员） |
| `ISndSessionAccess.cs` | 会话管理器 + 当前会话 + 前台判定（3 成员） |
| `ISndDeferredActions.cs` | 延迟动作队列：入队 + 冲刷 + 计数（3 成员） |
| `ISndTemplateAccess.cs` | 模板克隆（1 成员） |
| `ISndConsoleAccess.cs` | 控制台命令提交/处理/输出订阅（4 成员） |
| `ISndStateMachineAccess.cs` | 流程级状态机容器访问（1 成员）。返回 `IStateMachineContainer?`（Abstractions 层接口），而非具体 `StateMachineContainer` |
| `ISndSaveOperations.cs` | 存档列表/读/写 + 关卡切换 + continue 目标 + meta 贡献者注册（8 成员） |
| `ISndLifecycleOperations.cs` | Continue/Initial/MainMenu 生命周期入口（4 成员） |
| `ISndEntityOperations.cs` | 实体操作：标记销毁 + 批量清空（2 成员） |
| `ISndFileAccess.cs` | 文件访问：结构化读写 + 强类型读写 + 存在检查（5 成员）。所有文件内容读写统一通过 `IDataSourceIoGateway` 边界，策略无需自行处理原始文本解析 |
| `ISndArchiveFileAccess.cs` | 存档内文件访问：结构化读写 + 强类型读写 + 存在检查 + 删除（6 成员）。路径相对于存档活动目录的 extra/ 子目录，随存档生命周期 |

## ISndContext 组合

```
ISndContext : ISndBlackboardAccess + ISndSessionAccess + ISndDeferredActions
            + ISndTemplateAccess + ISndConsoleAccess + ISndStateMachineAccess
            + ISndSaveOperations + ISndLifecycleOperations + ISndEntityOperations
            + ISndFileAccess + ISndArchiveFileAccess
```

ISndContext 自身不声明任何成员——所有成员均来自继承的角色接口。

## 与 IStateMachineContext 的关系

`IStateMachineContext` 继承 `ISndBlackboardAccess` 和 `ISndDeferredActions` 两个共享角色接口，避免了两个接口中的成员重复定义：

```
IStateMachineContext : ISndBlackboardAccess + ISndDeferredActions
                     + SessionBlackboard + SceneAccess
```

## 设计决策

### 为什么拆分 ISndContext

将 30 成员的接口拆分为 11 个窄接口，每个消费者可按需依赖窄接口：

- 仅需黑板访问的代码可依赖 `ISndBlackboardAccess`
- 仅需延迟队列的代码可依赖 `ISndDeferredActions`
- 仅需存档操作的代码可依赖 `ISndSaveOperations`
- 仅需实体操作的代码可依赖 `ISndEntityOperations`
- 等等

策略钩子（`LifecycleStrategyBase` 的 8 个虚方法）保持 `ISndContext ctx` 全量参数——策略作为一等公民，应能访问框架全部能力。

### 为什么 ISndContext 仍作为组合接口存在

策略钩子需要全量能力（如 `Process` 中也允许 `RequestSaveGame`）。作为组合接口保持调用方兼容性，拆分仅在类型层级表达职责边界。

### 为什么 IStateMachineContext 也继承了角色接口

`IStateMachineContext` 的 `SystemBlackboard`、`ProgressBlackboard`、`EnqueueBusinessDeferred` 与 `ISndContext` 语义完全一致，故通过继承 `ISndBlackboardAccess` + `ISndDeferredActions` 复用，避免跨接口重复定义；`SessionBlackboard` + `SceneAccess` 为状态机特有成员。

### 为什么 RequestKill 独立为 ISndEntityOperations

`RequestKillEntity` 和 `RequestKillAll` 是实体运行时生命周期操作，与存档读写在职责上不应混在一起，因此独立为 `ISndEntityOperations` 角色接口：

- `ISndSaveOperations` 聚焦纯持久化操作（存档/读档/关卡切换/continue）
- `ISndEntityOperations` 聚焦实体运行时操作（标记销毁/批量清空）
- 消费者可按需只依赖需要的角色

### 为什么 GetProgressStateMachines() 返回 IStateMachineContainer

Abstractions 层接口的返回值不得引用 Runtime 层具体实现类型。`IStateMachineContainer` 定义在 `Origo.Core.Abstractions.StateMachine` 中，返回此抽象接口而非具体的 `StateMachineContainer`，确保 `ISndStateMachineAccess` 的消费者不传递性依赖到 Runtime 层内部实现（`StackStateMachine`、`SndStrategyPool` 等）。

### 为什么 ISndFileAccess 暴露 DataSourceNode 而非裸文件文本

所有文件操作通过三个基础接口完成——`IDataSourceIoGateway`（内容 I/O）、`IFileMetaAccess`（文件元数据）、`IPathResolver`（路径运算）。`ISndFileAccess` 暴露的方法分别委托到对应接口：

- `ReadFile` / `WriteFile` → `IDataSourceIoGateway.ReadTree` / `WriteTree` → 结构化 `DataSourceNode` 树
- `ReadObject<T>` / `WriteObject<T>` → 在 Gateway 基础上集成 `DataSourceConverterRegistry` → 强类型对象
- `FileExists` → `IFileMetaAccess.FileExists`

策略不应直接调用 `IFileSystem`（已完全内部化）或自行解析原始 JSON/Map 文本——后缀路由、编解码策略与 I/O 错误语义统一在 Gateway 一侧治理。路径拼接（`CombinePath`、`GetParentDirectory`）和目录检查（`DirectoryExists`）由框架内部的 `IPathResolver` 和 `IFileMetaAccess` 提供，不通过 `ISndFileAccess` 暴露给策略层。

---

[↑ 回到 Abstractions](../README.md)
