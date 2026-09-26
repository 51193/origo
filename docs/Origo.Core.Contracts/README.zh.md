<!-- docsync-pair: Origo.Core.Contracts/README -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Origo.Core.Contracts

> [↑ 回到 Origo.manual](../README.zh.md)

## 模块概述

**Origo.Core.Contracts** 是 Origo 的稳定消费者契约包：只包含平台无关的公共接口、
纯数据与扩展契约，不包含运行时实现、持久化、控制台路由或 Godot 依赖。shell 与
kernel 包共同引用这一契约层，消费者可以面向它编译。

## 子系统一览

| 子系统 | 能力 | 详情 |
|--------|------|------|
| [Abstractions](Abstractions/README.zh.md) | 平台无关基础抽象 | 日志、控制台 I/O、文件系统/路径、节点、帧驱动、生命周期、实体、场景与状态机契约 |
| [Runtime](Runtime/README.zh.md) | Runtime 契约与工具 | `IOrigoRuntime`/`ISndWorldAccess`、控制台 handler/调用模型与参数校验 |
| [Blackboard](Blackboard/README.zh.md) | 共享黑板实现 | shell 与 kernel 共用的内存 `IBlackboard` 实现 |
| [Grid](Grid/README.zh.md) | 网格值类型 | `GridPos` |
| [Logging](Logging/README.zh.md) | 共享日志实现 | `Logger<T>`、`LogMessageBuilder` 与 `NullLogger` |
| [Planning](Planning/README.zh.md) | 规划策略基类 | `PlanExecutionStrategyBase` |
| [Serialization](Serialization/README.zh.md) | 共享类型映射 | `TypeStringMapping` |
| [DataSource](DataSource/README.zh.md) | Data-source 契约 | 树数据节点、I/O gateway、文件元数据契约、转换器基类与 registry |
| [Snd](Snd/README.zh.md) | SND 数据契约 | TypedData、metadata、ISndContext、策略基类与 internal 实体查询契约 |
| [Save](Save/README.zh.md) | 存档契约 | 展示 metadata contributor 与 save-slot entry |
| [StateMachine](StateMachine/README.zh.md) | 状态机契约 | 策略基类与单次操作 context |
| [Utility](Utility/README.zh.md) | 共享纯工具 | `PathUtility` 与 internal 值推断 |

## 本层文件

| 文件 | 职责 |
|------|------|
| `OrigoMeta.cs` | 框架元数据：名称、版本号、默认横幅文本 |
| `OrigoHostOptions.cs` | Core shell host facade 的稳定配置模型 |
| `AssemblyAttributes.cs` | `[assembly: SndInlineTypes(...)]` Home 宿主内联类型注册：声明 TypedData 支持的系统基础类型与 string |

## 架构约束

- **零引擎依赖**：仅使用 `System.*` 与 .NET BCL，不引用 Godot 或其它适配器。
- **只放稳定契约**：接口、纯数据、扩展基类与工具契约；具体实现保留在 kernel 或 shell 包。
- **单一依赖方向**：`Origo.Core.Contracts` 不引用 `Origo.Core`、`Origo.Core.Kernel`、
  `Origo.GodotAdapter` 或 `Origo.ConsoleBridge`。

## 依赖方向

```
Origo.Core.Contracts ──► Origo.SourceGeneration (analyzer packaging)
        ▲
        ├──────────────────────┐
        │                      │
Origo.Core.Kernel      Origo.ConsoleBridge shell
        ▲
        │ runtime-only
Origo.Core shell
        ▲
        │
Origo.GodotAdapter
```

适配层、控制台桥接和实现层依赖契约层；契约层绝不反向依赖任何实现。

---
[↑ 回到 Origo.manual](../README.zh.md)
