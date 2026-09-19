<!-- docsync-pair: architecture/agent-friendly/game-development -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 面向 Agent 的游戏开发产品方向

> [↑ 回到 Agent 友好度研究](README.zh.md)

调查日期：2026-09-18。本页区分已存在的能力、外部研究和产品建议；没有 Origo 游戏生成成功率或市场验证结果。

## 先区分两种用户任务

**维护框架**是修改 Origo 的 C# 接口、生命周期、存档和 Godot 桥接，并通过仓库 CI。**使用框架做游戏**是在消费 Origo 的 Godot.NET 项目中实现玩法、场景、UI、资产与发布。二者共享 API 理解，但交付目标不同：框架测试通过不能证明用户得到可玩的游戏。

Origo 是具有无引擎依赖 Core 和 Godot 适配层的游戏框架，不是完整引擎。它可组织状态、策略、实体、会话和持久化；渲染、编辑器、物理、输入与资源导入仍由 Godot 提供。[架构概览](../overview.zh.md)、[适配层职责](../../Origo.GodotAdapter/README.zh.md)

## 真正的游戏开发评测

| 一手评测 | 已核实内容 | 对 Origo 的限制 |
|---|---|---|
| [GameDevBench 官方仓库](https://github.com/waynchi/gamedevbench)，2026-02 首版论文 | 当前 README 为 333 个 Godot 局部开发任务，覆盖图形、动画、UI 与逻辑；要求精确 Godot 4.4.1。公开 runner 与验证流程，Apache-2.0。[许可证](https://github.com/waynchi/gamedevbench/blob/main/LICENSE) | [论文 v1](https://arxiv.org/html/2602.11103v1) 为 132 题，不能混用版本和成绩。任务不是 Origo C# 套件，也不是完整游戏交付；迁移后须标注为派生评测。 |
| [GameCraft-Bench](https://github.com/FreedomIntelligence/gamecraft-bench)，2026-06-16 论文 | 140 题、15 游戏家族，提交完整 Godot 项目与可回放输入，隐藏 rubric 根据交互证据评分。公开仓库使用 Apache-2.0，当前安装说明固定 Godot 4.6.2、Ubuntu 22.04、Xvfb/xdotool/ffmpeg。[协议与运行](https://github.com/FreedomIntelligence/gamecraft-bench#evaluation-protocol)、[许可证](https://github.com/FreedomIntelligence/gamecraft-bench/blob/main/LICENSE) | [论文](https://arxiv.org/html/2606.17861v1) 与当前仓库排行榜时间不同，不能混用结果。现成环境下载的是普通 Godot 二进制；Origo 需独立固定 .NET 引擎、SDK 与框架版本，不能宣称原套件直接支持。 |
| [JAMER / JamBench](https://arxiv.org/abs/2606.19830)，2026-06-18 首版，06-21 修订 | 论文描述 300 个项目的主题生成/补全评测；Godot 4.x、GDScript 2.0，使用编译、结构完整性和行为对齐指标。[任务协议](https://arxiv.org/html/2606.19830v1) | 范围是游戏代码框架，包含占位资产，不等于完整美术与可玩品质。本次未核实完整可下载发布及统一数据许可证，运行前须补查，不能只据摘要的开放声明承诺可复现。 |

GameDevBench 的模型调用、图像/视频输入与显示环境均产生成本；Linux 官方严格运行要求 Bubblewrap、Xvfb 等。[运行要求](https://github.com/waynchi/gamedevbench#platform-notes) GameCraft-Bench 还包含录屏、回放与多模态裁判成本，官方以 token 计数而非统一美元价格报告；资产有独立许可证。[成本与资产说明](https://github.com/FreedomIntelligence/gamecraft-bench#token-usage) 实际预算须先跑参考解和空提交，再测少量题；无依据的“每题只需几分钱”不可采用。玩法 Agent 的胜率、奖励或导航能力不能代替开发产物验收。

## Origo 已有基础与缺口

已有能力包括 [SND 快速开始](../../usage/quick-start.zh.md) 中的 `HealthInitStrategy`、`SndMetaFluentBuilder`、`ctx.Template.CloneTemplate` 和 `entity.OwningSession.Spawn`，可将“状态定义—策略—模板—运行”串成明确路径。`OrigoDefaultEntry.Bootstrap.cs` 确实先调用 `ConfigureStrategies` 再执行 `sndContext.Bootstrap()`；异常调用 `MarkBootstrapFailed` 后重新抛出。Agent 应沿这条公开启动路径写游戏，不能靠手工拼接底层初始化。[启动契约](../../Origo.GodotAdapter/Bootstrap/README.zh.md)

[策略测试框架](../../usage/strategy-testing.zh.md) 可隔离验证数据、生命周期和保存请求，但不验证真实节点、多个实体与完整 Godot 场景；[集成测试](../../Origo.GodotAdapter.Integration.Tests/README.zh.md) 补充真实引擎行为。控制台 `tree_debug` 提供场景树，`press_button` 直接发射 `Pressed` 信号，`camera_view` 提供投影坐标；后两者不能证明鼠标命中、UI 没遮挡、焦点正确或最终画面合格。[控制台契约](../../Origo.GodotAdapter/Console/README.zh.md)

推断：这些基础适合先面向状态与存档密集的小型游戏建立 Agent 工作流。它们尚未证明 Origo 优于直接 Godot，也没有证明生成项目的趣味性、性能或商业发布质量。真实 GUI 回放与视觉证据应补到游戏验收，不必因此把截图系统塞进 Core。

## 建议路线与可证伪的用户试验

1. **先做可运行入口。** 提供一个消费框架的 Godot.NET starter 和少量完整示例：采集计分、失败/重开、存档恢复。固定合法资产、场景路径、模板 map 和入口配置；让新对话从干净目录完成“构建—启动—真实输入—保存恢复”。这是建议，尚未作为本次调查实现。
2. **缩短任务路径。** 提供按任务选择的配方与契约：加策略、生成实体、绑定 UI、保存/载入。错误应指向失败契约与相关文档。维护者门禁与消费者说明可分别导航，同时保持单一能力入口，避免复制一套底层 API。
3. **建立双层验收。** 用 Core/策略测试验证规则，用 Godot.NET 真实运行、输入回放、截图/视频验证体验；先校准环境，再执行[固定预算 A/B 协议](benchmarks.zh.md)。故障需区分脚本/SDK 主机条件与产品契约，不能把某台机器的 bootstrap 失败当作所有用户失败。
4. **先验证价值再扩张。** 招募 12–20 名 Godot C# 开发者，按经验分层、交叉随机做难度匹配且内容不同的任务。第一试验比较 Origo 当前材料与候选 Agent 套件；第二试验才比较 Origo 与直接 Godot，并分别记录设计与文档差异。固定同一模型、harness、时间和人工救援权限，盲审产物。

直接 Godot 对照组应使用同一 Godot.NET 版本、C#、资产、初始内容和验收规则。提示只描述玩家可观察的需求，例如“采集物增加积分，退出再进入恢复积分”，不能要求调用 SND/Strategy/Session API；Origo 组自行选择公开框架能力，Godot 组可采用其正常场景/节点实现。分别记录框架初始化成本、日常功能实现成本与项目维护成本，避免只选 Origo 现成模块覆盖的题。按目标品类预先分层，同时保留不利于 Origo 的输入/渲染密集任务；由独立评审检查两组 starter 和提示是否泄露答案或提供不对称的预实现。

预注册假设：候选套件把“首次可玩循环”的中位用时降低至少 20%，提高无需人工修补的完成率，且不增加存档/生命周期缺陷。报告失败与置信区间；达不到门槛便否定该版本的价值假设，调整入口或缩小目标品类。再观察一周后的第二次任务完成与自愿继续使用，访谈退出原因；小样本只能证明试点价值，不能宣称市场欢迎。只有上述证据持续成立，再扩展更多游戏类型、资产流程与编辑器工具。


## 长期方向：可观察、可执行、可反馈

长期产品方向建议建立稳定的**观察—行动—反馈契约**：观察同时包含可审计的游戏状态和带时间/场景标识的画面；行动通过已有单一公开能力入口或真实输入执行，明确前置条件与失败原因；反馈包含产物变化、状态断言、输入回放和视觉证据。状态检查与玩家体验互补，不能用发射信号替代真实输入，也不能用截图证明存档完整性。这是待设计能力，不是现有统一工具契约。

优先验证状态与存档密集品类，例如回合策略、经营、叙事分支和具有持久进度的轻量游戏；适配性是基于 Origo 职责的推断，仍须通过任务与用户实验。资产和场景应可追溯到来源、许可证、导入参数、模板/场景映射和当前产物哈希，帮助 Agent 判断错误来自代码、资源还是场景连线。

跨 Agent 工具互操作应共享可验证的输入/输出 schema、错误语义、权限边界与证据格式，允许不同 harness 读取同一项目并复查产物，避免依赖某个供应商的提示习惯。人继续决定审美、趣味、节奏、品类定位和发布取舍；自动化检查只验证可表达的契约与证据。长期成功的判据是开发者反复完成真实任务、愿意保留并维护产物；这条路线不承诺市场接受、流行程度或商业收益。
