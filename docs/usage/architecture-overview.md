# 架构总览

> [↑ 回到 usage](README.md)

## 设计原则

Origo 框架遵循以下核心设计约束：

- **平台无关**：Core 零引擎依赖，所有 I/O 通过 `IDataSourceIoGateway` + `IFileMetaAccess` + `IPathResolver`（`IFileSystem` 内部化）
- **适配层隔离**：适配层仅提供能力封装和桥接，不得触发策略钩子、管理策略生命周期
- **接口隔离（ISP）**：`ISndContext` 拆分为 11 个角色接口；`ISessionRun` 返回抽象 `IStateMachineContainer`
- **依赖方向单向**：Abstractions → Core 实现 → Adapter，反向严格禁止
- **public 白名单**：每个 public 接口必须有明确的跨程序集消费者
- **策略一等公民**：策略可访问 `ISndContext` 全部 30+ 成员，不限制框架能力
- **单线程帧模型**：一帧 = 一个逻辑原子边界

## 项目设计

Origo 是一个**轻量、平台无关的 C# 游戏框架**，采用 SND（Strategy + Node + Data）实体模型。项目由两层组成：

- **Origo.Core**：平台无关核心，所有游戏逻辑、持久化、实体模型在此层
- **Origo.GodotAdapter**：Godot 4 适配层，将 Core 的抽象接口与 Godot 引擎 API 桥接

## 四层运行时

```
SystemRun (持有 SystemRuntime, 内含 OrigoRuntime)
  ↓
ProgressRun (持有 ProgressRuntime)
  ↓
SessionManager (持有 SessionManagerRuntime)
  ↓
SessionRun (foreground + background)
```

### 能力传递规则

- 上层向下层注入依赖，下层不得反向引用上层
- 每层通过结构化参数对象构造，不使用零散参数
- 每层维护本层聚合容器（`*Runtime`），集中持有引用

### 各层职责

| 层级 | 容器 | 持有资源 |
|------|------|---------|
| System | `SystemRuntime` | Logger, PathResolver, SaveRootPath, OrigoRuntime, SaveStorageService, SavePathPolicy, MetaAccess |
| Progress | `ProgressRuntime` | Logger, SaveStorageService, SndWorld, SndRuntime, SceneHost, StateMachineContext, SndContext, SavePathPolicy |
| Session | `SessionManagerRuntime` | Logger, SaveStorageService, SndWorld, SndRuntime, SceneHost, StateMachineContext, SndContext, ProgressBlackboard |
| Instance | `SessionRun` | SessionBlackboard, ISndSceneHost, StateMachineContainer（经 RunStateScope） |

## SND 实体模型

```
Strategy + Node + Data
```

| 组件 | 说明 | 位置 |
|------|------|------|
| **Strategy** | 行为逻辑，无实例可变状态 | 策略池（全局共享） |
| **Node** | 表现层映射，由引擎实现驱动 | 适配层（GodotNodeHandle） |
| **Data** | 实体可变状态的唯一权威来源 | SndDataManager（内联存储 TypedData struct） |

### 策略池约束

- 策略通过 `[StrategyIndex("xxx")]` 显式注册
- 索引缺失、空值、类型不匹配必须抛错
- 策略注册时通过反射校验无状态性（拒绝实例字段/可写属性）
- 策略按优先级排序，同优先级按插入顺序

## 会话模型

- 前台/后台均通过 `ISessionRun` 表达，无两套并行类型
- 前台唯一键为 `__foreground__`
- 差异仅在 `IsFrontSession` 与注入的 `ISndSceneHost`
- 业务逻辑不得按会话实现类型分支

## 持久化

### 文件布局

```
{saveRoot}/
├── current/                          # 活动存档（临时）
│   ├── .write_in_progress
│   ├── progress.json
│   ├── progress_state_machines.json
│   ├── meta.map
│   ├── extra/                     # 策略存档内文件（随存档生命周期）
│   └── level_{id}/
│       ├── snd_scene.json
│       ├── session.json
│       └── session_state_machines.json
└── save_{id}/                        # 快照存档（持久）
    ├── extra/                     # 策略存档内文件（随存档生命周期）
    └── ... (同上)
```

### 两阶段写入

1. 写入 `current/`（先建标记 → 写全部文件 → 删标记）
2. 快照 `current/` → `save_{id}/`（atomic rename via temp directory）

### 严格读取

- 关卡三件套全部缺失 → 视为尚无存档
- 部分存在 → 视为损坏，抛异常
- `current/` 下有 `.write_in_progress` → 抛异常（中断写入）

## 并发模型

Origo 采用单线程帧循环模型：
- 生命周期动作通过延迟队列（`ActionScheduler` / `ConcurrentActionQueue`）顺序执行
- 单帧视为逻辑原子边界
- 跨帧语义落入可恢复状态（黑板、状态机、实体 Data）

## I/O 边界

Core 层所有文件操作通过三个接口完成：
- `IDataSourceIoGateway`：内容读写，仅 2 个方法——`ReadTree(filePath)` 和 `WriteTree(filePath, node, overwrite)`，所有文件内容 I/O 均通过 codec 路由，零旁路
- `IFileMetaAccess`：文件元数据操作（FileExists、目录管理、枚举、删除、复制）
- `IPathResolver`：平台路径运算（CombinePath、GetParentDirectory）

`IFileSystem` 是实现细节，上述三个接口是其公共门面。业务代码不应直接依赖 `IFileSystem`。

```
业务模块 → DataSourceNode → IDataSourceIoGateway / IFileMetaAccess / IPathResolver → IFileSystem（内部） → 文件系统
```

后缀路由、编解码策略与 I/O 错误语义集中在 Gateway 一侧统一治理。`.sha` 和 `.write_in_progress` 等原始文本文件同样经由 `RawStringDataSourceCodec` 走 codec 路由，不存在直读直写旁路。Gateway 采用 fail-fast 策略：codec 解码失败（如 `.map` 文件格式错误）时，Gateway 将异常包装为包含文件路径信息的 `InvalidOperationException` 立即抛出，不吞没错误。

`IDataSourceIoGateway` 是框架的硬性 I/O 内容边界：**系统内的任何模块（包括策略）都应通过此边界访问文件内容**。文件元数据操作（存在性检查、目录管理等）通过 `IFileMetaAccess`，路径运算通过 `IPathResolver`。策略通过 `ISndFileAccess` 和 `ISndArchiveFileAccess` 接口暴露的文件操作均委托到上述三个接口，策略无需自行处理原始文本解析或平台路径差异。

## 适配层与 Core 层分离原则

适配层和 Core 层的分离是 Origo 框架的核心架构约束。适配层职责严格限定为两项，其余全部在 Core 层实现。

### 接口抽象设计

Core 层遵循接口隔离原则（ISP），`ISndContext` 拆分为 11 个角色接口：

| 角色接口 | 职责 |
|---------|------|
| `ISndBlackboardAccess` | 系统级 + 流程级黑板访问 |
| `ISndSessionAccess` | 会话管理器 + 当前会话访问 |
| `ISndDeferredActions` | 延迟动作队列 |
| `ISndTemplateAccess` | 模板克隆 |
| `ISndConsoleAccess` | 控制台命令提交/处理 |
| `ISndStateMachineAccess` | 流程级状态机容器访问 |
| `ISndSaveOperations` | 存档/读档/关卡切换/continue |
| `ISndLifecycleOperations` | 生命周期入口（Continue/Initial/MainMenu） |
| `ISndEntityOperations` | 实体操作（标记销毁/批量清空） |
| `ISndFileAccess` | 静态资源文件访问（经 DataSource 边界 + 内置解析） |
| `ISndArchiveFileAccess` | 存档内文件访问（路径相对于存档活动目录的 `extra/` 子目录，随存档生命周期） |

此外，`ISessionManager` 和 `ISessionRun` 位于 Abstractions 层，`ISessionRun.GetSessionStateMachines()` 返回 `IStateMachineContainer`（Abstractions 层接口）而非具体 `StateMachineContainer`，确保接口层完全解耦 Runtime 实现。

### 适配层职责（仅两项）

| 职责 | 说明 | 示例 |
|------|------|------|
| **能力提供** | 将引擎原生能力封装为 Core 层抽象接口的实现 | `GodotFileSystem : IFileSystem`、`GodotLogger : ILogger`、`GodotNodeHandle : INodeHandle`、`GodotPackedSceneNodeFactory : INodeFactory` |
| **桥接** | 在启动期将适配层实现注入 Core 层，完成装配 | `OrigoAutoHost`（创建 Runtime + SndManager）、`GodotSndBootstrap`（依赖绑定）、`OrigoDefaultEntry`（默认入口编排） |

### 适配层禁止事项

| 禁止 | 原因 | 反例 |
|------|------|------|
| **触发策略生命周期钩子** | 策略是 Core 层概念，钩子触发时机和顺序必须由 Core 统一编排 | `GodotSndManager` 不得调用 `FireAfterSpawnHooks()`、`FireBeforeDeadHooks()` 等 |
| **管理策略释放/引用计数** | 策略池、引用计数、优先级排序在 Core 的 `SndStrategyPool` 和 `SndStrategyManager` 中管理 | `GodotSndManager` 不得调用 `ReleaseStrategiesOnly()` |
| **直接调用 Core 管线方法** | 帧边界操作（实体处理→业务队列→杀实体→系统队列→控制台）的时机和顺序由 Core 控制。适配层只应调用 `IOrigoFrameDriver.DriveFrame(delta)` 移交帧控制权 | 适配层不得直接调用 `FlushDeferredActionsForCurrentFrame()` 或 `ProcessPending()` |
| **直接驱动 Core 启动流程** | 策略发现、别名/模板加载、入口存档加载是 Core 内部编排，统一在 `SndContext.Bootstrap()` 中执行。适配层仅通过 `SndContextParameters` 传入配置 | 适配层不得直接调用 `OrigoAutoInitializer.DiscoverAndRegisterStrategies()`、`LoadSceneAliases()`、`LoadTemplates()`、`RequestLoadMainMenuEntrySave()` |
| **持有 Core 编排状态** | 实体生命周期管理（如 pending kill、拆卸流程）的状态机由 Core 层维护 | 适配层不应有 `QuitFromManager`、`DeadFromManager` 等方法 |
| **加载引擎无关的业务配置** | 模板解析、别名映射、策略索引解析全部在 Core 中完成 | 适配层不应读取和解析 `snd_templates.map` 等业务配置 |

### Core 层职责

| 职责 | 说明 |
|------|------|
| **策略系统** | 策略基类、策略池、策略管理器、生命周期钩子定义与触发 |
| **实体生命周期编排** | `SndRuntime.Spawn`/`SpawnMany`/`KillPendingEntities`/`ClearAll` 统一编排所有钩子 |
| **场景宿主抽象** | `ISndSceneHost` 仅定义容器操作（创建/查找/移除），不含钩子语义 |
| **延迟动作管线** | `ActionScheduler` 业务队列 + 系统队列，`IOrigoFrameDriver.DriveFrame` 统一冲刷 |
| **启动编排** | `SndContext.Bootstrap()` 统一执行策略发现→别名/模板加载→入口存档加载 |

### 帧循环中的职责划分

```
Godot._Process
  └── OrigoAutoHost._Process              ← 适配层（唯一帧入口）
        └── IOrigoFrameDriver.DriveFrame(delta)  ← Core: 统一帧边界
              ├── Snd.ProcessAll(delta)    ← Core: 实体帧处理
              ├── FlushEndOfFrameDeferred() ← Core: 延迟动作 + KillPendingEntities
              └── Console.ProcessPending() ← Core: 控制台
```

帧循环的入口在适配层（Godot 的 `_Process` 回调），但进入后立即将控制权通过 `IOrigoFrameDriver.DriveFrame(delta)` 移交给 Core 层。适配层不参与帧内的任何逻辑决策，不感知 Core 内部的实体处理、延迟队列、控制台的三阶段顺序。

## 项目结构

```
Origo.Core/           # 平台无关核心 (~90 个 .cs 文件)
├── Abstractions/     # 公共接口（Blackboard/Entity/StateMachine/...）
├── Blackboard/       # 黑板实现
├── DataSource/       # JSON/Map 编解码 + 类型转换
├── Random/           # 随机数 + 噪声
├── Runtime/          # 运行时四层生命周期 + 控制台
├── Save/             # 持久化存储
├── Scheduling/       # 延迟队列
├── Serialization/    # 类型 ↔ 字符串映射
├── Snd/              # SND 实体系统（策略 + 数据 + 节点）
├── StateMachine/     # 字符串栈状态机
└── Testing/          # 策略测试框架

Origo.SourceGeneration/  # Roslyn 源码生成器 (~1 个 .cs 文件)
└── TypedDataGenerator.cs  # Home/Adapter 双模式代码生成

Origo.GodotAdapter/   # Godot 4 适配层 (~18 个 .cs 文件)
├── Bootstrap/        # 启动编排
├── Console/          # Godot 命令
├── FileSystem/       # Godot 文件系统
├── Logging/          # Godot 日志
├── Serialization/    # Godot 类型序列化
└── Snd/              # Godot 实体 + 管理器 + TypedDataInitializer

Origo.ConsoleBridge/  # TCP 远程控制台（~2 个 .cs 文件）
```

## 测试策略

- Core 侧：引擎依赖泄露、生命周期边界、持久化契约、策略池约束
- Adapter 侧：宿主装配、路径策略、序列化注册在 Godot 环境下可运行
- `Origo.Core` 总行覆盖率 ≥ 90%（Coverlet）
- 测试目录结构与生产代码镜像

---
[↑ 回到 usage](README.md)
