# Origo.Core

> [↑ 回到 Origo.manual](../README.md)

## 模块概述

**Origo.Core** 是 Origo 框架的平台无关核心。不依赖任何引擎类型（Godot、Unity 等），仅使用 `System.*` 和 .NET BCL。所有游戏逻辑、存档系统、实体模型、状态机在此层实现，通过接口注入与适配层的差异。

## 子系统一览

| 子系统 | 能力 | 详情 |
|--------|------|------|
| [Abstractions](Abstractions/README.md) | 11 组公共抽象接口 | IBlackboard / IFilesystem / ILogger / ISndEntity / IStateMachine ... |
| [Addons](Addons/README.md) | Vendor 第三方库 | FastNoiseLite v1.1.1（噪声生成）|
| [Blackboard](Blackboard/README.md) | IBlackboard 默认实现 | 基于 Dictionary + TypedData 的内存黑板 |
| [DataSource](DataSource/README.md) | 数据源抽象层 | DataSourceNode 树模型 + JSON/Map 编解码 + 类型转换器注册 |
| [Grid](Grid/README.md) | 网格坐标系工具 | GridCoordinateSystem：网格 ↔ 世界坐标双向转换 |
| [Logging](Logging/README.md) | 日志系统 | LogMessageBuilder（结构化构建）+ NullLogger（测试静默）|
| [Planning](Planning/README.md) | 行为规划系统 | PlanExecutionStrategyBase：意图驱动计划执行 + EnsureReplaceableStrategy 扩展 |
| [Random](Random/README.md) | 随机数系统 | XorShift128+ 伪随机数 + PersistentRandom + Simplex/Worley 噪声图 |
| [Runtime](Runtime/README.md) | 运行时核心 | 四层生命周期 + 控制台 + 状态机容器 + OrigoRuntime |
| [Save](Save/README.md) | 持久化系统 | 两阶段写入 + 严格读取 + 路径策略 + meta.map |
| [Scheduling](Scheduling/README.md) | 延迟调度 | ActionScheduler + 线程安全 ConcurrentActionQueue |
| [Serialization](Serialization/README.md) | 类型映射 | TypeStringMapping（CLR 类型 ↔ 稳定字符串标识）|
| [Snd](Snd/README.md) | SND 实体系统 | 策略→实体→数据→场景宿主→数值配方加载 完整堆栈 |
| [StateMachine](StateMachine/README.md) | 字符串栈状态机 | StackStateMachine + 策略钩子 + 持久化模型 |
| [Testing](Testing/README.md) | 测试基础设施 | StrategyTestScenario：策略隔离测试框架 |
| [Utility](Utility/README.md) | 通用工具 | DiffUtility：集合差异比较 |

> TypedData 源码生成器是独立项目 [Origo.SourceGeneration](../Origo.SourceGeneration/README.md)，不在 Core 内。

## 本层文件

| 文件 | 职责 |
|------|------|
| `OrigoMeta.cs` | 框架元数据：名称、版本号、默认横幅 |

## 架构约束

- **禁止 Godot 引用**：Origo.Core 的 `.csproj` 和源码中不得出现 `Godot`、`GodotSharp` 命名空间或程序集引用
- **IO 经由 Gateway**：所有文件读写必须通过 `IDataSourceIoGateway`，禁止直接 `File.*`
- **Core 可测试性**：能否在单测中完整运行核心业务逻辑，无需 mock 文件系统/时钟以外的任何东西？

## 依赖方向

```
Origo.Core (平台无关)
    ↑ 实现接口
Origo.GodotAdapter (引擎适配)
    ↑ 注入差异
Origo.ConsoleBridge (独立服务)
```

适配层依赖 Core 的抽象接口并注入具体实现，Core 绝不反向依赖适配层。

---
[↑ 回到 Origo.manual](../README.md)
