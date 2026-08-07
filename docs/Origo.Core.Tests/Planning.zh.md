<!-- docsync-pair: Origo.Core.Tests/Planning -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Planning 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Planning](../Origo.Core/Planning/README.zh.md)

## 被测行为概览

验证 `PlanExecutionStrategyBase` 的行为：意图驱动的计划执行、Action 策略的自动插拔/卸载、
计划步骤推进（含失败分支）、重复 Wire 不泄露订阅。

所有计划执行测试使用 `FullMemorySndSceneHost` + `TestFactory.CreateRuntime()` 构建完整的内存中运行时，
确保 Action 策略挂载/卸载通过真实 `SndStrategyManager` 执行。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `Planning/PlanExecutionStrategyBaseTests.cs` | 完整计划生命周期：启动/推进/完成/失败、Action 策略挂载/卸载、订阅防泄漏（含回归：连续相同步骤重置 ActionKey 后缀、StartIntent 失败回滚接线可重试、意图缺失时动作完成抛异常并清理） |

## PlanExecutionStrategyBaseTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `AfterSpawn_IntentPresent_StartsPlan` | AfterSpawn 时 intent 数据键存在，自动启动计划（写入 PlanStepKey=第一步） | Planning |
| `AfterAdd_IntentPresent_StartsPlan` | AfterAdd 时 intent 存在，同样启动计划 | Planning |
| `AfterLoad_IntentPresent_DoesNotRestartPlan` | AfterLoad 不重置已存在的计划步骤（存档恢复） | Planning |
| `StartIntent_ClearsPreviousPlanState` | 启动新 intent 时清除旧步骤/Action 数据 | Planning |
| `ActionCompletion_InSndEntity_AdvancesToNextStep` | ActionStatus="completed" 后通过数据订阅推进到下一步，卸载旧 Action 并挂载新 Action | Planning |
| `ActionCompletion_LastStep_CompletesPlan` | 最后一步完成后清除 intent，intent_status="completed" | Planning |
| `StepWithoutAction_DoesNotAddStrategy` | StepToActionIndex 返回 null 时不挂载 Action 策略，但仍记录步骤 | Planning |
| `BeforeRemove_UnmountsActionStrategy` | 计划策略被移除时 BeforeRemove 清理当前 Action 策略 | Planning |
| `ActionFailed_AdvancesPlan_AndTerminates` | Action 失败（ActionStatus="failed"）后调用 OnPlanFailed，计划终止 | Planning |
| `OnPlanCompleted_SuccessPath_FiresHook` | 单步计划完成后 OnPlanCompleted 触发，OnPlanFailed 不触发 | Planning |
| `ResolveNextStep_ReturnsNull_NoPathTerminatesPlan` | action 完成但 ResolveNextStep 返回 null（无可行路径），计划干净终止，触发 OnPlanCompleted | Planning |
| `PushAction_ActionAlreadyMountedInLifecycleIndices_ReusesItInsteadOfThrowing` | 步骤 Action 已在 LifecycleIndices 静态挂载时复用而非重复挂载，不抛异常 | Planning |
| `PushAction_ConsecutiveSameStep_ResetsActionKeyParamSuffix` | 连续相同步骤时 ActionKey 重置为规范形式，不留旧 ",param" 后缀 | Planning |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `AdvancePlan_ActionCompletesWithoutIntent_ThrowsAndCleansUpAction` | intent 被清空后 Action 标记完成 | InvalidOperationException（消息含 "no intent is active"），且卸载 Action、清空计划步骤 |
| `Wire_SecondPlanStrategyOnSameEntity_Throws` | 同一实体上挂载第二个计划策略 | InvalidOperationException（消息含 "plan strategy"） |
| `Wire_StartIntentThrows_RollsBackWiringAndAllowsRetry` | StartIntent 期间 ResolveNextStep 抛异常 | InvalidOperationException 传播，订阅回滚归零，可重试成功 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `AfterSpawn_NoIntent_DoesNotStartPlan` | 无 intent 数据键 | 不写入任何步骤数据 |
| `DefaultHooks_DoNotMutateEntityData` | 默认钩子实现依次调用 | 实体现有数据不变 |
| `Wire_MultipleCycles_ManagesSubscriptionsCorrectly` | AfterSpawn 后再次 AfterAdd | 订阅数保持 1，BeforeRemove 后归零 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `SimplePlanStrategy` | PlanExecutionStrategyBaseTests.cs | 实现 ResolveNextStep/StepToActionIndex：三步计划 test→step_a→step_b→完成 |
| `FakeActionStrategy` | PlanExecutionStrategyBaseTests.cs | 模拟 Action 策略，通过 AfterAdd/BeforeRemove 收集调用事件 |
| `FakeAction2Strategy` | PlanExecutionStrategyBaseTests.cs | 模拟第二个 Action 策略，收集 AfterAdd 事件 |
| `FailingPlanStrategy` | PlanExecutionStrategyBaseTests.cs | 模拟失败计划：step_a 完成后 ResolveNextStep 返回 null 触发 OnPlanFailed |
| `CompletingPlanStrategy` | PlanExecutionStrategyBaseTests.cs | 模拟完成计划：单步计划 complete_test→step_a→完成，覆写 OnPlanCompleted/OnPlanFailed 记录调用 |
| `LoopPlanStrategy` | PlanExecutionStrategyBaseTests.cs | 模拟循环计划：同一步骤 step_a 反复推进，验证重复入栈时 ActionKey 参数后缀复位与已挂载 action 复用 |
| `FlakyStartPlanStrategy` | PlanExecutionStrategyBaseTests.cs | 可配置 ResolveNextStep 抛异常的启动失败计划，验证 StartIntent 失败时接线回滚、实体可重新接线 |
| `SubscriptionTrackingEntity` | PlanExecutionStrategyBaseTests.cs | 包装 StubSndEntity 的 ISndEntity + ISndEntityRawSubscription 实现，记录各键订阅/退订次数，验证 Wire 失败不残留孤儿回调 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
