<!-- docsync-pair: architecture/agent-friendly/benchmarks -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Agent 友好度的评测依据与实验协议

> [↑ 回到 Agent 友好度研究](README.zh.md)

调查日期：2026-09-18。本页是调查与实验建议；Origo 尚未运行下述实验，不能把预期指标当作已有成绩。

## 已有评测回答什么

本次一手资料检索未找到通用、获得共同认可的“仓库 Agent 友好度认证”。任务成功率同时受模型、harness、预算、任务难度和仓库影响；跨项目排行榜不能单独证明仓库设计更友好。以下工具适合借鉴实验方法，不能直接给 Origo 打分。

| 评测与时间 | 对象、语言与适用边界 | 许可证与运行代价 |
|---|---|---|
| SWE-bench，ICLR 2024；Verified 于 2024-08 发布 | 真实 issue 修复，Verified 500 题；原始任务以 Python 为主。Multilingual 有 300 题、42 仓库、9 种语言，但无 C#、GDScript。可借鉴隐藏测试与回归判定。[数据指南](https://github.com/SWE-bench/SWE-bench/blob/main/docs/guides/datasets.md)、[Multilingual](https://www.swebench.com/multilingual.html) | harness MIT；Multilingual 数据卡标注 MIT，目标仓库仍有自身许可证。Docker 构建、测试和模型调用分别计费；官方建议 x86_64、120GB 空闲磁盘、16GB RAM、8 核。[许可证](https://github.com/SWE-bench/SWE-bench/blob/main/LICENSE)、[数据卡](https://huggingface.co/datasets/SWE-bench/SWE-bench_Multilingual)、[运行说明](https://github.com/SWE-bench/SWE-bench#-usage) |
| Terminal-Bench 2.0，2026-01 论文 | 89 个容器内终端任务，范围超出代码维护。可验证工具使用与环境恢复，但没有统一的 Origo/C# 游戏任务。[论文](https://arxiv.org/abs/2601.11868) | Apache-2.0；Harbor 支持本地 Docker，`harbor run --dataset terminal-bench@2.0 --agent oracle` 可先验证参考解。成本含容器资源、超时和模型调用。[仓库与命令](https://github.com/harbor-framework/terminal-bench-2)、[许可证](https://github.com/harbor-framework/terminal-bench-2/blob/main/LICENSE) |
| RepoBench，ICLR 2024；v1.1 于 2024-02 更新 | Python/Java 的跨文件检索、补全；不评估完整修复、运行游戏或交付质量。适合分析上下文检索，不能替代任务评测。[官方说明](https://github.com/Leolty/repobench) | 仓库 LICENSE 为 CC BY 4.0；底层项目权利另核对。本地 Transformers 推理需要相应算力，数据下载不等于完整运行成本。[许可证](https://github.com/Leolty/repobench/blob/main/LICENSE) |

游戏开发的直接证据见[游戏开发报告](game-development.zh.md)：GameDevBench 可复现局部 Godot 修改；GameCraft-Bench 已有公开仓库，覆盖完整游戏交付；JamBench 论文定义项目生成/补全，但本次未核实可下载的完整发布及统一数据许可证。它们评估开发产物，与让 Agent 玩现成游戏的玩法评测不同。

## AGENTS.md 消融实验的启示

2026-02 的 [Evaluating AGENTS.md](https://arxiv.org/html/2602.11988v1) 对比无上下文、模型生成上下文、开发者上下文。其 AGENTbench 是 12 个 Python 仓库的 138 个任务，并结合 SWE-bench Lite。正文结果中，生成文件在 8 个设置中的 5 个降低成功率，平均调用成本增加约 20%/23%；人工文件通常略有帮助，但也增加成本，Claude Code 是成功率改善的例外。不能简化成“所有 AGENTS.md 有害”，也不能外推为 Origo 当前文件的效果。

推断：Origo 应测试规则的任务相关性与阅读成本，而非追求文件更长。现有门禁仍须遵守；消融须在独立实验副本中预先定义允许改变的说明材料，保留相同安全与生产契约，禁止正式工作绕过规则。

## 最小开跑路径

以下命令来自官方说明，要求先准备对应环境；本次未运行。固定仓库 SHA、Godot/Python/系统依赖后，先验证原始题组参考解，再做 C# 派生评测。把 `<task>` 换成固定提交 `tasks/` 下的实际目录。

**GameDevBench：** 准备精确 Godot 4.4.1、uv、Python 3.10+，Linux 还需官方要求的 Bubblewrap/Xvfb/xauth。参考解入口是 `validate_tasks.py`，不应发明 `--agent oracle`。[官方设置与验证](https://github.com/waynchi/gamedevbench#verify-your-setup)

```bash
git clone https://github.com/waynchi/gamedevbench.git
cd gamedevbench
bash unzip_tasks.sh
uv run python validate_tasks.py
```

验证默认并行检查全部参考解，应预留资源。之后可用官方模型入口 `uv run python gamedevbench/src/benchmark_runner.py --agent AGENT --model MODEL run --task-list tasks.yaml`，替换已安装的 agent 与精确模型 ID。

**GameCraft-Bench：** 先按官方安装章准备 Ubuntu 22.04 的显示/录制依赖、Godot 4.6.2、uv 与 Python 3.12，配置 `.env` 的引擎路径和裁判凭证，以及所选题需要的合法资产。以下验证参考解与空提交；参考解仍可能调用收费裁判。[官方安装及任务命令](https://github.com/FreedomIntelligence/gamecraft-bench#install)

```bash
git clone https://github.com/FreedomIntelligence/gamecraft-bench.git
cd gamecraft-bench
uv venv --python 3.12 .venv
source .venv/bin/activate
uv pip install -e .
cp .env.example .env
# 编辑 .env 并完成资产/系统依赖准备后执行；替换 <task>
./scripts/run.sh -p tasks/<task> --agent oracle
./scripts/run.sh -p tasks/<task> --agent nop
```

**Terminal-Bench 2.0：** 准备 uv 与运行中的 Docker，Harbor 自动下载固定名称的数据集，无需猜测源码仓库的 runner。[官方命令](https://github.com/harbor-framework/terminal-bench-2#getting-started)

```bash
uv tool install harbor
uv run harbor run --dataset terminal-bench@2.0 --agent oracle --n-concurrent 4
```

参考解验收、空提交拒绝、隔离与日志检查完成后，另建 Godot.NET/C# 派生题组，替换引擎、构建与验收接口，并再次验收派生参考解。派生任务使用独立名称与版本，不报告为原始套件成绩。维护隐藏测试和游戏真实输入/视觉验收按下节设计；这些工具尚未提供 Origo runner。

## Origo 的可执行 A/B 协议建议

最初先做 **4 题：2 道维护、2 道游戏；两组各 3 次，共 24 runs**。此阶段只验收 harness、成本、隔离、评分和错误分类，小样本不能宣称显著改善或市场价值。下文每轨 12 题、两组各 5 次即 240 runs，是小试点通过后正式研究的规模建议；样本量还应根据试点方差与需要检测的效果校准。

1. **固定实验清单。** A 为选定基线提交及当前文档，B 只加入候选导航、任务配方或环境修复；代码重构与文档干预分开实验。记录提交 SHA、操作系统镜像、SDK/Godot 版本、模型精确 ID、harness 版本、工具权限、网络范围、reasoning effort、上下文压缩策略。两组共用任务提示，禁止人为提示 B 的正确文件。
2. **分两条任务轨。** maintenance 首批 12 题，覆盖启动失败、策略注册顺序、观察者存档重载等契约；从私有新缺陷或明确标记的故障注入构造，不能把已修复问题称为当前缺陷。game creation 首批 12 题，在消费 Origo 的 Godot.NET 项目中制作、修改小游戏，如模板生成实体、状态驱动 UI、保存后恢复计分。两条轨分别报告，避免框架维护成绩掩盖游戏制作失败。[启动契约](../../Origo.GodotAdapter/Bootstrap/README.zh.md)、[快速开始](../../usage/quick-start.zh.md)
3. **固定预算与重复。** 每题每组至少 5 次独立运行，随机安排顺序，清空对话与运行目录。试点预设 maintenance 30 分钟、game creation 60 分钟，以及相同 token/费用上限；校准后锁定预算，不为某组追加重试。分别公布 pass@1、每次运行分布、配对成功率差及按任务聚类的置信区间；不得只选最好一次。
4. **独立验收。** 公开 smoke tests 支持迭代；隐藏测试、参考补丁和评分规则留在 solver 无法访问的评估环境。维护任务要求目标行为与未修改回归测试通过；游戏任务还要固定分辨率下的真实鼠标/键盘输入、截图或录屏，验证 UI 可读、遮挡、焦点、状态反馈与完整玩法循环。视觉项采用预写 rubric、盲审两名评审；模型裁判先做人类校准，不单独决定通过。[现有 headless 测试](../../Origo.GodotAdapter.Integration.Tests/README.zh.md)
5. **记录成本与失败。** 报告冷启动/暖启动时间、首次有效测试时间、token、缓存 token、真实账单、CPU/GPU 时间及人工救援次数。失败分为环境启动、定位、契约误用、代码/资源构建、逻辑回归、真实输入、视觉呈现、交付遗漏、预算耗尽、验收基础设施故障；保存首个阻塞原因和后续原因。基础设施失败不得悄悄删掉，也不应冒充模型逻辑失败。
6. **防污染与可复查。** solver 禁止读取题库参考解、隐藏测试、历史结果及其他运行目录；评估阶段无模型凭证、无外网。私有题至少保留一份未用于调参的 holdout，按任务家族分割，发布后标记可能污染。保存脱敏 trace、产物哈希、输入回放、评审理由与版本清单。资产单独记录许可证，不把评测代码许可证当资产授权。

现有 `scripts/godot-test.sh` 会检查 SDK 配对、正数测试数量和 ObjectDB 泄漏，提供真实引擎验收基础；它不覆盖完整 GUI 玩法。[适配层控制台](../../Origo.GodotAdapter/Console/README.zh.md) 的 `press_button` 是 `EmitSignal(Pressed)`，不能证明真实点击可达；`camera_view` 输出投影元数据，不能充当截图。建议先完成上述试点，再依据成本与成功率决定是否扩大规模。
