# Origo Agent 强制工作流与开发准则

> 本文件是本仓库面向 AI Agent 与开发者的**唯一权威入口**。它会随每次会话自动注入。
> 任何对本仓库的**阅读或修改**，都必须遵守本文件，以及本文件指引你阅读的文档（`docs/META.md`、各模块 `docs/.../README.md`）。
> 文档已并入本仓库 `docs/` 目录，不再依赖任何外部文档仓库。

---

## 0. 动手前必读门禁（Gate）

**在阅读或修改本项目任何源码之前，必须先读完以下内容：**

1. **本文件** —— 开发循环、核心原则、文档总索引。
2. **[`docs/META.md`](docs/META.md)** —— 文档维护准则（文档写作规范 + Git 提交消息规范）。
3. **你要改动的模块对应的文档**：`docs/<镜像路径>/README.md`，**以及其上下游 / 相关设施的对应文档**（落实 §1.3 全链路原则）。

`docs/` 是源代码的**结构镜像**：源码 `Origo.Core/Snd/Entity/` 的文档在 `docs/Origo.Core/Snd/Entity/README.md`。
**不要绕过文档直接从头啃源码。** 顺着「本文件 → [`docs/README.md`](docs/README.md) 索引 → 模块 README 逐级下钻」即可获得完整上下文。每篇 README 都含设计原理与「为什么 / 为什么不」的权衡——这些是安全改动的前提，不是可选读物。

---

## 1. 核心原则

### 1.1 Fail-fast（显式失败优先）

- 接口契约被违反时**抛异常**，**禁止静默降级或兜底**。
- 存档读 / 写**严格校验完整性**，宁可显式失败也不接受半初始化状态。
- 不要为了「让它先跑起来」而吞掉异常、塞默认值或加防御性 fallback——这些会掩盖真实错误。

### 1.2 早期开发不背兼容包袱

本项目处于**早期开发阶段**，**不承诺 API 冻结**。

- **禁止**写兼容垫片、弃用层（deprecation shim）、双轨 API、迁移壳等任何为「保持旧用法可用」而存在的代码。
- **禁止**保留演进痕迹：不留废弃代码、注释掉的旧实现、`v0.x 起 / 旧版 / 新增` 之类的历史标记。
- 需要变更时就**做干净的破坏性变更**，把代码与文档改到当前应有的样子，不要为承诺稳定而制造技术债。
- 破坏性变更并非无成本：它仍必须按 §4 记入 `CHANGELOG.md`，并以 `BREAKING:` 前缀标注，让使用者知晓。
- **禁止**为测试便利性而在源码中暴露公共属性、`internal` 属性、或额外方法。框架编写时**应当假设测试不存在**，仅以自身代码质量与安全性为唯一标准——`ISndContext`、`SndContext` 等核心对象不需要"为了方便测试拿到 SessionManager"而保留一个属性。测试应通过 `InternalsVisibleTo`、反射、或自己构造 (`TestFactory` 等) 来接入框架内部状态，而非让框架为其"留门"。

### 1.3 全链路理解 —— 杜绝「假阳性」技术债修复

> **这是本项目最容易被违反、代价最高的一条原则。**

任何对源码的**修改或扩展**，动手前**必须**阅读其**上下游与所有相关设施**的文档与代码，理解模块之间的协作契约。

- **许多模块只有与协作者共同作用时才正确且安全。** 单独看某个类型，可能显得「冗余 / 不安全 / 暴露不足 / 重复」，但那往往是刻意设计。
  - 例：`ISndEntityRawSubscription` 以**显式接口实现**对业务 `ISndEntity` 隐藏，**仅由 per-scene-host 的 `ObserverTopology` 驱动**；观察者绑定经拓扑序列化（`ObserverIndices`）并在读档时自动恢复，而非由业务代码手动重连。脱离这条链去「修」它，会破坏跨模块正确性。
  - 例：`IEntityLifecycle` 的分阶段方法刻意不暴露在面向业务的 `ISndEntity` 上，钩子触发时机由 `SndEntityFactory` / `SessionRun` 统一编排。
- **在理解整条协作链之前，不得把「看似缺陷」的设计当技术债去「修复」。** 这类误判是**假阳性**——改它非但不解决问题，反而引入真正的 bug。
- 怀疑是真缺陷时：先在对应 `docs/.../README.md` 的「设计决策 / 为什么 / 为什么不」中**确认设计意图**；文档未覆盖或无法确认时，**向维护者询问**，不得依据局部猜测擅自改动。
- 同理，§4 的 Changelog 中**不得**把这类「跨模块共同作用的设计」误记为 `Fixed`。

### 1.4 单一访问路径 —— 杜绝「旁路」

> **每种能力只能有一条外部访问路径。旁路是 bug 的温床。**

- 如果某个操作已通过专用接口对外暴露（如 `ISessionRun.RequestKillEntity`），则**任何能达成相同效果的其他路径必须封闭**：将可模拟该接口的对象/方法置为 `internal`，并将其其他能力也封装为各自对应的专用接口。
- **禁止**让调用方自行拼接底层操作来"手工模拟"某个接口的效果——即使表面结果相同，接口封装内有意编排的副作用（校验、钩子、状态转换、资源生命周期管理）会被跳过。这类由旁路造成的缺失极难排查。
- 如果旁路来自一个**本不该具备该能力的对象**（即它既不是该能力的预期提供者，也不是其委托链上的一环），则这极可能是设计缺陷。此情况**必须由维护者介入判断**，Agent 不得自行扫描修复——防止把跨模块协作的刻意设计误判为旁路（参见 §1.3）。

---

## 2. 开发迭代循环（强制顺序）

> **任何变更必须按以下顺序闭环。顺序不可颠倒，步骤不可遗漏。**

| 步骤 | 名称 | 说明 |
|------|------|------|
| 1 | **开发源码** | 在满足 §0 门禁与 §1 原则的前提下实现功能 / 修复 / 重构 |
| 2 | **测试扩展 / 适配** | 为本次变更新增或调整测试：新 public API 写行为测试，bug 修复写回归测试（先红），行为变更同步更新既有测试 |
| 3 | **测试执行** | 运行 `bash scripts/ci.sh`（与 CI 同管线：restore → build → test + 覆盖率门禁） |
| 4 | **修复源码 + 重测试循环** | 测试未全绿则回到源码修复，再次执行步骤 3，**循环直到全部通过**。修复仍须遵守 §1（尤其勿做假阳性修复） |
| 5 | **Changelog 对齐** | 将面向用户的显著变更写入 `CHANGELOG.md` 的 `[Unreleased]` 区块（规范见 §4） |
| 6 | **文档同步** | 同步更新 `docs/` 下镜像结构、接口清单、设计决策、usage / 测试文档（规则见 §5 与 `docs/META.md`） |

**禁止只完成部分步骤。** 若某步确实不适用（如纯内部重构不影响 public API 与文档），必须在提交消息中**显式说明跳过原因**。

---

## 3. 测试要求

| 变更类型 | 测试要求 |
|----------|----------|
| 新增 public API | 必须有对应的行为测试 |
| Bug 修复 | 必须有回归测试（先红后绿） |
| 行为变更 | 更新已有测试以反映新行为 |
| 重构 | 现有测试必须全部通过，无需新增 |

- **运行命令**：`bash scripts/ci.sh`（在仓库根目录）。仅跑测试可用 `bash scripts/run-test.sh`。
- **测试项目**：`Origo.Core.Tests`、`Origo.GodotAdapter.Tests`、`Origo.ConsoleBridge.Tests`、`Origo.SourceGeneration.Tests`。
- **覆盖率门禁**由 Coverlet 在 `ci.sh` 中强制（Core ≥ 90%、ConsoleBridge ≥ 80%、GodotAdapter ≥ 85%、SourceGeneration ≥ 85%）；低于门槛 `dotnet test` 直接失败。
- 测试风格、`InternalsVisibleTo` 白名单原则、静态可变状态隔离等约定见 [`docs/Origo.Core.Tests/META-TEST.md`](docs/Origo.Core.Tests/META-TEST.md)。

---

## 4. Changelog 编写规范

格式基于 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-CN/1.1.0/)，遵循 [语义化版本](https://semver.org/lang/zh-CN/)。文件位置：`CHANGELOG.md`。

### 变动分类

| 类别 | 含义 |
|------|------|
| `Added` | 新添加的功能 |
| `Changed` | 对现有功能的变更 |
| `Deprecated` | 已不建议使用、即将移除的功能 |
| `Removed` | 已经移除的功能 |
| `Fixed` | 对 bug 的修复 |
| `Security` | 对安全性的改进 |

> **破坏性变更不另立分类。** 按性质归入 `Changed`（行为变更）或 `Removed`（API 移除），并在条目开头加 `BREAKING:` 前缀。不使用独立的 `Breaking Changes` 分类。

### 关键约束

1. **基线是上一个正式发布版本的 tag。** 对比上一个正式版本到当前状态的差异，提炼面向用户的显著变更。Nightly tag（如 `v0.0.8-nightly.20260626`）不作为基线。
2. **Nightly 不视为版本。** 带 `-nightly`、`-alpha`、`-preview` 等预发布后缀的包号是快照标识，不是语义化版本。这些变更一律留在 `[Unreleased]`；仅无后缀的正式版本号（如 `0.0.7`）才产生 `## [x.y.z] - YYYY-MM-DD` 区块。
3. **禁止记录版本内的来回变动。** 功能引入又删除、引入后又修复自身引入的 bug——这些噪声不应出现在 changelog 中，只记录最终状态。
4. **禁止记录版本内自身引入问题的修复。** 同一正式版本周期内引入并修复的 bug，既不记引入也不记修复。
5. **面向用户撰写。** 描述行为变化对使用者的影响，而非内部实现细节。
6. **遵守 §1.3。** 不把「跨模块共同作用的既有设计」当 bug 记入 `Fixed`。
7. **日常变更写入 `[Unreleased]`。** Nightly 每日构建，变更累积在 `[Unreleased]`；发正式版本时移入带版本号的区块。

### 编写流程

1. 确定上一个正式版本 tag。
2. 对比该 tag 到当前 HEAD 的全部变更。
3. 按分类归纳面向用户的显著变更，过滤掉版本内来回变动。
4. 写入 `[Unreleased]` 对应分类下。

---

## 5. 文档同步规则

`docs/` 是源代码的结构镜像。以下情况必须在步骤 6 同步更新文档：

| 源代码变更 | 文档操作 |
|------------|----------|
| 新增 / 删除 / 重命名目录 | 在 `docs/` 中镜像相同操作 |
| 新增 public 接口 / 方法 | 更新对应叶子 README 的接口清单 |
| 删除 / 重命名 public 接口 | 更新对应叶子 README，删除旧条目 |
| 设计决策变更 | 更新对应 README 的设计决策章节 |
| 新增配置键 / 命令 | 更新相关 README 和 `docs/usage/` 文档 |
| 模块间依赖关系变化 | 更新模块 README 的链接 |
| 新增测试能力 / 方法 | 更新 `docs/Origo.*.Tests/` 对应能力文档 |

**无需同步**：纯内部实现细节、不改变职责与接口的重构、不改变外部语义的性能优化。

### 同步检查清单

- [ ] 目录结构是否镜像（新增 / 删除 / 重命名）？
- [ ] 叶子 README 的接口 / 文件清单是否准确？
- [ ] 中间层 README 的子模块索引是否完整？
- [ ] 所有链接是否有效（无 404）？
- [ ] 设计决策章节是否反映当前设计意图？
- [ ] `docs/usage/` 与测试文档是否覆盖新增场景 / 能力？

文档写作的层级结构、链接规范、禁止演进标记、提交消息规范等详见 [`docs/META.md`](docs/META.md)。

---

## 6. 发版流程

1. 确定新版本号（正式语义化版本，不可带 `-nightly` 等后缀）。
2. 将 `[Unreleased]` 内容移入 `## [x.y.z] - YYYY-MM-DD` 区块。
3. 清空 `[Unreleased]`。
4. 更新 `Directory.Build.props` 中的 `<Version>`。
5. 提交并打 tag（tag 名 `vx.y.z`）。推 tag 触发 `release` 工作流：发布 NuGet 包，并附带 `docs/` 的文档快照压缩包。

---

## 7. 文档总索引

> 让 agent 无需自行探索即可读到全部相关信息。改 `X/` 下的源码，先读 `docs/X/README.md` 及其上下游。

| 入口 | 路径 | 用途 |
|------|------|------|
| 手册索引 | [`docs/README.md`](docs/README.md) | 全部模块 / usage / 测试文档的顶级导航 |
| 文档维护准则 | [`docs/META.md`](docs/META.md) | 文档写作规范 + Git 提交消息规范 |
| Changelog | [`CHANGELOG.md`](CHANGELOG.md) | 面向用户的变更记录 |
| Core 模块 | [`docs/Origo.Core/README.md`](docs/Origo.Core/README.md) | 平台无关核心：SND 实体、运行时、持久化、状态机等 |
| 源码生成 | [`docs/Origo.SourceGeneration/README.md`](docs/Origo.SourceGeneration/README.md) | TypedData 增量源码生成器 |
| Godot 适配 | [`docs/Origo.GodotAdapter/README.md`](docs/Origo.GodotAdapter/README.md) | Godot 4 适配层 |
| ConsoleBridge | [`docs/Origo.ConsoleBridge/README.md`](docs/Origo.ConsoleBridge/README.md) | TCP 远程控制台桥接 |
| 使用指南 | [`docs/usage/README.md`](docs/usage/README.md) | 从快速入门到深度参考 |
| 测试文档 | [`docs/Origo.Core.Tests/README.md`](docs/Origo.Core.Tests/README.md) | 按能力查看测试覆盖 |
| 性能基线 | [`docs/benchmarks/baseline.md`](docs/benchmarks/baseline.md) | TypedData 性能现状快照与权衡 |
