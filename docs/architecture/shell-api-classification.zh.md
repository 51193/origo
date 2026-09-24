<!-- docsync-pair: architecture/shell-api-classification -->
<!-- docsync-revision: 14 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 0.1.0 Shell API 分类

> [↑ 回到 architecture](README.zh.md)

本文是 issue #37 的 0.1.0 稳定消费者表面基线：初始清单在源提交 `7405cf5` 上完整记录 `Origo.Core`、`Origo.GodotAdapter`、`Origo.ConsoleBridge` 的全部导出类型。后续 #38 拆分提交在原位更新 Assembly 与目标包列，同时保持已分类类型集合完整；#41 再对最终 shell 成员面重新建基线。

拓扑与兼容策略仍以 [shell-kernel-boundary](shell-kernel-boundary.zh.md) 为准；本文只把该决策应用到当前导出面。任何新增导出类型都会在三个测试项目各自的架构守卫中失败，直到它同时进入中英文分类表并补齐分类、目标包、能力组和理由。迁移类型若未在同一变更中更新程序集列与对应程序集守卫，也会失败。

## 复现方式

本清单是类型集合比对，而不是手工维护的源码扫描：守卫先构建 Release 程序集，再用 `Assembly.GetExportedTypes()` 与下表比对。执行：

```bash
dotnet build Origo.sln --configuration Release
dotnet test Origo.Core.Tests --configuration Release -p:CollectCoverage=false --filter FullyQualifiedName~ShellApiClassification_CoversEveryCoreExport
dotnet test Origo.GodotAdapter.Tests --configuration Release -p:CollectCoverage=false --filter FullyQualifiedName~ShellApiClassification_CoversEveryAdapterExport
dotnet test Origo.ConsoleBridge.Tests --configuration Release -p:CollectCoverage=false --filter FullyQualifiedName~ShellApiClassification_CoversEveryConsoleBridgeExport
```

这些守卫也在 `scripts/test.sh` 中执行，因此完整开发循环会重新校验本表。`Origo.Core.Tests` 同时校验 `Origo.Core`、`Origo.Core.Contracts` 与 `Origo.Core.Kernel` 的导出集，并校验中英文表携带完全一致的类型元数据。

## 生成的公共成员

- `TypedData` 由 Source Generator 生成的 `TryGetXxx` 访问器与 operator 属于某个 Shell contract 类型的成员；其签名、nullable 标注、Kind 分配和诊断由 #41 的 Roslyn baseline 负责。
- Godot 生成的 `MethodName`/`PropertyName`/`SignalName` 嵌套类型是独立导出类型，因此各自有独立行。
- 其它 shell 类型上的生成成员随其宿主类型分类。模块初始化器等生成 internal 类型不属于导出面，也不承诺消费者兼容。

## 分类取值

| 取值 | 含义 |
|------|------|
| **Shell contract** | 消费者支持且保持版本兼容的 API，编译进 shell 包。 |
| **Tooling extension** | 文档化的适配器/工具扩展点；其编译进 shell 的签名属于稳定面，但不是游戏主路径。 |
| **Kernel implementation** | 0.1.x 消费者编译面不可见的实现细节。 |
| **Test-only** | 仅为测试或 friend 访问而导出；没有消费者路径，也不承诺兼容。 |

当前清单没有 test-only 导出类型：测试专用能力已经位于 `Origo.TestSupport` 或 `InternalsVisibleTo` 之后。因此下表只使用其余三类。

## 包与运行期规则

- `Origo.Core.Contracts` 承载稳定接口、纯数据、元数据、策略基类、data source 契约和日志抽象。
- `Origo.Core` 承载只依赖 Contracts 的具体消费者 shell 辅助与工具实现。
- `Origo.Core.Kernel` 承载运行时构造、SND 内部、持久化、存储、codec 和控制台路由。
- `Origo.GodotAdapter` 保持单一 shell 包；公开 Godot `Node` 入口保留真实 shell 类型，bridge/manager 实现类型为 internal，不进入消费者编译面。
- `Origo.ConsoleBridge` 保持 shell-only，只依赖 Core 的控制台/日志契约。
- Godot 生成的嵌套 signal 类型是公开导出，因此逐项列出。成员级生成访问器（`TryGetXxx`、operator、nullable、诊断）由 #41 的 Roslyn baseline 负责；本表分类其宿主类型。

## 当前拆分检查点

当前包路径由 Assembly 列表示：`Origo.Core.Contracts` 承载稳定接口、纯数据、TypedData/metadata 模型、策略基类与 attribute、data-source 契约、控制台/日志抽象，以及 shell 与 kernel 共用的纯工具（黑板、日志实现、类型映射、converter registry、路径工具、网格坐标）。`Origo.Core.Kernel` 承载 runtime 构造、SND 内部、持久化/存储、data-source codec 与 factory、console 路由、FastNoiseLite、调度以及 internal `HostKernelPort`。`Origo.Core` 是 shell 包：`OrigoHost`、网格/随机工具与 SND 扩展辅助；公共签名只使用 Contracts 与 shell 类型。

## 兼容 API 的移除条件

每个 shell/tooling 行都带有 owner 和下列 issue #37 移除条件之一，作为下一个 `0.y.0` 版本的检查输入。

| 代号 | 具体的 0.2.0 移除条件 |
|------|----------------------|
| `R-HOST` | 仅当 `IOrigoRuntime`/`ISndWorldAccess` 与 shell host facade 提供等价的启动和帧访问路径、适配器与 demo 完成迁移，并且宿主/帧行为测试通过后移除。 |
| `R-SND` | 仅当替代的实体、角色、策略或状态机类型交付，生命周期、观察者和 fail-fast 测试通过，并存在迁移说明后移除。 |
| `R-DATA` | 仅当替代元数据或 data source 契约成文、生成访问器可编译、存档往返测试通过后移除。 |
| `R-SAVE` | 仅当替代存档/文件访问 API 交付，并且存档格式 golden、损坏与恢复测试通过后移除。 |
| `R-CONSOLE` | 仅当替代控制台/日志扩展契约覆盖 Core、GodotAdapter 和 ConsoleBridge，并且 bridge-only 隔离守卫通过后移除。 |
| `R-UTIL` | 仅当工具迁移到替代模块或包、消费者 usage 文档更新，并由消费者编译或测试覆盖后移除。 |
| `R-GODOT-ENTRY` | 仅当替代 Godot 入口仍是真实 `Node` 类型、脚本发现与 headless 启动测试通过、demo 完成迁移后移除。 |
| `R-GODOT-SHELL` | 仅当替代适配器 shell 类型在适配器与集成测试下保持文件、日志、节点扩展与场景工厂行为后移除。 |
| `R-GODOT-TOOLING` | 仅当替代适配器扩展点成文，并且 Core/ConsoleBridge 隔离守卫通过后移除。 |
| `R-BRIDGE` | 仅当替代 bridge 协议/选项成文，并且 loopback-only、单连接、重连和溢出测试通过后移除。 |

`Kernel implementation` 行明确写 `n/a`：kernel 包在 0.1.x 继续遵循早期开发无兼容负担规则。

## 完整导出类型清单

<!-- shell-api-classification:start -->
| 类型 | 程序集 | 分类 | 目标包 | 能力组 | 理由 | Owner / 0.2.0 移除条件 |
|------|------|------|------|------|------|------|
| <code>Origo.Core.Abstractions.Blackboard.IBlackboard</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | blackboard | 黑板/状态访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.Console.IConsoleInputSource</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | console | 控制台 I/O 抽象能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-CONSOLE |
| <code>Origo.Core.Abstractions.Console.IConsoleOutputChannel</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | console | 控制台 I/O 抽象能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-CONSOLE |
| <code>Origo.Core.Abstractions.Entity.ISndActiveStrategyAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | snd-entity | SND 实体/策略消费者 API能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.Entity.ISndDataAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | snd-entity | SND 实体/策略消费者 API能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.Entity.ISndEntity</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | snd-entity | SND 实体/策略消费者 API能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.Entity.ISndNodeAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | snd-entity | SND 实体/策略消费者 API能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.Entity.ISndObserverStrategyAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | snd-entity | SND 实体/策略消费者 API能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.Entity.ISndStrategyAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | snd-entity | SND 实体/策略消费者 API能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.FileSystem.IFileSystem</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | data-io | 数据源 I/O 边界能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-DATA |
| <code>Origo.Core.Abstractions.FileSystem.IPathResolver</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | data-io | 数据源 I/O 边界能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-DATA |
| <code>Origo.Core.Abstractions.Lifecycle.ISessionManager</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-session | 上下文/会话访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Lifecycle.ISessionRun</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-session | 上下文/会话访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Logging.ILogger</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | logging | 日志抽象能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-CONSOLE |
| <code>Origo.Core.Abstractions.Logging.ILogger`1</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | logging | 日志抽象能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-CONSOLE |
| <code>Origo.Core.Abstractions.Logging.LogLevel</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | logging | 日志抽象的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-CONSOLE |
| <code>Origo.Core.Abstractions.Node.INodeFactory</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | engine-node | 引擎节点抽象能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Node.INodeHandle</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | engine-node | 引擎节点抽象能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Runtime.IOrigoFrameDriver</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | host-runtime | 宿主/运行时能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Scene.ISndSceneReadAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-session | 上下文/会话访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Snd.ISndArchiveFileAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | file-access | 文件访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SAVE |
| <code>Origo.Core.Abstractions.Snd.ISndFileAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | file-access | 文件访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SAVE |
| <code>Origo.Core.Abstractions.Snd.ISndBlackboardAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-companion | SND 上下文 companion 访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Snd.ISndConsoleAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-companion | SND 上下文 companion 访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Snd.ISndDeferredActions</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-companion | SND 上下文 companion 访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Snd.ISndLifecycleOperations</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-companion | SND 上下文 companion 访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Snd.ISndStateMachineAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-companion | SND 上下文 companion 访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Snd.ISndTemplateAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | context-companion | SND 上下文 companion 访问能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Snd.ISndSaveOperations</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | save | 存档操作能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SAVE |
| <code>Origo.Core.Abstractions.StateMachine.IStateMachine</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | state-machine | 状态机能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.StateMachine.IStateMachineContainer</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | state-machine | 状态机能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Abstractions.StateMachine.IStateMachineContext</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | state-machine | 状态机能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Addons.FastNoiseLite.FastNoiseLite</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | noise-kernel | 噪声实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Addons.FastNoiseLite.FastNoiseLite+CellularDistanceFunction</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | noise-kernel | 噪声实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Addons.FastNoiseLite.FastNoiseLite+CellularReturnType</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | noise-kernel | 噪声实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Addons.FastNoiseLite.FastNoiseLite+DomainWarpType</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | noise-kernel | 噪声实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Addons.FastNoiseLite.FastNoiseLite+FractalType</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | noise-kernel | 噪声实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Addons.FastNoiseLite.FastNoiseLite+NoiseType</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | noise-kernel | 噪声实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Addons.FastNoiseLite.FastNoiseLite+RotationType3D</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | noise-kernel | 噪声实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Blackboard.Blackboard</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | blackboard | shell 辅助工具与 kernel 编排共用的 Contracts 黑板/状态实现，因此位于 Contracts 编译面。 | core-shell; R-SND |
| <code>Origo.Core.DataSource.DataSourceConverter`1</code> | Origo.Core.Contracts | Tooling extension | Origo.Core.Contracts | data-tooling | 数据源工具扩展的文档化工具扩展基类；派生方式与校验语义属于稳定契约。 | core-tooling; R-DATA |
| <code>Origo.Core.DataSource.DataSourceConverterBase</code> | Origo.Core.Contracts | Tooling extension | Origo.Core.Contracts | data-tooling | 数据源工具扩展的文档化工具扩展基类；派生方式与校验语义属于稳定契约。 | core-tooling; R-DATA |
| <code>Origo.Core.DataSource.DataSourceConverterRegistry</code> | Origo.Core.Contracts | Tooling extension | Origo.Core.Contracts | data-tooling | 数据源注册与类型名映射共用的 Contracts 工具类型，shell 工具与 kernel 服务都通过文档化消费者扩展路径使用。 | core-tooling; R-DATA |
| <code>Origo.Core.DataSource.DataSourceFactory</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | data-kernel | 数据源构造的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.DataSource.DataSourceNode</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | data-model | 数据源模型的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.DataSource.DataSourceNodeKind</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | data-model | 数据源模型的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.DataSource.IDataSourceIoGateway</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | data-io | 数据源 I/O 边界能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-DATA |
| <code>Origo.Core.DataSource.IFileMetaAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | data-io | 数据源 I/O 边界能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-DATA |
| <code>Origo.Core.Grid.Astar</code> | Origo.Core | Shell contract | Origo.Core | grid-utility | 网格工具的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | core-shell; R-UTIL |
| <code>Origo.Core.Grid.GridCoordinateSystem</code> | Origo.Core | Shell contract | Origo.Core | grid-utility | 网格工具的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | core-shell; R-UTIL |
| <code>Origo.Core.Grid.GridParser</code> | Origo.Core | Shell contract | Origo.Core | grid-utility | 网格工具的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | core-shell; R-UTIL |
| <code>Origo.Core.Grid.GridPos</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | grid-utility | 网格工具的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-UTIL |
| <code>Origo.Core.Logging.Logger`1</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | logging-shell | shell 与 kernel 服务共用的 Contracts 日志实现，保持在消费者编译面且无需 kernel 编译资产。 | core-shell; R-CONSOLE |
| <code>Origo.Core.Logging.LogMessageBuilder</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | logging-shell | shell 与 kernel 服务共用的 Contracts 日志实现，保持在消费者编译面且无需 kernel 编译资产。 | core-shell; R-CONSOLE |
| <code>Origo.Core.Logging.NullLogger</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | logging-shell | shell 与 kernel 服务共用的 Contracts 日志实现，保持在消费者编译面且无需 kernel 编译资产。 | core-shell; R-CONSOLE |
| <code>Origo.Core.OrigoMeta</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | host-runtime | 宿主/运行时的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-HOST |
| <code>Origo.Core.Planning.PlanExecutionStrategyBase</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定扩展基类；消费者派生实现，框架必须跨 kernel 保持钩子行为兼容。 | core-shell; R-SND |
| <code>Origo.Core.Random.NoiseMapGenerator</code> | Origo.Core | Shell contract | Origo.Core | random-utility | 随机数工具的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | core-shell; R-UTIL |
| <code>Origo.Core.Random.PersistentRandom</code> | Origo.Core | Shell contract | Origo.Core | random-utility | 随机数工具的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | core-shell; R-UTIL |
| <code>Origo.Core.Random.RandomNumberGenerator</code> | Origo.Core | Shell contract | Origo.Core | random-utility | 随机数工具的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | core-shell; R-UTIL |
| <code>Origo.Core.Runtime.Console.CommandInvocation</code> | Origo.Core.Contracts | Tooling extension | Origo.Core.Contracts | console-tooling | 控制台工具扩展的文档化工具扩展基类；派生方式与校验语义属于稳定契约。 | core-tooling; R-CONSOLE |
| <code>Origo.Core.Runtime.Console.ConsoleCommandHandlerBase</code> | Origo.Core.Contracts | Tooling extension | Origo.Core.Contracts | console-tooling | 控制台工具扩展的文档化工具扩展基类；派生方式与校验语义属于稳定契约。 | core-tooling; R-CONSOLE |
| <code>Origo.Core.Runtime.Console.IConsoleCommandHandler</code> | Origo.Core.Contracts | Tooling extension | Origo.Core.Contracts | console-tooling | 控制台工具扩展的文档化工具扩展契约；适配器与消费者工具实现它时无需接触 kernel 内部。 | core-tooling; R-CONSOLE |
| <code>Origo.Core.Runtime.Console.ConsoleInputBuffer</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | console-kernel | 控制台路由实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Runtime.Console.ConsoleOutputChannel</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | console-kernel | 控制台路由实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Runtime.Console.OrigoConsole</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | console-kernel | 控制台路由实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Runtime.OrigoRuntime</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | runtime-kernel | 运行时构造的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Save.LevelPayload</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | save-kernel | 存档/存储实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Save.SaveGamePayload</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | save-kernel | 存档/存储实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Save.PersistentBlackboard</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | save-kernel | 存档/存储实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Save.Storage.ISavePathPolicy</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | save-kernel | 存档/存储实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Save.Storage.ISaveStorageService</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | save-kernel | 存档/存储实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Save.Meta.ISaveMetaContributor</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | save | 存档操作能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SAVE |
| <code>Origo.Core.Save.Meta.SaveMetaBuildContext</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | save | 存档操作的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-SAVE |
| <code>Origo.Core.Save.Meta.SaveMetaDataEntry</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | save | 存档操作的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-SAVE |
| <code>Origo.Core.Serialization.TypeStringMapping</code> | Origo.Core.Contracts | Tooling extension | Origo.Core.Contracts | data-tooling | 数据源注册与类型名映射共用的 Contracts 工具类型，shell 工具与 kernel 服务都通过文档化消费者扩展路径使用。 | core-tooling; R-DATA |
| <code>Origo.Core.Snd.ActiveStrategyExtensions</code> | Origo.Core | Shell contract | Origo.Core | strategy | 策略扩展模型的消费者扩展 API，编译进 shell，并只通过文档化的编排路径生效。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Archetype.SndArchetypeLoader</code> | Origo.Core | Shell contract | Origo.Core | snd-utility | SND 工具的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | core-shell; R-UTIL |
| <code>Origo.Core.Snd.Entity.SndEntity</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | snd-kernel | SND 实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Snd.EntityExtensions</code> | Origo.Core | Shell contract | Origo.Core | snd-entity | SND 实体/策略消费者 API的消费者扩展 API，编译进 shell，并只通过文档化的编排路径生效。 | core-shell; R-SND |
| <code>Origo.Core.Snd.ISndContext</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | snd-context | SND 上下文门面能力组的稳定消费者契约；shell 消费者面向该抽象编译，具体实现留在 kernel 或适配器包中。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Metadata.DataMetaData</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | metadata | 元数据的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.Snd.Metadata.NodeMetaData</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | metadata | 元数据的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.Snd.Metadata.SndInlineTypesAttribute</code> | Origo.Core.Contracts | Tooling extension | Origo.Core.Contracts | metadata-tooling | TypedData 注册的文档化第一方适配器工具契约；生成代码通过 friend 程序集白名单限制实际访问。 | core-tooling; R-DATA |
| <code>Origo.Core.Snd.Metadata.SndMetaData</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | metadata | 元数据的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.Snd.Metadata.SndMetaFluentBuilder</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | metadata | 元数据的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.Snd.Metadata.StrategyMetaData</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | metadata | 元数据的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.Snd.Metadata.StrategyMetaData+ObserverBinding</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | metadata | 元数据的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.Snd.Metadata.TypedData</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | metadata | 元数据的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-DATA |
| <code>Origo.Core.Snd.SndContext</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | snd-kernel | SND 实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Snd.SndContextParameters</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | snd-kernel | SND 实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Snd.SndWorld</code> | Origo.Core.Kernel | Kernel implementation | Origo.Core.Kernel | snd-kernel | SND 实现的 kernel 实现细节；消费者通过 shell 接口或 kernel port 获得行为，0.1.x 不得直接编译依赖。 | n/a (no 0.1.x consumer compatibility promise) |
| <code>Origo.Core.Snd.Strategy.ActiveStrategyBase</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定扩展基类；消费者派生实现，框架必须跨 kernel 保持钩子行为兼容。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Strategy.ActiveStrategyJsonBase`1</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定扩展基类；消费者派生实现，框架必须跨 kernel 保持钩子行为兼容。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Strategy.ActiveStrategyResults</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定消费者辅助 API，属于 shell 编译面且不暴露 kernel 内部。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Strategy.BaseStrategy</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定扩展基类；消费者派生实现，框架必须跨 kernel 保持钩子行为兼容。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Strategy.EntityStrategyExtensions</code> | Origo.Core | Shell contract | Origo.Core | strategy | 策略扩展模型的消费者扩展 API，编译进 shell，并只通过文档化的编排路径生效。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Strategy.LifecycleStrategyBase</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定扩展基类；消费者派生实现，框架必须跨 kernel 保持钩子行为兼容。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Strategy.ObserveDataAttribute</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定声明特性；消费者策略与第一方适配器依赖其元数据契约。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Strategy.ObserverStrategyBase</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定扩展基类；消费者派生实现，框架必须跨 kernel 保持钩子行为兼容。 | core-shell; R-SND |
| <code>Origo.Core.Snd.Strategy.StrategyIndexAttribute</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | strategy | 策略扩展模型的稳定声明特性；消费者策略与第一方适配器依赖其元数据契约。 | core-shell; R-SND |
| <code>Origo.Core.Snd.TryGetNumericExtensions</code> | Origo.Core | Shell contract | Origo.Core | snd-entity | SND 实体/策略消费者 API的消费者扩展 API，编译进 shell，并只通过文档化的编排路径生效。 | core-shell; R-SND |
| <code>Origo.Core.StateMachine.StateMachineStrategyBase</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | state-machine | 状态机的稳定扩展基类；消费者派生实现，框架必须跨 kernel 保持钩子行为兼容。 | core-shell; R-SND |
| <code>Origo.Core.StateMachine.StateMachineStrategyContext</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | state-machine | 状态机的稳定纯数据模型，横跨消费者、生成访问器与存档格式边界。 | core-shell; R-SND |
| <code>Origo.Core.Utility.PathUtility</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | utility | shell 与 kernel 共用的 Contracts 纯平台无关路径工具，保持在消费者编译面。 | core-shell; R-UTIL |
| <code>Origo.GodotAdapter.Bootstrap.OrigoAutoHost</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-entry | Godot 入口的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | adapter-shell; R-GODOT-ENTRY |
| <code>Origo.GodotAdapter.Bootstrap.OrigoDefaultEntry</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-entry | Godot 入口的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | adapter-shell; R-GODOT-ENTRY |
| <code>Origo.GodotAdapter.Bootstrap.OrigoAutoHost+MethodName</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-entry | 已批准 shell Node 入口上的 Godot 生成公开嵌套 signal 类型；随宿主入口分类，属于生成的 shell 表面。 | adapter-shell; R-GODOT-ENTRY |
| <code>Origo.GodotAdapter.Bootstrap.OrigoAutoHost+PropertyName</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-entry | 已批准 shell Node 入口上的 Godot 生成公开嵌套 signal 类型；随宿主入口分类，属于生成的 shell 表面。 | adapter-shell; R-GODOT-ENTRY |
| <code>Origo.GodotAdapter.Bootstrap.OrigoAutoHost+SignalName</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-entry | 已批准 shell Node 入口上的 Godot 生成公开嵌套 signal 类型；随宿主入口分类，属于生成的 shell 表面。 | adapter-shell; R-GODOT-ENTRY |
| <code>Origo.GodotAdapter.Bootstrap.OrigoDefaultEntry+MethodName</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-entry | 已批准 shell Node 入口上的 Godot 生成公开嵌套 signal 类型；随宿主入口分类，属于生成的 shell 表面。 | adapter-shell; R-GODOT-ENTRY |
| <code>Origo.GodotAdapter.Bootstrap.OrigoDefaultEntry+PropertyName</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-entry | 已批准 shell Node 入口上的 Godot 生成公开嵌套 signal 类型；随宿主入口分类，属于生成的 shell 表面。 | adapter-shell; R-GODOT-ENTRY |
| <code>Origo.GodotAdapter.Bootstrap.OrigoDefaultEntry+SignalName</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-entry | 已批准 shell Node 入口上的 Godot 生成公开嵌套 signal 类型；随宿主入口分类，属于生成的 shell 表面。 | adapter-shell; R-GODOT-ENTRY |
| <code>Origo.GodotAdapter.Console.CommandHandlerBase</code> | Origo.GodotAdapter | Tooling extension | Origo.GodotAdapter | godot-tooling | Godot 工具扩展的文档化工具扩展契约；适配器与消费者工具实现它时无需接触 kernel 内部。 | adapter-tooling; R-GODOT-TOOLING |
| <code>Origo.GodotAdapter.Serialization.GodotJsonConverterRegistry</code> | Origo.GodotAdapter | Tooling extension | Origo.GodotAdapter | godot-tooling | Godot 工具扩展的文档化工具扩展契约；适配器与消费者工具实现它时无需接触 kernel 内部。 | adapter-tooling; R-GODOT-TOOLING |
| <code>Origo.GodotAdapter.FileSystem.GodotFileSystem</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-shell | Godot shell 实现的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | adapter-shell; R-GODOT-SHELL |
| <code>Origo.GodotAdapter.Logging.GodotLogger</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-shell | Godot shell 实现的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | adapter-shell; R-GODOT-SHELL |
| <code>Origo.GodotAdapter.SndEntityNodeExtensions</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-shell | Godot shell 实现的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | adapter-shell; R-GODOT-SHELL |
| <code>Origo.GodotAdapter.Snd.GodotPackedSceneNodeFactory</code> | Origo.GodotAdapter | Shell contract | Origo.GodotAdapter | godot-shell | Godot shell 实现的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | adapter-shell; R-GODOT-SHELL |
| <code>Origo.ConsoleBridge.ConsoleBridgeOptions</code> | Origo.ConsoleBridge | Shell contract | Origo.ConsoleBridge | bridge | 控制台桥接 shell的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | bridge-shell; R-BRIDGE |
| <code>Origo.ConsoleBridge.ConsoleBridgeServer</code> | Origo.ConsoleBridge | Shell contract | Origo.ConsoleBridge | bridge | 控制台桥接 shell的消费者 shell 具体实现，只依赖 Contracts，在缺少 kernel 编译资产时仍可使用。 | bridge-shell; R-BRIDGE |
| <code>Origo.Core.Abstractions.Runtime.IOrigoRuntime</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | host-runtime | 通过 Core shell host facade 暴露的稳定 runtime 契约；具体 runtime 仍是 kernel 实现。 | core-shell; R-HOST |
| <code>Origo.Core.Abstractions.Runtime.ISndWorldAccess</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | host-runtime | shell 消费者使用的稳定策略、映射与数据源访问表面；具体 SND world 仍是 kernel 实现。 | core-shell; R-HOST |
| <code>Origo.Core.OrigoHostOptions</code> | Origo.Core.Contracts | Shell contract | Origo.Core.Contracts | host-runtime | Core shell host facade 的稳定配置模型；所有值类型均为 Contracts 类型。 | core-shell; R-HOST |
| <code>Origo.Core.OrigoHost</code> | Origo.Core | Shell contract | Origo.Core | host-runtime | 面向消费者的 Core shell host facade；通过 internal kernel port 构造 kernel runtime 与 SND context，不暴露 kernel 编译资产。 | core-shell; R-HOST |
<!-- shell-api-classification:end -->
