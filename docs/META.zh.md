<!-- docsync-pair: docs/META -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 手册维护元指令

> [↑ 回到 Origo 手册](README.zh.md)

> **⚠️ 强制开发循环：任何变更必须按序闭环——① 开发源码 → ② 测试扩展/适配 → ③ 测试执行 → ④ 修复源码+重测试直到通过 → ⑤ Changelog → ⑥ 文档同步。改动源码前必先阅读其上下游与相关设施的文档，杜绝把跨模块共同作用的设计误判为缺陷。完整规则见 [AGENTS.md](../AGENTS.md)。**

## 手册定位

`docs/` 是 Origo 框架的文档镜像，随源代码同仓维护。目标是：**阅读根目录 → 找到目标文件夹 → 进入继续阅读 → 递归下降，避免从源代码从头读起**。

## 编写原则

### 自底向上

1. **叶子层**（最深目录）：描述文件清单 + 功能概述 + 设计决策（为什么做/为什么不）
2. **中间层**（有子目录）：汇总所有子模块能力，忽略细节，描述模块对外的整体价值
3. **模块根**：子系统一览 + 模块职责 + 架构约束
4. **项目根**：顶级索引，所有子模块入口

### 链接规范

- **每个 README 必须包含向上一层（父目录）的链接**，格式：`` `[↑ Back to Xxx](path)` ``
- **每个 README 必须包含所有子模块的链接**（如果有子目录）
- **横向关联可选**（如实现 ↔ 抽象），格式：`` `[↔ Xxx](path)` ``
- **禁止孤立叶子**：整个文档树通过链接严格连通

### 内容约定

| 层级 | 内容 |
|------|------|
| 叶子目录 | 包含文件列表 + 功能概述 + 设计决策（为什么做/为什么不） |
| 中间目录 | 子模块能力摘要 + 本层直接文件说明 |
| 模块根 | 子系统一览 + 模块架构约束 |
| 顶级 | 所有模块入口索引 + 手册使用指南 |

### 写作风格

- 每个 README 开头标注当前层级的父链接（↑）
- 叶子层 README 结尾可再次标注向父链接（便于返回导航）
- 表格清晰列出文件职责和接口成员
- 设计决策使用"为什么"和"为什么不"分点阐述
- **不确定的设计决策必须询问维护者，不得编造**
- **禁止演进标记**：文档是现状快照，不得出现"新增"、"旧版"、"已废弃"、"v0.x 起"等标记代码/接口版本演进历史的字样。任何接口/方法/决策的描述应直接陈述其当前职责和理由，不暗示其是否"曾经不存在"或"未来可能删除"。

### 双语文档机制（DocSyncTool）

`docs/` 使用**同目录语言后缀**的方式组织多语言文档。每个目录下：

| 文件 | 用途 |
|------|------|
| `README.md` | **自动生成**的导航中枢（列出所有 `.zh.md` / `.en.md` 文件）。**禁止手动编辑。** |
| `README.zh.md` | 中文内容 |
| `README.en.md` | 英文内容 |

同名不同语言后缀的两个文件组成一个 **sync pair**。同步状态通过每个内容文件头部的元数据追踪：

```markdown
<!-- docsync-pair: docs/Origo.Core/Snd/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
```

| 字段 | 含义 |
|------|------|
| `docsync-pair` | 全局唯一的 pair 标识符（文件路径去语言后缀）。自动推导，跨语言必须一致。 |
| `docsync-revision` | 单调递增整数。**同一 pair 两个文件的 revision 相等 = 同步。** 尾部注释为强制提醒——CI 会校验其存在。 |

**revision 更新规则**（由开发者手动操作，CI 校验）：

| 操作 | 做法 |
|------|------|
| 修改 `.zh.md` 内容 | **递增**该文件的 `docsync-revision`。`.en.md` 即刻 stale。 |
| 将 `.zh.md` 的改动翻译到 `.en.md` | **设置** `.en.md` 的 `docsync-revision` 等于 `.zh.md` |
| 在 `.en.md` 中新增原创内容（非翻译） | **设置** `.en.md` 的 `docsync-revision` 为 `max(zh.rev, old_en.rev) + 1`。`.zh.md` 即刻 stale。 |
| 新建文档文件 | 初始 `docsync-revision: 1` |

**每次文档内容或 revision 变更后**，必须运行：

```bash
dotnet run --project tools/DocSyncTool -- generate
```

这会生成两类派生文件（应一并提交）：

1. **每个目录的 `README.md`** 导航中枢——自动生成的索引，按语言列出所有文档
2. **`docs/.sync-status.json`** ——所有 pair revision 状态的机器可读快照

**DocSyncTool 命令速查**（在仓库根目录执行）：

| 命令 | 作用 |
|------|------|
| `dotnet run --project tools/DocSyncTool -- generate` | 重新生成所有 `README.md` 导航中枢 + `.sync-status.json`。永远成功。 |
| `dotnet run --project tools/DocSyncTool -- validate` | 只读检查：所有 pair 的 revision 一致、所有链接指向同语言文件、无断裂链接。失败时 exit code 1。 |
| `dotnet run --project tools/DocSyncTool -- init` | **一次性迁移** —— 重命名 `.md` → `.zh.md`，注入元数据，更新链接。已执行完毕，切勿重复运行。 |

**链接规则**（由 `validate` 以 ERROR 级别强制检查）：

- 中文文档（`.zh.md`）只链接到 `.zh.md` 目标
- 英文文档（`.en.md`）只链接到 `.en.md` 目标
- **跨语言链接禁止**
- 不带语言后缀的裸 `.md` 链接禁止（迁移后）

**配置的语言**定义在 `tools/DocSyncTool/docsync-config.json`：

```json
{ "languages": ["zh", "en"], "docs_root": "docs" }
```

**CI 强制执行**：`scripts/doc-sync.sh`（由 `scripts/ci.sh` 调用）会运行 `generate` 然后 `validate`。`push` 到 main 时，CI 自动提交过时的生成文件；`pull_request` 时，检查到生成文件过时则失败并提示本地运行 `generate`。Validation 失败始终阻断构建。

## 同步规则

### 需同步更新的情况

1. **新增/删除/重命名源代码目录** → 在 `docs/` 中相应镜像
2. **新增 public 接口/方法** → 更新对应叶子 README 的接口列表
3. **设计决策变更** → 更新设计决策章节
4. **新配置键/命令** → 更新相关 README 和 usage 文档
5. **模块间依赖关系变化** → 更新模块 README 的链接
6. **AGENTS.md 元指令变更** → 在本文档中同步引用新规则（如 AGENTS.md §1.7 注释语言要求）

### 无需同步的情况

- 纯内部实现细节变更（不影响公开 API 或设计意图）
- 代码重构（不改变模块职责和接口）
- 性能优化（不改变外部行为语义）

### 同步检查清单

在代码 PR 合并后，检查：
- [ ] 目录结构是否镜像（新增/删除/重命名）？
- [ ] 叶子 README 的接口/文件清单是否准确？
- [ ] 中间层 README 的子模块索引是否完整？
- [ ] 所有链接是否有效（无 404）？
- [ ] 设计决策章节是否反映当前设计意图？

## Git 提交消息格式

所有提交必须遵循 Conventional Commits 规范，保持仓库历史可读、可机器解析。

### 基本格式

```
type: 简述

详细段落，说明变更的**内容**和**原因**，而非实现细节（代码 diff 已经展示了"怎么做"）。

多行正文每行不超过 72 字符，段落之间空一行。
当变更涉及多个子项目时，使用分组标题。
```

### 类型（type）

| 类型 | 用途 |
|------|------|
| `feat` | 新功能（面向用户或下游库消费者） |
| `fix` | 缺陷修复 |
| `refactor` | 不改变外部行为的代码重构 |
| `perf` | 性能优化 |
| `docs` | 仅文档变更 |
| `test` | 仅测试新增或修改 |
| `chore` | 构建、依赖、版本号等维护性变更 |

### 简述规则

- 使用英文祈使句（如 `add`, `fix`, `remove`, `extract`），首字母小写
- 一行完成，不超过 72 个字符
- 不加句号结尾
- 描述面向外部行为，而非内部细节

### 正文规则（多段时必填，单行修复可选）

- 说明**为什么要做**这个变更（如设计缺陷、技术债、新需求）
- 说明**对使用者的影响**（API 变更、行为变更、破坏性变更）
- 破坏性变更必须在正文末尾添加 `BREAKING CHANGE:` 前缀段落
- 关联的 issue 或 PR 编号放最后一行（`Closes #xxx` / `Refs #xxx`）

### 示例

```
feat: add Vector3 support to TypedData inline storage

Register Vector3, Vector3I, and Vector4 as GodotAdapter inline types
with startKind=128. The TypedData source generator now emits TryGetXxx
and AsXxx extension methods for all registered adapter types.

Closes #42
```

```
refactor: extract SaveCoordinator from ProgressRun nested class

SaveCoordinator held references to ProgressRun internals via _owner,
preventing isolated testing. Extracting it with explicit constructor
injection makes save orchestration independently testable and clarifies
the ProgressRun persistence boundary.

BREAKING CHANGE: SaveCoordinator constructor now requires IStateMachineContainer
instead of accessing ProgressScope.StateMachines through the owner reference.
```

```
fix: prevent partial session state after failed load recovery

ResetAfterLoadFailure used a single try-catch that swallowed all
exceptions, leaving the session in an inconsistent state. Split into
per-step try-finally blocks with aggregate rethrow to ensure each
cleanup step executes independently and failures are surfaced.
```

```
chore: bump Origo to 0.0.7-nightly.20260608
```

### 禁止的做法

- ❌ 无类型前缀的提交消息
- ❌ 空提交消息
- ❌ 仅写 `update`、`fix bug`、`wip` 等无信息量消息
- ❌ 在提交消息中写实现细节（"改用 X 类"、"把参数从 A 改成 B"）——这些是 diff 的内容
- ❌ 描述不在本次提交范围内的计划或意图
- ❌ 使用内部代号或优先级标记（如 `P0`、`P1`、`Phase 1` 等）——提交消息面向的是无前置知识的读者，应直接描述变更内容而非开发过程中的内部分类名称
- ❌ Squash merge 时保留中间开发的阶段性提交消息（应重新撰写面向功能的消息）

## 目录结构约定

```
docs/                            # 文档根（位于 origo 仓库内）
├── README.md                    # 自动生成：双语导航中枢
├── README.zh.md                 # 中文顶级索引（手工编写）
├── META.zh.md                   # 本文件（维护元指令）
├── .sync-status.json            # 自动生成：所有 pair 的同步状态
├── usage/                       # 系统使用文档
│   ├── README.md               # 自动生成：导航中枢
│   ├── README.zh.md            # 使用文档索引（手工编写）
│   └── *.zh.md                 # 按使用场景组织的文档（手工编写）
├── benchmarks/                  # 性能基线（TypedData 现状快照）
│   ├── README.md               # 自动生成
│   ├── README.zh.md            # 手工编写
│   └── baseline.zh.md
├── Origo.Core/                  # 镜像仓根 Origo.Core/ 的目录结构
│   ├── README.md               # 自动生成：导航中枢
│   ├── README.zh.md            # 模块根文档（手工编写）
│   └── 子目录/                  # 每个子目录内：README.md(自动) + README.zh.md(手工)
├── Origo.Core.Tests/            # 镜像仓根 Origo.Core.Tests/
├── Origo.GodotAdapter/          # 镜像仓根 Origo.GodotAdapter/
├── Origo.GodotAdapter.Tests/    # 镜像仓根 Origo.GodotAdapter.Tests/
├── Origo.ConsoleBridge/         # 镜像仓根 Origo.ConsoleBridge/
├── Origo.ConsoleBridge.Tests/   # 镜像仓根 Origo.ConsoleBridge.Tests/
├── Origo.SourceGeneration/      # 镜像仓根 Origo.SourceGeneration/
└── Origo.SourceGeneration.Tests/ # 镜像仓根 Origo.SourceGeneration.Tests/
```

> 顶层入口 [AGENTS.md](../AGENTS.md) 位于仓库根，自动注入每次会话，并链接到本文件。
>
> **英文文档启用后**，每个 `.zh.md` 旁会出现对应的 `.en.md` 文件，`README.md` 导航中枢会自动列出两种语言的入口。

## 手册版本

文档随本仓库 `Directory.Build.props` 中的 `<Version>` 同步——文档与源代码同仓，版本天然一致。

## 生成

本手册的**内容文件**（`.zh.md` / `.en.md`）由分析源代码后手工编写。**导航中枢**（`README.md`）和**同步状态文件**（`.sync-status.json`）由 `DocSyncTool generate` 自动生成，禁止手动编辑。质量依赖对源代码的正确理解和维护者的设计知识。如发现偏差，向手册维护者报告。

---
[↑ 回到 Origo 手册](README.zh.md)
