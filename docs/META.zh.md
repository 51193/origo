<!-- docsync-pair: META -->
<!-- docsync-revision: 22 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 手册维护元指令

> [↑ 回到 Origo 手册](README.zh.md)

> **⚠️ 强制开发循环：任何变更必须按序闭环——① 开发源码 → ② 测试扩展/适配 → ③ 测试执行 → ④ 修复源码+重测试直到通过 → ⑤ Changelog → ⑥ 文档同步 → ⑦ 提交 → ⑧ 提交后 `scripts/ci.sh` → ⑨ 提交后 `scripts/lint-commits.sh`。改动源码前必先阅读其上下游与相关设施的文档，杜绝把跨模块共同作用的设计误判为缺陷。完整规则见 [AGENTS.md](../AGENTS.md)。**

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

### 双语文档机制（Bilingual Documentation Mechanism / DocSyncTool）

`docs/` 使用**同基名 `.zh.md`/`.en.md` 成对**的方式组织多语言文档。常见基名是 `README`，也允许 `Integration.*`、`pipeline.*` 等其他基名；纯导航目录只包含自动生成的 `README.md` 中枢。

| 文件 | 用途 |
|------|------|
| `README.md` | **自动生成**的导航中枢（列出所有语言对与子目录）。**禁止手动编辑。** |
| `<name>.zh.md` | 中文内容文件；`<name>` 常见为 `README` |
| `<name>.en.md` | 英文内容文件；`<name>` 常见为 `README` |

同名不同语言后缀的两个文件组成一个 **sync pair**。同步状态通过每个内容文件头部的元数据追踪：

```markdown
<!-- docsync-pair: Origo.Core/Snd/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
```

| 字段 | 含义 |
|------|------|
| `docsync-pair` | 全局唯一的 pair 标识符（文件路径去语言后缀）。自动推导，跨语言必须一致。 |
| `docsync-revision` | 单调递增整数，**由 DocSyncTool 根据 git 历史自动计算**。**同一 pair 两个文件的 revision 相等 = 同步。** 请勿手工修改。 |

**revision 计算规则**（由 `generate` 自动计算，`validate` 校验）：

| git 变化 | 计算出的 revision |
|----------|-------------------|
| 新建文件 / 新 pair | 从 `1` 开始。为已有 pair 新增翻译文件时，直接追上对侧 revision。 |
| pair 中仅一种语言变化 | 领先侧前进一代；若落后侧变化，则追上对侧（翻译追赶）。 |
| 同一 commit 中两种语言都变化 | 两者一起前进一代。 |
| 仅元数据变化 / 纯重命名 commit | revision 不变（内容哈希排除 DocSync 元数据块）。 |
| 一次 push 多个内容 commit | 每个内容 commit 都计数；最终 CI checkout 不会合并掉中间变更。 |

规划器会剔除 DocSync 元数据块后对文件做内容哈希，在该文件的 git
历史中定位上次生成的内容状态，并重放之后的每个内容变更 commit。因为
GitHub 对一次多 commit 的 push 只会为最后一个 commit 跑 CI，所以 CI
必须使用 `fetch-depth: 0` 拉取完整历史。

**每次文档内容变化后**，必须运行：

```bash
dotnet run --project tools/DocSyncTool -- generate
```

这会重写 revision 头并生成两类派生文件（应一并提交）：

1. **每个目录的 `README.md`** 导航中枢——自动生成的索引，按语言列出所有文档
2. **`docs/.sync-status.json`** ——所有 pair revision 状态的机器可读快照，其中包含作为幂等规划锚点的内容哈希

**DocSyncTool 命令速查**（在仓库根目录执行）：

| 命令 | 作用 |
|------|------|
| `dotnet run --project tools/DocSyncTool -- generate` | 根据 git 历史自动计算 `docsync-revision`，重新生成所有 `README.md` 导航中枢 + `.sync-status.json`。幂等且永远成功。 |
| `dotnet run --project tools/DocSyncTool -- validate` | 只读检查：pair/revision 一致且单调递增（以 `generate` 记录的上次 revision 为下限）、镜像内链接同语言、镜像内跨语言/裸 `.md` 禁止、文件/目录/锚点目标存在、reference-style 定义完整、每个镜像源目录内的 `.cs` 文件都列入该目录的双语 README 文件清单；标题结构差异以警告输出。失败时 exit code 1。 |

**链接规则**（由 `validate` 在 docs 镜像内以 ERROR 级别强制检查）：

- 中文文档（`.zh.md`）只链接到 `.zh.md` 目标
- 英文文档（`.en.md`）只链接到 `.en.md` 目标
- **镜像内跨语言链接禁止**
- 镜像内禁止不带语言后缀的裸 `.md` 链接；跳出镜像链接根目录文件（如 `../AGENTS.md`、`../CHANGELOG.md`）允许

**工具配置**（语言、文档根、源码镜像根与 source→doc 覆盖）定义在 `tools/DocSyncTool/docsync-config.json`：

```json
{
  "Languages": ["zh", "en"],
  "DocsRoot": "docs",
  "SourceMirrorRoots": [
    "Origo.Core",
    "Origo.GodotAdapter",
    "Origo.ConsoleBridge",
    "Origo.SourceGeneration",
    "Origo.TestSupport"
  ],
  "SourceDocOverrides": {
    "Origo.TestSupport/Metadata": "docs/Origo.TestSupport/Architecture",
    "Origo.TestSupport/Runtime": "docs/Origo.TestSupport/Architecture"
  }
}
```

**CI 强制执行**：`scripts/doc-sync.sh`（由 `scripts/ci.sh` 调用）会运行 `generate` 然后 `validate`。`push` 到 main 时，CI 自动提交过时的生成文件；`pull_request` 时，检查到生成文件过时则失败并提示本地运行 `generate`。Validation 失败始终阻断构建。

## 同步规则（Sync Rules）

### 需同步更新的情况

1. **新增/删除/重命名源代码目录** → 在 `docs/` 中相应镜像
2. **新增/重命名/删除 `SourceMirrorRoots` 下任意 `.cs` 文件** → 更新对应镜像 README 的双语文件清单（纯内部文件也必须更新，`validate` 会强制检查）；测试项目/工具的 `.cs` 变更按第 7 条处理。
3. **新增 public 接口/方法** → 更新对应叶子 README 的接口列表
4. **设计决策变更** → 更新设计决策章节
5. **新配置键/命令** → 更新相关 README 和 usage 文档
6. **模块间依赖关系变化** → 更新模块 README 的链接
7. **测试能力/方法变更** → 更新对应 `docs/Origo.*.Tests/` 能力文档
8. **发布或 Changelog 规则变更** → 更新 [release-process.zh.md](release-process.zh.md)（英文对侧为 `release-process.en.md`）
9. **AGENTS.md 元指令变更** → 以 [AGENTS.md](../AGENTS.md) 为冲突时的权威，在同一次变更中同步本文件对应章节；本条不硬编码 AGENTS 章节号；由本文档负责的规则保留完整正文，不用可能漂移的摘要替代。

### 无需同步的情况

- 纯内部实现细节变更（不影响公开 API 或设计意图）——不更新设计说明，但必须按第 2 条更新镜像 README 文件清单
- 代码重构（不改变模块职责和接口）——涉及文件增删改时同样按第 2 条更新文件清单
- 性能优化（不改变外部行为语义）——涉及文件结构时同样按第 2 条处理

### 同步检查清单

在代码 PR 合并后，检查：
- [ ] 目录结构是否镜像（新增/删除/重命名）？
- [ ] 叶子 README 的接口/文件清单是否准确？
- [ ] 中间层 README 的子模块索引是否完整？
- [ ] 所有链接是否有效（无 404）？
- [ ] 设计决策章节是否反映当前设计意图？
- [ ] `docs/usage/` 与测试能力文档是否覆盖新场景/能力？
- [ ] 新增/重命名/删除的 `.cs` 文件是否已列入镜像 README 双语文件清单（含纯内部文件）？
- [ ] 若涉及发布或 Changelog 规则，是否已更新 `release-process.zh/en.md`？

## Git 提交消息格式

所有提交必须遵循 Conventional Commits 规范，保持仓库历史可读、可机器解析。PR 提交消息由 `scripts/lint-commits.sh` 与 `.github/workflows/commit-lint.yml` 强制执行：类型、72 字符标题上限、禁止句尾句号、正文每行不超过 72 字符。Dependabot 自动提交是唯一例外：Dependabot 只能配置提交消息前缀，不支持自定义消息模板，且自动生成的正文行宽超过 72 字符。`.github/dependabot.yml` 为所有生态系统配置 `chore(deps)` 前缀，使生成的标题保持 Conventional Commits；`scripts/lint-commits.sh` 会跳过 Dependabot 作为作者的提交，同一 PR 中人类编写的提交仍会被完整检查。

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
| `build` | 构建系统或外部依赖变更 |
| `ci` | CI 配置或 CI 脚本变更 |
| `style` | 不影响代码含义的格式/风格变更 |
| `revert` | 回退一个之前的提交 |

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
├── README.zh.md / README.en.md  # 顶级索引（手工编写，双语成对）
├── META.zh.md / META.en.md      # 本维护元指令（双语成对）
├── release-process.zh/.en.md    # 正式发布、每周快照与 Changelog 规则（双语成对）
├── .sync-status.json            # 自动生成：所有 pair 的同步状态
├── usage/                       # 系统使用文档（zh/en 成对）
├── adr/                         # 架构决策记录（zh/en 成对）
├── benchmarks/                  # 性能基线（zh/en 成对 + baseline.json）
├── Origo.Core/                  # 镜像仓根 Origo.Core/ 的目录结构
├── Origo.Core.Tests/            # 测试能力文档（按能力分组，zh/en 成对）
├── Origo.GodotAdapter/          # 镜像仓根 Origo.GodotAdapter/
├── Origo.GodotAdapter.Tests/    # GodotAdapter 测试能力文档
├── Origo.GodotAdapter.Integration.Tests/ # Godot headless 集成测试文档
├── Origo.ConsoleBridge/         # 镜像仓根 Origo.ConsoleBridge/
├── Origo.ConsoleBridge.Tests/   # ConsoleBridge 测试能力文档
├── Origo.SourceGeneration/      # 镜像仓根 Origo.SourceGeneration/
├── Origo.SourceGeneration.Tests/ # 源码生成器测试能力文档
├── Origo.TestSupport/           # 测试支撑库文档
└── tools/                       # 仓库工具测试文档（DocSyncTool.Tests）
```

每个手工内容文件都有 `.zh.md` / `.en.md` 双语成对；每个目录的 `README.md` 导航中枢由 `generate` 自动生成。纯导航目录没有语言后缀内容文件。架构决策记录位于 `docs/adr/`；策略顺序等领域词汇见根目录 `CONTEXT.md`。

> 顶层入口 [AGENTS.md](../AGENTS.md) 位于仓库根，自动注入每次会话，并链接到本文件。
>
> 每个 `.zh.md` 内容文件旁都有对应的 `.en.md` 文件，`README.md` 导航中枢自动列出两种语言入口。

## 环境引导（Environment Bootstrap）

运行任何 `dotnet` 命令前，必须按仓库要求配置环境，而不是修改 `global.json` 去迁就本机：

1. `global.json` 是 .NET SDK 功能带的唯一权威来源；禁止降级请求版本，也禁止为迁就已安装 SDK 而修改它。
2. 执行 `bash scripts/install-dotnet.sh`。该脚本解析 `global.json`，通过官方 `dotnet-install.sh` 安装精确版本，并优先使用默认安装根目录（`$HOME/.dotnet`），使正常登录 shell 无需每次会话导出即可解析 `dotnet`。
3. 默认安装根目录只读时，脚本回退到仓库内 `.dotnet/`；此时使用仓库根的被跟踪 `./dotnet` 包装器。包装器在子进程内部设置环境，不向调用方 shell 导出任何变量。
4. 仓库脚本会 source `scripts/dotnet-env.sh`，优先使用 `.dotnet/`，否则回退系统 `dotnet`。禁止用 shell profile 或每次会话的 `PATH` / `DOTNET_ROOT` / `NUGET_PACKAGES` 导出替代安装脚本。
5. Godot 引擎二进制是独立依赖：`scripts/download-godot.sh` 按 `Origo.GodotAdapter/Origo.GodotAdapter.csproj` 中的 `Godot.NET.Sdk` 版本下载并缓存到 `.godot_binary/`。

## 本地 Agent 工作缓冲（Local Agent Work Buffer）

`_origo_local/` 是未被 git 跟踪的单册工作缓冲，用于记录无法立即完成的发现与交接；根 `README.md` 是实时索引与状态持有者，编号章节属于同一工作项。

- **状态**：`inbox`、`in-progress`、`blocked`、`done`、`superseded`；记录 owner/日期/基线提交。
- **Producer**：写入问题证据、官方文档、上下游协作者、相关测试、相关 git 历史、范围/验收标准、已运行命令与未知项；禁止倾倒原始聊天记录。
- **Consumer**：必须先重读完整链路上下文并复核基线假设仍成立，再领取并置为 `in-progress`，补充 owner/branch 或新基线提交；不得删除或改写他人的 `in-progress` 工作。
- **关闭**：仅在 AGENTS 完整开发闭环通过后关闭，记录最终提交与 durable 文档位置；不得删除或标记 `done` 未验证、半成品或未提交的项；受阻时必须留下精确交接：worktree 状态、改动文件、已运行命令、失败信息、下一步动作。
- **约束**：禁止 `git add` 或提交本目录内容；禁止存 secret。stale 内容标记 `superseded` 并指向替代项，不得仅因整洁删除。需要长期存活的结论必须迁入 tracked `docs/`、测试或 `CHANGELOG.md`。
- 缓冲变大时按日期/主题组织，不破坏上述生产者—消费者生命周期。

实时协议见 `_origo_local/README.md`；本节是缓冲区存在时的 tracked 权威规则。

## 手册版本

文档随本仓库 `Directory.Build.props` 中的 `<Version>` 同步——文档与源代码同仓，版本天然一致。正式发布时还必须按 [release-process.zh.md](release-process.zh.md) 更新 `docs/README.zh.md` / `docs/README.en.md` 的版本说明；`scripts/verify-release.sh` 会检查这两个文件是否提到该版本。

## 生成

本手册的**内容文件**（`.zh.md` / `.en.md`）由分析源代码后手工编写。**导航中枢**（`README.md`）和**同步状态文件**（`.sync-status.json`）由 `DocSyncTool generate` 自动生成，禁止手动编辑。质量依赖对源代码的正确理解和维护者的设计知识。如发现偏差，向手册维护者报告。

---
[↑ 回到 Origo 手册](README.zh.md)
