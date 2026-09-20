<!-- docsync-pair: Origo.Core.Kernel/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Origo.Core.Kernel

> [↑ 回到 Origo.manual](../README.zh.md)

## 模块概述

**Origo.Core.Kernel** 是 Origo 的 kernel 实现包：只引用稳定契约包
`Origo.Core.Contracts`，承载不适合进入消费者编译面的实现。当前包含 vendor
噪声实现与延迟调度实现；runtime 构造、SND 内部、持久化与 console 路由仍位于
`Origo.Core`。

## 子系统一览

| 子系统 | 能力 | 详情 |
|--------|------|------|
| [Addons](Addons/README.zh.md) | Vendor 第三方库 | FastNoiseLite 噪声实现 |
| [Scheduling](Scheduling/README.zh.md) | 延迟调度实现 | `IScheduler` + `ActionScheduler` + `ConcurrentActionQueue` |

## 本层文件

本目录仅包含子目录，无直接 `.cs` 文件。

## 架构约束

- **只依赖 Contracts**：Kernel 不引用 `Origo.Core`、`Origo.GodotAdapter`、
  `Origo.ConsoleBridge` 或任何 Godot 程序集。
- **不对消费者公开编译资产**：kernel 类型不进入 shell 消费者的编译面；
  shell 只能通过 Contracts 或文档化的 kernel-shell port 使用其能力。
- **Internal 桥接**：调度实现保持 internal，经 `InternalsVisibleTo` 使
  `Origo.Core` 与测试程序集访问；当前 runtime 构造仍位于 `Origo.Core`。
- **Package 依赖边界**：`Origo.Core` 对 Kernel 的 ProjectReference 使用
  `PrivateAssets="compile"`，NuGet 依赖携带 runtime/build/native 资产但不携带
  compile 资产。

## 依赖方向

```
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel
        ▲
        │
Origo.Core (shell / current host facade)
```

---
[↑ 回到 Origo.manual](../README.zh.md)
