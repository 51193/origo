<!-- docsync-pair: Origo.Core.Contracts/Snd/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Snd

> [↑ 回到 Origo.Core.Contracts](../README.zh.md)

## 模块能力

SND 契约层。包含 TypedData 内联存储/访问模型、实体元数据模型与
`ISndContext` 统一业务门面接口。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Metadata](Metadata/README.zh.md) | TypedData 与实体元数据模型 | TypedData / SndMetaData / NodeMetaData / StrategyMetaData / DataMetaData / SndMetaFluentBuilder |

## 本层文件

| 文件 | 职责 |
|------|------|
| `ISndContext.cs` | SND 统一业务门面接口：Bootstrap + 10 个 companion 属性 |

本目录另包含 Metadata 子目录。

---
[↑ 回到 Origo.Core.Contracts](../README.zh.md)
