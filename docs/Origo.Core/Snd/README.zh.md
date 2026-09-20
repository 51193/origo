<!-- docsync-pair: Origo.Core/Snd/README -->
<!-- docsync-revision: 18 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Snd（Shell 辅助）

> [↑ 回到 Origo.Core](../README.zh.md)

## 模块能力

面向消费者的 SND shell 辅助。SND context、world、entity、scene 与 strategy
实现位于 [Origo.Core.Kernel/Snd](../../Origo.Core.Kernel/Snd/README.zh.md)；
稳定策略/meta 契约位于 [Origo.Core.Contracts/Snd](../../Origo.Core.Contracts/Snd/README.zh.md)。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Strategy](Strategy/README.zh.md) | 策略 shell 扩展 | `EnsureReplaceableStrategy` |
| [Archetype](Archetype/README.zh.md) | 数值配方加载 | `SndArchetypeLoader` 键值解析与 typed-data 应用 |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `ActiveStrategyExtensions.cs` | 泛型 active strategy 调用与幂等 `EnsureStrategy` |
| `EntityExtensions.cs` | 跨 inner/wrapper 引用的实体身份比较 |
| `TryGetNumericExtensions.cs` | 基于 `ISndDataAccess` 的数值兼容读取 |

## 架构说明

- 这些辅助只编译依赖 Contracts，不暴露 kernel 实现类型。
- `ISndContext`、实体角色接口与 metadata 位于 Contracts。

---
[↑ 回到 Origo.Core](../README.zh.md)
