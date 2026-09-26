<!-- docsync-pair: Origo.Core.Kernel/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Origo.Core.Kernel

> [↑ 回到 Origo Manual](../README.zh.md)

## 模块概述

**Origo.Core.Kernel** 是 Origo 的 kernel 实现包：只引用
`Origo.Core.Contracts`，承载 runtime 构造、SND 内部、持久化/存储、
data-source codec 与 factory、console 路由、vendor 噪声、延迟调度以及 internal
`Origo.Core.Kernel.Ports`。kernel 编译资产不进入 Core shell 消费者编译面。

## 子系统一览

| 子系统 | 能力 | 详情 |
|--------|------|------|
| [Abstractions](Abstractions/README.zh.md) | internal 框架契约 | 实体生命周期、node host、scene host/access 与 session 绑定 |
| [DataSource](DataSource/README.zh.md) | 数据源实现 | JSON/Map codec、factory、I/O gateway、converter 与路径/文件访问 |
| [Runtime](Runtime/README.zh.md) | 运行时生命周期与控制台 | System/Progress/Session 四层、帧队列与 console 路由 |
| [Save](Save/README.zh.md) | 持久化实现 | save coordinator、payload、严格读取、原子写入与存储布局 |
| [Snd](Snd/README.zh.md) | SND 实现 | context/world、entity 聚合、scene host、策略池/管理器与 observer topology |
| [StateMachine](StateMachine/README.zh.md) | 状态机实现 | 栈状态机与持久化模型 |
| [Ports](Ports/README.zh.md) | kernel-shell port | internal host 构造 port，经 `InternalsVisibleTo` 提供给 shell 程序集 |
| [Scheduling](Scheduling/README.zh.md) | 延迟调度实现 | `IScheduler` + `ActionScheduler` + `ConcurrentActionQueue` |
| [Addons](Addons/README.zh.md) | Vendor 第三方库 | FastNoiseLite 噪声实现 |

## 本层文件

本目录仅包含子目录，无直接 `.cs` 文件。

## 架构约束

- **只依赖 Contracts**：Kernel 不引用 `Origo.Core`、`Origo.GodotAdapter`、
  `Origo.ConsoleBridge` 或任何 Godot 程序集。
- **不对消费者公开编译资产**：kernel 类型不进入 Core shell 消费者编译面；
  消费者使用 Contracts 与 Core shell facade。
- **Internal port**：`Origo.Core.Kernel.Ports` 成员保持 internal，仅通过
  `InternalsVisibleTo` 提供给 shell 程序集与测试。
- **Package 依赖边界**：`Origo.Core` 对 Kernel 使用 `PrivateAssets="compile"`，
  NuGet 依赖携带 runtime/build/native 资产但不携带 compile 资产。

## 依赖方向

```
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel ◄── runtime-only ── Origo.Core shell
```

---
[↑ 回到 Origo Manual](../README.zh.md)
