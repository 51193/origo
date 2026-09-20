<!-- docsync-pair: agents/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Agent skills 配置

> [↑ 回到 Origo 手册](../README.zh.md)

本目录把 Matt Pocock 的工程 skills 接入 Origo。skills 读取下面三个固定路径的配置文件；这些文件是面向工具的英文配置，手册说明仍按双语成对维护。仓库根 [AGENTS.md](../../AGENTS.md) 与 [META](../META.zh.md) 的强制规则始终优先。

| 配置文件 | 职责 |
|----------|------|
| `issue-tracker.md` | `51193/origo` 的 GitHub Issues 与 `.scratch/issues/` 的逐项本地副本；两者按编号对应 |
| `triage-labels.md` | 五种 triage 状态在 GitHub 标签与本地 `Status:` 中的名称 |
| `domain.md` | 单一领域上下文的阅读和记录路径，沿用双语手册及 `docs/architecture/` |

## 设计决策

- GitHub Issue 编号作为共享身份，本地副本便于检索、离线准备和按 skill 流程处理；已发布 issue 的远端状态与本地内容要显式同步。
- 领域术语与架构决策留在现有手册、模块文档和 [架构文档](../architecture/README.zh.md) 中，避免另建 `CONTEXT.md` 或 `docs/adr/`。
- 三个固定路径是 skill 读取的配置入口，双语 README 是手册入口；配置内容变化时同步更新本页与英文对侧，并运行 DocSync。

---
[↑ 回到 Origo 手册](../README.zh.md)
