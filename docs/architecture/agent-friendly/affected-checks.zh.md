<!-- docsync-pair: architecture/agent-friendly/affected-checks -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 受影响检查：缩短反馈回路而不缩小质量合同

> [↑ 回到 Agent Friendly 调查](README.zh.md)

调查日期：2026-09-18；仓库观察基线：`cdba5e4`。本文区分现状、推断和拟议设计；下面的 `check.sh`、规划器与 JSON 合同均未实现，也不改变当前 [AGENTS.md](../../../AGENTS.md) 的完整开发循环。

## 1. 现状与具体成本

**观察**：[test.sh](../../../scripts/test.sh) 每次 restore、Release build 整个 `Origo.sln`，然后执行非 Benchmark 测试。[ci.sh](../../../scripts/ci.sh) 依次执行脚本 lint、format、DocSync、测试、性能基准、Godot 集成；DocSync 后检查 `docs/` 是否已提交，因此它是提交后门禁。开发阶段可以直接调用单项脚本，仓库并非完全没有局部入口；缺少的是统一的“这个改动必须运行哪些项目与设施”的规划器。

`-m:1` 有真实理由：脚本记录 Windows 上并行测试进程触发 xUnit v3 assembly-info 子进程退出竞争的问题。**不能把删除串行参数当作 Agent Friendly 优化**；应先减少不相关项目，再保持当前安全执行方式。核心和适配层的覆盖率排除口径也不同，Godot 原生调用通过独立 headless runner 验证，不能用 xUnit 绿灯代表引擎行为绿灯。见 [Core 测试](../../Origo.Core.Tests/README.zh.md)、[Godot 集成测试](../../Origo.GodotAdapter.Integration.Tests/README.zh.md)。

**推断**：当 Agent 修复 `SavePayloadReader.cs` 时，首先需要快速判断真实 save/load 回归是否成立。反复执行整 solution 会延长每次假设验证；但只跑 `Save` 名称测试，又会遗漏运行时恢复、策略钩子和观察者拓扑。优化目标是按证据减少反馈成本，不承诺某个未实测的加速比例。

## 2. 三层检查的明确合同

| 拟议模式 | 内容 | 结果能够证明什么 |
|---|---|---|
| `quick` | 当前改动所需格式、目标项目编译、指定真实路径回归；检查测试实际执行数量 | 当前假设获得局部反馈，不能证明覆盖率和全链路通过 |
| `affected` | 受影响项目完整非 Benchmark 套件与各自现有覆盖率门槛，附加相关 DocSync、生成器或 Godot 检查 | 显式依赖合同所选择范围通过，仍有选择器漏选风险 |
| `full` | 对应当前提交后 `ci.sh`，随后执行提交消息 lint；CI 的 OS matrix 保持原职责 | 当前仓库规定的最终完成门禁通过 |

项目默认 `CollectCoverage=true`。过滤测试的 `quick` 必须**显式关闭该次局部运行的覆盖率收集**并在结果标记 `coverage: not-measured`，不能降阈值、把子集覆盖率包装成项目覆盖率，或让 quick 成功冒充 CI 成功。`affected` 使用所选项目整套非 Benchmark 测试，保留原有 ≥90% 行覆盖率合同和排除项，不拼接不同测试子集的数据。Microsoft 的 VSTest 文档确认支持项目选择与 `--filter`，同时指出零匹配默认也可能返回成功；因此“测试数量为零即失败”必须是显式门禁。[dotnet test with VSTest](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest)

拟议用法：

```bash
# 拟议命令，仓库当前没有此脚本
bash scripts/check.sh plan --base <commit> --worktree --json
bash scripts/check.sh quick --plan .artifacts/check-plan.json --test <regression>
bash scripts/check.sh affected --plan .artifacts/check-plan.json
# 当前最终入口依然是提交后的 bash scripts/ci.sh
```

## 3. 如何选择：项目图加显式影响合同

第一步读取 Git 的基线至 HEAD、暂存、未暂存和未跟踪文件，包含 rename/delete；记录基线与工作树指纹，执行前发现指纹变化必须重新规划。第二步用 **MSBuild 评估后的项目图**计算反向依赖闭包，保留 `OutputItemType="Analyzer"`、`ReferenceOutputAssembly="false"` 等生成器边，不能只查普通 DLL 引用。MSBuild 的静态图是项目构建依赖的起点，并不声称表达运行时反射、资源路径、测试业务场景等所有影响。[MSBuild static graph](https://github.com/dotnet/msbuild/blob/main/documentation/specs/static-graph.md)

第三步增加版本控制内的显式合同：共享 `Directory.Build.props`、`Directory.Packages.props`、`global.json`、solution、`.editorconfig` 或 CI 主脚本变动扩大为 full；DocSync 配置或实现影响工具测试与文档验证；Godot 场景、资源、`project.godot` 和引擎桥接影响 headless 测试；共享 TestSupport 影响引用它的所有测试项目；生成器影响生成器测试、Core、GodotAdapter 及其下游。`.cs` 增删改名触发镜像文件清单检查，纯实现改动也不能遗漏增删文件的 DocSync 约束。

未知路径、无法评估的项目、缺失合同、空测试选择、生成器诊断、配置不匹配都应失败并说明原因。规划器可以报告 `requiresFull: true`，由用户或既定命令显式运行 full；不能在解析失败后静默选择“没有检查”。不能仅凭目录叫 `Save` 就猜出测试边界，人工合同需经过历史变更回放验证。

| 仓库实际路径示例 | 最小保守选择及理由 |
|---|---|
| `Origo.Core/Save/Storage/SavePayloadReader.cs` | Core.Tests 完整套件；Core 的反向依赖包含 ConsoleBridge、GodotAdapter 等，按图保留下游测试；读档恢复需真实 Godot 集成。初期可以保守选择所有 Core 消费者，测得安全证据后再缩小 |
| `Origo.SourceGeneration/TypedDataGenerator.HomeGeneration.cs` | SG.Tests，加 Core 与 Adapter 消费者编译/测试、TypedData 注册 headless 集成；生成代码没有手写文件 diff 也可能改变公开 API |
| `Origo.GodotAdapter/Bootstrap/OrigoDefaultEntry.Bootstrap.cs` | Adapter.Tests 与 Godot headless；该文件的原生调用在纯 .NET 覆盖率中被排除，必须验证真实 `_Ready`、后续帧与失败启动 |
| `Origo.TestSupport/FileSystem/TestMemoryFileSystem.cs` | 由项目图选中所有引用 TestSupport 的套件，不能只跑文件系统测试 |
| `tools/DocSyncTool/Validator.cs` | DocSyncTool.Tests、仓库 DocSync generate/validate；最终仍需提交并过 full |
| 仅 `docs/usage/agent-reference.*.md` | DocSync generate/validate；示例可编译性检查如果实现则追加，不能把链接合法误认为代码正确 |

初期选择保守而宽的项目集是合理代价。项目很多时再考虑模块级 suite 标签；应避免把成百上千个测试名手写成第二套难维护的依赖系统。

## 4. 可审查的机器输出

以下是拟议规划合同的节选，不是实际运行结果：

```json
{
  "schemaVersion": 1,
  "baseCommit": "cdba5e4",
  "worktreeFingerprint": "<content-hash>",
  "configuration": "Release",
  "changedPaths": ["Origo.GodotAdapter/Bootstrap/OrigoDefaultEntry.Bootstrap.cs"],
  "selectedProjects": ["Origo.GodotAdapter.Tests"],
  "additionalGates": ["godot-headless", "doc-sync"],
  "reasons": [
    {"gate": "godot-headless", "rule": "engine-bound-bootstrap"}
  ],
  "coverage": "project-suite-with-existing-thresholds",
  "requiresFull": false,
  "finalGateRequired": true
}
```

实际完整合同还需记录 SDK/Godot 版本、TFM、图与合同哈希、命令参数数组、执行次数/通过数、退出码、耗时和产物路径。stdout 给机器 JSON，stderr 给诊断；失败应带改动路径、规则和下一动作。显示 `requiresFull: false` 仅说明 affected 规划可执行，`finalGateRequired: true` 明确最终 full 永远需要。shell 不应直接执行来自自然语言或未验证 JSON 的任意命令文本。

## 5. 如何证明选择器没有漏选

建立包含真实回归的历史样本：存档读写、延迟队列、观察者恢复、生成器 Kind 注册、Godot 启动、TestSupport 与全局构建配置。每个样本固定基线与补丁，同时运行 affected 和 full，比较**失败集合**，而非只比较两者是否退出 0。故意破坏上游接口、移除生成输出、修改 `.tscn`、改共享属性，验证相关下游检查必被选中；rename/delete、未跟踪文件、空匹配测试和失效规划也要覆盖。

在足够多真实任务中记录漏选率、无谓选择率、冷/热运行中位数与 P95、restore/build/test/Godot 分项成本。可先以“已知失败样本零漏选，未知输入明确失败，最终 CI 全保留”为验收；这不是所有未来改动零漏选的数学保证。开发反馈节省的时间应超过维护项目图、影响合同与样本的成本，再扩大粒度。

这项能力主要帮助维护 Origo。游戏使用者还需要针对自身策略、存档、场景和游戏验收的局部回路；两者可以共享规划机制，但不能共享一份假定所有游戏结构相同的规则表。

## 6. 建议顺序与边界

先做只读 `plan` 与解释输出；再接项目级 affected；最后才尝试模块级过滤。实施前同步修改 AGENTS/META 的迭代步骤，让 quick/affected 有明确授权而 final full 不变；本文是研究，不能自行替代当前强制 `test.sh`。人工文档继续记录跨模块设计与测试行为，选择器只负责执行证据路由。API 与生成器事实来源参见 [机器 API 清单](api-inventory.zh.md)。
