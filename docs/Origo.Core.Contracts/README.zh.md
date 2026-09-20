<!-- docsync-pair: Origo.Core.Contracts/README -->
<!-- docsync-revision: 4 -->
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
| [Abstractions](Abstractions/README.zh.md) | 平台无关的基础抽象 | 日志、控制台输入输出、文件系统/路径、节点与帧驱动契约 |
| [Runtime](Runtime/README.zh.md) | 运行时工具扩展契约 | 控制台命令处理器、调用模型与参数校验基类 |
| [Snd](Snd/README.zh.md) | SND 数据契约 | TypedData 内联存储与实体元数据模型 |

## 本层文件

| 文件 | 职责 |
|------|------|
| `OrigoMeta.cs` | 框架元数据：名称、版本号、默认横幅文本 |
| `AssemblyAttributes.cs` | `[assembly: SndInlineTypes(...)]` Home 宿主内联类型注册：声明 TypedData 支持的系统基础类型与 string |

## 架构约束

- **零引擎依赖**：仅使用 `System.*` 与 .NET BCL，不引用 Godot 或其它适配器。
- **只放稳定契约**：接口、纯数据、扩展基类与工具契约；具体实现保留在 kernel 或 shell 包。
- **单一依赖方向**：`Origo.Core.Contracts` 不引用 `Origo.Core`、`Origo.Core.Kernel`、
  `Origo.GodotAdapter` 或 `Origo.ConsoleBridge`。

## 依赖方向

```
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel
        ▲
        │
Origo.Core ──► Origo.SourceGeneration (analyzer)
        ▲
        │
Origo.GodotAdapter
```

适配层和实现层依赖契约层；契约层绝不反向依赖任何实现。

---
[↑ 回到 Origo.manual](../README.zh.md)
