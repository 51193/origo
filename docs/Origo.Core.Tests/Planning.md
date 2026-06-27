# Planning 测试

> [↑ 回到 Origo.Core.Tests](README.md)

## 验证能力

`PlanExecutionStrategyBase` 的行为：意图驱动的计划执行、Action 策略的自动插拔、计划步骤推进。

## 测试覆盖

### 正确路径

| 测试 | 验证内容 |
|------|---------|
| `AfterSpawn_IntentPresent_StartsPlan` | 实体首次创建时，如果 intent 数据键存在，自动启动计划（写入 PlanStepKey = 第一步） |
| `AfterAdd_IntentPresent_StartsPlan` | 策略被动态添加时，如果 intent 存在，同样启动计划 |
| `ActionCompletion_InSndEntity_AdvancesToNextStep` | Action 完成（ActionStatusKey = "completed"）后，通过数据订阅自动推进到下一步，同时卸载旧 Action 策略并挂载新 Action 策略 |
| `ActionCompletion_LastStep_CompletesPlan` | 最后一步完成后，intent 被清空，intent_status 设为 "completed" |

### 边界/错误路径

| 测试 | 验证内容 |
|------|---------|
| `AfterSpawn_NoIntent_DoesNotStartPlan` | 无 intent 时不启动计划（不写入任何步骤数据） |
| `AfterLoad_IntentPresent_DoesNotRestartPlan` | 从存档恢复时不重置已存在的计划步骤 |
| `StartIntent_ClearsPreviousPlanState` | 启动新 intent 时清除旧步骤/Action 数据 |
| `StepWithoutAction_DoesNotAddStrategy` | `StepToActionIndex` 返回 null 时不会挂载 Action 策略，但仍记录步骤 |
| `BeforeRemove_UnmountsActionStrategy` | 计划策略被移除时，BeforeRemove 清理当前 Action 策略 |
| `DefaultHooks_DoNotMutateEntityData` | 默认钩子实现不修改实体的现有数据 |

## 测试辅助设施

测试使用 `FullMemorySndSceneHost` + `TestFactory.CreateRuntime()` 构建完整的内存中运行时，确保 Action 策略挂载/卸载通过真实 `SndStrategyManager` 执行。

测试策略 `SimplePlanStrategy` 实现 `ResolveNextStep` 和 `StepToActionIndex` 两个抽象方法，返回固定的三部计划（step_a → step_b → 完成）。
