# 能力清单

> [↑ 回到使用文档](README.md)

Origo 框架的全部能力，按功能域组织。每个条目包含能力说明和文档入口，方便开发者按需查阅。

## 实体与策略

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| SND 实体模型 | Strategy（行为）、Node（表现）、Data（状态）三元解耦模型 | [SND 实体模型](snd-entity-model.md) |
| 8 个生命周期钩子 | AfterSpawn / AfterLoad / AfterAdd / Process / BeforeRemove / BeforeSave / BeforeQuit / BeforeDead | [SND 实体模型](snd-entity-model.md) |
| 无状态策略池 | 策略实例共享复用，注册时反射校验无状态约束，引用计数管理 | [↔ Snd/Strategy](../Origo.Core/Snd/Strategy/README.md) |
| 策略优先级排序 | Process 等钩子按 Priority 升序执行，同优先级 FIFO | [SND 实体模型](snd-entity-model.md) |
| TypedData 类型保持 | 只读 partial struct 内联存储，Source Generator 生成类型化转换，JSON 往返不丢失精度 | [SND 实体模型](snd-entity-model.md) |
| 数据观察者 | Subscribe 按 key 监听实体数据变更，支持可选的 filter 过滤 | [SND 实体模型](snd-entity-model.md) |
| 跨实体观察 | ObserveData / ObserveLifecycle 实现跨实体数据与生命周期观察，Teardown 自动清理 | [Agent Reference](agent-reference.md) |
| 主动策略 | 按索引外部调用 Invoke，与被动策略独立容器管理，O(1) 查找 | [策略测试](strategy-testing.md) |
| 泛型主动策略调用 | `InvokeStrategy<TInput, TOutput>` 扩展方法，类型安全消除 JSON 序列化样板 | [↔ Snd/Strategy](../Origo.Core/Snd/Strategy/README.md) |
| SndMetaFluentBuilder | 链式 API 构建实体元数据，消除 `??= new DataMetaData()` 样板 | [↔ Snd/Metadata](../Origo.Core/Snd/Metadata/README.md) |
| TryGetNumeric | 实体数据数值兼容读取，桥接 `SetData("k", 5)` (int) 与 `TryGetData<float>("k")` 的类型不匹配 | [↔ Snd](../Origo.Core/Snd/README.md) |
| 数值配方加载 | SndArchetypeLoader 从键值对文件加载 archetype 并推断类型写入实体 | [↔ Snd/Archetype](../Origo.Core/Snd/Archetype/README.md) |
| 惰性策略挂载 | EnsureStrategy 扩展方法，带幂等守卫的惰性策略层初始化 | [SND 实体模型](snd-entity-model.md)、[↔ Snd/Strategy](../Origo.Core/Snd/Strategy/README.md) |

## 会话管理

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| 四层运行时 | SystemRun → ProgressRun → SessionManager → SessionRun 分层生命周期 | [架构概览](architecture-overview.md) |
| 前后台 Session 同构 | 后台 Session 与前台走同一套 ISessionRun 接口与策略管线 | [Session 模型](session-model.md) |
| 会话拓扑编码 | SessionTopology 文本格式编解码，记录所有活跃 Session 的 key/levelId/syncProcess | [Session 模型](session-model.md) |
| LevelId 全局唯一 | 每个 levelId 同一时刻只允许一个 Session 存在，冲突时抛出异常 | [Session 模型](session-model.md) |

## 持久化

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| 两阶段写入 | 先写 `current/`（带 .write_in_progress 标记），校验通过后原子复制到 `save_{id}/` | [持久化流程](persistence-flow.md) |
| 严格读取校验 | .write_in_progress 标记检测、关卡三件套完整性校验、progress.json 强制存在 | [持久化流程](persistence-flow.md) |
| 快照管理 | EnumerateSaveIds / EnumerateSavesWithMetaData，支持保存选择 UI | [持久化流程](persistence-flow.md) |
| meta.map 显示元数据 | 与业务数据分离的显示元数据系统，ISaveMetaContributor 插件式贡献者模式 | [持久化流程](persistence-flow.md) |
| 幂等去重 | SHA256 哈希比对，相同游戏状态跳过 I/O 写入 | [↔ Save/Storage](../Origo.Core/Save/Storage/README.md) |

## 状态机

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| 字符串栈状态机 | 栈内仅存字符串标识，行为由关联的 Push/Pop 策略定义 | [状态机](state-machine.md) |
| Push/Pop 策略钩子 | OnPushRuntime / OnPushAfterLoad / OnPopRuntime / OnPopBeforeQuit，含 BeforeTop/AfterTop 迁移上下文 | [状态机](state-machine.md) |
| 两阶段加载恢复 | RestoreStackWithoutHooks（静默恢复栈）→ FlushAfterLoad（按序回放钩子） | [状态机](state-machine.md) |

## 控制台系统

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| 11 个内置命令 | help / bb_get / bb_set / bb_keys / spawn / find_entity / kill_all / snd_count / entity_get_data / entity_set_data / invoke_strategy | [控制台命令](console-commands.md) |
| 自定义命令注册 | Core 层继承 ConsoleCommandHandlerBase，适配层继承 CommandHandlerBase | [控制台命令](console-commands.md) |
| TCP 远程控制台桥接 | ConsoleBridgeServer 监听 localhost:9876，单连接模式，双向 I/O 经由 pub-sub | [控制台命令](console-commands.md) |
| 命令类型推断 | bb_set / entity_set_data 自动推断 int/float/bool/string 类型，已存在 key 保持原类型 | [控制台命令](console-commands.md) |

## 数据与序列化

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| DataSource 抽象层 | 统一树数据模型 DataSourceNode（Map/Array/Text/Number/Bool/Null + Lazy），通过 IDataSourceIoGateway 作为 Core 层唯一文件入口 | [↔ DataSource](../Origo.Core/DataSource/README.md) |
| JSON + .map 编解码 | JsonDataSourceCodec（延迟展开）、MapDataSourceCodec（key:value 扁平结构） | [↔ DataSource/Codec](../Origo.Core/DataSource/Codec/README.md) |
| 延迟 JSON 展开 | 嵌套对象/数组仅在首次访问时解析，分摊大型存档的解析开销 | [↔ DataSource](../Origo.Core/DataSource/README.md) |
| 类型-字符串双向映射 | TypeStringMapping 保持 CLR 类型与稳定字符串标识的双向映射，避免 FullName 版本耦合 | [↔ Serialization](../Origo.Core/Serialization/README.md) |
| Godot 14 种类型序列化 | Vector2/3/4、Vector2I/3I、Quaternion、Color、Basis、Transform2D/3D、Rect2/2I、Aabb、Plane 完整 JSON 往返 | [↔ GodotAdapter/Serialization](../Origo.GodotAdapter/Serialization/README.md) |
| 转换器注册与继承回溯 | DataSourceConverterRegistry 在精确类型未注册时沿基类链和接口链回溯查找转换器 | [↔ DataSource/Converters](../Origo.Core/DataSource/Converters/README.md) |
| 策略文件访问（ISndFileAccess） | 策略通过 ISndContext 读写 JSON/Map 文件，经 IDataSourceIoGateway 边界自动解析为 DataSourceNode 树或强类型对象 | [架构概览](architecture-overview.md)、[↔ Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.md) |
| 存档内文件访问（ISndArchiveFileAccess） | 策略通过 ISndContext 在存档 extra/ 子目录中读写文件（含删除），文件随存档生命周期管理：写入后纳入 save snapshot，load 时自动恢复 | [架构概览](architecture-overview.md)、[↔ Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.md) |

## Godot 适配器

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| res:// + user:// 文件系统 | GodotFileSystem 实现 IFileSystem，支持虚拟路径和路径穿越防护 | [↔ GodotAdapter/FileSystem](../Origo.GodotAdapter/FileSystem/README.md) |
| 日志代理 | GodotLogger 通过委托注入 GD.Print / PushWarning / PushError，无内部格式化 | [↔ GodotAdapter/Logging](../Origo.GodotAdapter/Logging/README.md) |
| PackedScene 节点实例化 | GodotPackedSceneNodeFactory 从资源路径加载场景并实例化为 GodotNodeHandle | [↔ GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.md) |
| GodotEntity + StableName | GodotSndEntity 桥接 ISndEntity 与 Godot Node 生命周期，独立 StableName 避免 Godot 自动重命名干扰 | [↔ GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.md) |
| 场景别名解析 | 通过 SndMappings 将逻辑别名解析为 res:// 资源路径 | [↔ GodotAdapter/Bootstrap](../Origo.GodotAdapter/Bootstrap/README.md) |
| 适配层控制台命令 | press_button（模拟按钮点击）、tree_debug（打印实体节点树） | [↔ GodotAdapter/Console](../Origo.GodotAdapter/Console/README.md) |

## 测试基础设施

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| StrategyTestScenario | 声明式策略单元测试框架，三阶段模式（Configure → Simulate → Inspect） | [策略测试](strategy-testing.md) |
| ActiveStrategy 测试 | 独立的 ActiveStrategy 测试 Harness，Invoke / InvokeViaEntity / 数据读写 | [策略测试](strategy-testing.md) |
| 架构护栏测试 | Core / Adapter / ConsoleBridge 三层各自验证依赖方向、接口组合、策略无状态等架构约束 | [↔ Core.Tests](../Origo.Core.Tests/README.md)、[↔ GodotAdapter.Tests](../Origo.GodotAdapter.Tests/README.md)、[↔ ConsoleBridge.Tests](../Origo.ConsoleBridge.Tests/README.md) |

## 辅助能力

| 能力 | 说明 | 文档入口 |
|------|------|----------|
| XorShift128+ 随机数 | 周期 2^128-1，无全局状态，调用方显式传递种子，跨平台一致性 | [↔ Random](../Origo.Core/Random/README.md) |
| PersistentRandom | 黑板持久化随机状态：InitSeed → TryNextInt32/NextInt32/NextFloat，存档安全可恢复 | [↔ Random](../Origo.Core/Random/README.md) |
| 2D 噪声图生成 | OpenSimplex2 (70%) + Worley Cellular (30%) 混合噪声，基础 + 扩展重载（自定义 octaves/lacunarity/gain） | [↔ Random](../Origo.Core/Random/README.md) |
| 网格坐标系 | GridPos 类型、GridCoordinateSystem 单/双轴转换、A* 寻路、GridParser 坐标解析 | [↔ Grid](../Origo.Core/Grid/README.md) |
| 内存黑板 | IBlackboard 默认实现，SetValue/TryGet/SerializeAll/DeserializeAll，key 大小写敏感 | [↔ Blackboard](../Origo.Core/Blackboard/README.md) |
| 延迟动作调度 | ConcurrentActionQueue 线程安全队列，快照-排干模式，支持执行中再次入队 | [↔ Scheduling](../Origo.Core/Scheduling/README.md) |
| 结构化日志构建器 | LogMessageBuilder 流式 API（SetElapsedMs / AddPrefix / AddSuffix / Build） | [↔ Logging](../Origo.Core/Logging/README.md) |

## 框架设计属性

| 属性 | 说明 | 文档入口 |
|------|------|----------|
| 平台无关 | Origo.Core 仅依赖 System.\*，不引用任何引擎特定代码 | [架构概览](architecture-overview.md) |
| 适配层隔离 | 引擎代码仅在 Origo.GodotAdapter 实现 Core 抽象，适配层不参与策略生命周期管理 | [架构概览](architecture-overview.md) |
| 接口隔离（ISP） | ISndContext 拆分为 11 个窄角色接口，ISessionRun 返回抽象 IStateMachineContainer | [架构概览](architecture-overview.md) |
| 单线程帧模型 | 一帧 = 一个逻辑原子边界，延迟动作通过队列顺序执行。宿主（如 Godot `_Process`）通过 `IOrigoFrameDriver.DriveFrame(double delta)` 驱动帧，Core 内部顺序：实体 Process → 业务队列 → Kill 待处理 → 系统队列 → 控制台 | [架构概览](architecture-overview.md) |

---

> **使用建议**：新用户可从 [Quick Start](quick-start.md) 开始，然后浏览本清单选择感兴趣的能力深入阅读。
>
> **AI Agent**：请直接查阅 [Agent Reference](agent-reference.md) 获取完整接口签名与运行时参考。

[↑ 回到使用文档](README.md)
