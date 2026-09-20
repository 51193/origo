<!-- docsync-pair: Origo.Core/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Origo.Core

> [↑ 回到 Origo.manual](../README.zh.md)

## 模块概述

**Origo.Core** 是 Origo 框架的平台无关核心。不依赖任何引擎类型（Godot、Unity 等），仅使用 `System.*` 和 .NET BCL。所有游戏逻辑、存档系统、实体模型、状态机在此层实现，通过接口注入与适配层的差异。

## 子系统一览

| 子系统 | 能力 | 详情 |
|--------|------|------|
| [Contracts](../Origo.Core.Contracts/README.zh.md) | 稳定消费者契约 | 日志、控制台、文件系统与路径等平台叶级契约；本实现层与适配层共同引用 |
| [Origo.Core.Kernel](../Origo.Core.Kernel/README.zh.md) | Kernel 实现包 | FastNoiseLite vendor 噪声与延迟调度；runtime/SND/持久化当前仍在本层 |
| [Abstractions](Abstractions/README.zh.md) | 核心抽象接口 | IBlackboard / ISndEntity / IStateMachine / INode* ... |
| [Blackboard](Blackboard/README.zh.md) | IBlackboard 默认实现 | 基于 Dictionary + TypedData 的内存黑板 |
| [DataSource](DataSource/README.zh.md) | 数据源抽象层 | DataSourceNode 树模型 + JSON/Map 编解码 + 类型转换器注册 |
| [Grid](Grid/README.zh.md) | 网格坐标系工具 | GridCoordinateSystem：网格 ↔ 世界坐标双向转换 |
| [Logging](Logging/README.zh.md) | 日志系统 | LogMessageBuilder（结构化构建）+ NullLogger（测试静默）|
| [Planning](Planning/README.zh.md) | 行为规划系统 | PlanExecutionStrategyBase：意图驱动计划执行 + EnsureReplaceableStrategy 扩展 |
| [Random](Random/README.zh.md) | 随机数系统 | XorShift128+ 伪随机数 + PersistentRandom + Simplex/Worley 噪声图 |
| [Runtime](Runtime/README.zh.md) | 运行时核心 | 四层生命周期 + 控制台 + 状态机容器 + OrigoRuntime |
| [Save](Save/README.zh.md) | 持久化系统 | 两阶段写入 + 严格读取 + 路径策略 + meta.map |
| [Serialization](Serialization/README.zh.md) | 类型映射 | TypeStringMapping（CLR 类型 ↔ 稳定字符串标识）|
| [Snd](Snd/README.zh.md) | SND 实体系统 | 策略→实体→数据→场景宿主→数值配方加载 完整堆栈 |
| [StateMachine](StateMachine/README.zh.md) | 字符串栈状态机 | StackStateMachine + 策略钩子 + 持久化模型 |
| [Utility](Utility/README.zh.md) | 通用工具 | 路径规范化（PathUtility）与字符串到类型值推断（ValueInference） |

> TypedData 源码生成器是独立项目 [Origo.SourceGeneration](../Origo.SourceGeneration/README.zh.md)，不在 Core 内。
>
> 框架元数据与平台叶级契约位于稳定契约包 [Origo.Core.Contracts](../Origo.Core.Contracts/README.zh.md)。

## 本层文件

| 文件 | 职责 |
|------|------|
| `AssemblyAttributes.cs` | `[assembly: SndInlineTypes(...)]` 宿主内联类型注册：声明 Core 支持的系统基础类型与 string |

## 架构约束

- **禁止 Godot 引用**：Origo.Core 的 `.csproj` 和源码中不得出现 `Godot`、`GodotSharp` 命名空间或程序集引用
- **IO 经由 Gateway**：所有文件读写必须通过 `IDataSourceIoGateway`，禁止直接 `File.*`
- **Core 可测试性**：能否在单测中完整运行核心业务逻辑，无需 mock 文件系统/时钟以外的任何东西？

## 依赖方向

```
Origo.Core.Contracts (稳定契约)
        ▲
        │
Origo.Core.Kernel (Kernel 实现)
        ▲
        │
Origo.Core (平台无关实现与当前 host facade)
        ▲ 实现接口
Origo.GodotAdapter (引擎适配)
        ▲ 注入差异
Origo.ConsoleBridge (独立服务)
```

适配层依赖 Core 的抽象接口并注入具体实现，Core 绝不反向依赖适配层；
稳定契约层不依赖任何实现程序集。

---
[↑ 回到 Origo.manual](../README.zh.md)
