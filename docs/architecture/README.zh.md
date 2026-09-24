<!-- docsync-pair: architecture/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 架构文档

> [↑ 回到 Origo 手册](../README.zh.md)

Origo 框架的架构总览、架构决策记录与暂缓设计方向。

## 文档索引

| 文档 | 说明 |
|------|------|
| [overview](overview.zh.md) | 四层运行时、SND 实体模型、持久化、并发模型与 Core/Adapter 分层原则 |
| [strategy-ordering](strategy-ordering.zh.md) | 生命周期策略相对顺序约束的决策、已知边界与演进选项 |
| [extension-directions](extension-directions.zh.md) | 暂缓设计方向的 issue 索引；完整权衡、门禁与重新评估信号在对应 issue |
| [agent-friendly](agent-friendly/README.zh.md) | Agent 友好度基准、OpenAI 工程实践、四项改造调查与游戏开发产品方向 |
| [shell-kernel-boundary](shell-kernel-boundary.zh.md) | 0.1.0 稳定 shell/kernel 边界的决策、消费者兼容承诺与实施计划 |
| [shell-api-classification](shell-api-classification.zh.md) | 当前全部导出类型的 0.1.0 shell/tooling/kernel 分类、目标包与移除条件 |
| [shell-api-baseline](shell-api-baseline.zh.md) | shell API 的 Roslyn JSON baseline、CI/release 门禁、批准流程与 previous-package validation 行为 |

---
[↑ 回到 Origo 手册](../README.zh.md)
