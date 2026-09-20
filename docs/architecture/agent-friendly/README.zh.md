<!-- docsync-pair: architecture/agent-friendly/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Origo Agent Friendly 调查报告

> [↑ 回到架构文档](../README.zh.md)

## 结论与范围

本报告区分两个目标：Agent 能否可靠维护 Origo，以及游戏开发者能否让 Agent 使用 Origo 实现、运行并验证游戏。后者是本次产品判断的主要目标。Origo 是带 Godot 适配层的平台无关 C# 游戏框架；渲染、物理和编辑器仍由 Godot 提供，不能把框架易用性等同于完整引擎能力。

Agent Friendly 的价值应体现为更高的任务完成率、更少的人类介入和更短的玩法验证时间。文档字数、skills 数量、MCP 工具数量和覆盖率不能独立证明这种价值。前次讨论的 7.5/10 是主观工程审计判断，不是 benchmark 分数；本报告不发布未经实验的友好度排名。

报告日期：2026-09-18。Origo 观察基线：`cdba5e4`。实际实验必须固定外部 commit、数据集版本、模型和 harness。各专题是调查与候选方案，不改变当前 AGENTS 门禁、公开 API 或 CI 规则。

## 阅读地图

| 用户问题 | 专题 | 应获得的答案 |
|---|---|---|
| 有什么现成 benchmark？ | [Benchmark 调查与实验协议](benchmarks.zh.md) | 可复用评测、语言和版本限制，以及衡量 Origo 改进的受控实验 |
| 怎样学习 OpenAI，具体差在哪里？ | [OpenAI Harness 案例对照](openai-harness.zh.md) | 一手案例的证据边界、Origo 实际任务差距与借鉴方式 |
| 如何精简根指令？ | [根指令与上下文路由](root-instructions.zh.md) | 常驻约束、按需阅读、预算与验收 |
| 如何引入按需 skills？ | [按需工作流设计](skills.zh.md) | 触发条件、输入输出契约、脚本边界与维护成本 |
| 如何做 affected checks？ | [按影响范围验证](affected-checks.zh.md) | 依赖闭包、快速反馈、漏选检测与最终全量门禁 |
| 如何生成 API 清单？ | [机器 API 库存](api-inventory.zh.md) | 有效编译面、生成代码、查询及人工设计文档分工 |
| 是否符合 AI 开发游戏的目标？ | [游戏开发产品价值与路线](game-development.zh.md) | 创作闭环、产品假设、竞争边界与可证伪验证 |

推荐先读游戏产品报告和 benchmark，再读 OpenAI 对照；最后按失败类别选择工程专题。每份专题独立覆盖现状、方案、风险和验收。

## 两条路径不能混为一个分数

| 路径 | 代表任务 | 完成证据 | 四项方案的贡献 |
|---|---|---|---|
| 维护框架 | 修改存档管线、补策略排序回归、扩展生成器 | 真路径回归、跨模块契约、完整 CI | 根指令、skills、affected checks 直接改善维护 |
| 使用框架做游戏 | 拾取、伤害、存档恢复和可玩的循环 | 游戏启动、真实输入、画面、状态断言、人工玩法评价 | 消费者路由、策略模板和 API 库存直接有用；框架 CI 加速影响较间接 |

游戏项目开发者不应完整执行 Origo 仓库维护流程。消费者入口应解释安装版本、策略和实体数据、宿主启动、游戏项目自己的测试与运行工具。维护入口继续承担历史、跨模块阅读、DocSync、发布和架构门禁。

## 已核对的事实与修正

- [AGENTS.md](../../../AGENTS.md) 的单一入口、完整链路阅读与验证闭环是现有优势。守卫独立测得 223 行、16,364 字节；16 KiB 是 Origo 自定预算，剩余 20 字节不是模型上限，也不是友好度指标。
- `scripts/test.sh` 测试整个 solution；`-m:1` 为 Windows xUnit v3 discovery race 而设。提效应从范围选择着手，不能未经核实移除串行约束。
- [Snd 角色文档](../../Origo.Core.Contracts/Abstractions/Snd/README.zh.md) 明确 9 个 Snd 角色加 `IStateMachineContext`，合计 10 个 companion。能力清单的简写易歧义，不能当作已确认的实现缺陷。
- [TCP Bridge](../../Origo.ConsoleBridge/README.zh.md) 端口已可配置；单连接和 loopback 有明确理由。并行任务需隔离实例与状态，不应直接多客户端写入同一会话。
- [Godot 控制台](../../Origo.GodotAdapter/Console/README.zh.md) 的 `press_button` 发射按钮信号，`camera_view` 输出投影信息；分别不能证明真实输入可达性和最终画面正确。
- [真实 Godot 集成测试](../../Origo.GodotAdapter.Integration.Tests/README.zh.md) 已验证宿主契约，不能替代用户游戏的视觉、交互与玩法验收。

## 学习 OpenAI 的边界

OpenAI 将仓库知识、可执行约束和可观察运行环境组合成 Agent 基础设施。公开案例是实践报告，不是游戏框架的市场实验。Origo 可学习机制并用失败轨迹验证，不能推导“相同目录结构就会更受欢迎”。[OpenAI Harness engineering](https://openai.com/index/harness-engineering/)

四项方案是候选干预，不必同时完成。精简可能丢约束，skills 可能漏触发，affected checks 可能漏验证，API 库存可能增噪声；分别测量收益和回归，保留 fail-fast、单一访问路径与最终完整 CI。

## 建议顺序与停止条件

| 顺序 | 交付结果 | 验证与停止条件 |
|---|---|---|
| 1 | 固定 C# / Godot 版本的样板游戏与少量隐藏验收任务 | 样板或验收器不稳定时先修复，不运行大规模评测 |
| 2 | 基线轨迹，按上下文、API、启动、输入、视觉、验证分类 | 不把所有模型失败归因于框架，识别高频可干预原因 |
| 3 | 分开维护者/消费者路由，为单个玩法提供按需流程 | 完成率或规则遵守下降时修路由，不能只看 tokens |
| 4 | 根据失败证据选择 affected checks 或最小 API 库存 | 只做消费者实际查询的符号，快速检查不宣称完整门禁通过 |
| 5 | 隔离启动、真实输入、截图/视频、状态观察和复现工件 | 先固定时序、资源导入与环境，再判断视觉失败 |
| 6 | 对照相同 Godot 基础上的直接开发方式做用户试验 | 净学习成本更高、无完成率收益或无留存时调整定位 |

长期方向是可被 Agent 理解和操作的游戏系统：玩法规格到策略/资源，运行观察到确定性复现，需求修改到可靠验收。扩大投入应由真实开发者留存、可玩游戏产出和人工修复成本决定。

## 非目标

本次不实现 benchmark runner、skills、affected selector、API generator 或游戏操作服务；不把拟议命令当作现有 CLI；不改变公开接口或发布版本。后续实现需独立完成 AGENTS 开发闭环。

行为/API 测试扩展、用户功能 Changelog 不适用于纯研究文档；双语同步、导航、生成物、提交后 CI 与消息检查仍适用。来源许可与可复现性应按各专题版本复核。

---
[↑ 回到架构文档](../README.zh.md)
