<!-- docsync-pair: architecture/extension-directions -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 扩展方向与暂缓设计

> [↑ 回到 architecture](README.zh.md)

> **性质说明**：本文档是暂缓设计方向的索引。每个方向的完整上下文、设计门禁、重新评估信号与验收标准记录在对应的 GitHub issue 中；本文档不再保存完整设想正文，避免形成第二套待办来源。只有方向增加、移除或状态变化时才更新本页。

阅读本页前，应先理解当前现状：[架构总览](overview.zh.md)、[SND 实体模型](../usage/snd-entity-model.zh.md)、[策略生命周期](../usage/strategy-lifecycle.zh.md)、[设计模式](../usage/design-patterns.zh.md)。

## 方向索引

| 方向 | 状态 | 跟踪 issue | 说明 |
|------|------|------------|------|
| 统一树形命名空间 | blocked-design | [#44](https://github.com/51193/origo/issues/44) | 受限树根、内容/元数据边界与远端异步 I/O 尚未决策 |
| 实体级并发 | waiting-signal | [#45](https://github.com/51193/origo/issues/45) | 等待性能 profile 信号；实体内策略仍保持偏序串行 |
| ActiveStrategy 同名多实现 | blocked-design | [#46](https://github.com/51193/origo/issues/46) | 契约名、每实体绑定与存档恢复语义尚未决策 |

## 维护规则

- 暂缓方向必须以 GitHub issue 作为唯一跟踪载体；本文档只保留一行索引和当前状态。
- issue 被实现、取消或拆分时，在同一变更中更新本页，并保持中英文一致。
- 方向的完整权衡以 issue 正文和冻结提交中的历史版本为准；不要在模块 README 中复制长篇设想。

## 关联文档

- 现状架构：[架构总览](overview.zh.md)
- 策略系统实现：[Strategy 模块](../Origo.Core/Snd/Strategy/README.zh.md)
- 数据源实现：[DataSource 模块](../Origo.Core/DataSource/README.zh.md)
- 调度实现：[Scheduling 模块](../Origo.Core.Kernel/Scheduling/README.zh.md)
- 实体实现：[Entity 模块](../Origo.Core/Snd/Entity/README.zh.md)
- 常用模式：[设计模式](../usage/design-patterns.zh.md)

---
[↑ 回到 architecture](README.zh.md)
