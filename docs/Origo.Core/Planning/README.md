# Planning

> [↑ 回到 Origo.Core](../README.md)

规划（Planning）子系统提供意图驱动的实体级计划执行基础设施。

## 接口

### `PlanExecutionStrategyBase : LifecycleStrategyBase`

意图驱动的计划执行基类。封装了从 intent 产生到 plan 拆解、action 挂载/卸载、计划推进的完整生命周期。

**设计理念：** 框架管理 wiring（订阅配对、Action 策略插拔、状态机控制流），用户仅提供两个领域映射函数。任何步骤类型（包括 idle、patrol 等）都应实现为独立的 `LifecycleStrategyBase` Action 策略，通过 `StepToActionIndex` 注册，与框架的其他 action 无差别对待。

#### 用户必须实现的抽象成员

| 成员 | 类型 | 说明 |
|------|------|------|
| `IntentKey` | `abstract string` | 实体 data 键，存储当前意图字符串 |
| `IntentStatusKey` | `abstract string` | 实体 data 键，存储意图执行状态 |
| `PlanStepKey` | `abstract string` | 实体 data 键，存储当前计划步骤类型 |
| `ActionKey` | `abstract string` | 实体 data 键，存储当前动作描述符 |
| `ActionStatusKey` | `abstract string` | 实体 data 键，存储动作执行状态 |
| `ResolveNextStep(intent, currentStep, failed, entity)` | `abstract string?` | 根据意图和当前步骤返回下一步骤，`null`/空终止计划 |
| `StepToActionIndex(stepType)` | `abstract string?` | 步骤类型 → Action 策略索引，`null`/空表示该步骤无需挂载策略 |

#### 用户可覆写的虚成员

| 成员 | 默认值 | 说明 |
|------|--------|------|
| `IntentStatusActive` | `"active"` | 意图激活状态值 |
| `IntentStatusCompleted` | `"completed"` | 意图完成状态值 |
| `ActionStatusExecuting` | `"executing"` | 动作执行中状态值 |
| `ActionStatusCompleted` | `"completed"` | 动作完成状态值 |
| `ActionStatusFailed` | `"failed"` | 动作失败状态值 |

#### Sealed 生命周期钩子（不可覆写）

基类 `sealed` 了 `LifecycleStrategyBase` 的全部 8 个生命周期钩子，自动管理：

- `AfterSpawn` / `AfterAdd`：订阅信号 + 若 intent 已存在则重启计划 → 调用 `OnAfterSpawn` / `OnAfterAdd`
- `AfterLoad`：仅订阅信号（存档恢复时计划状态已持久化，不重启）→ 调用 `OnAfterLoad`
- `BeforeRemove` / `BeforeQuit` / `BeforeDead`：移除当前 Action 策略 → 调用对应的 `OnBefore*` → 取消订阅
- `BeforeSave`：直接委托给 `OnBeforeSave`（保存时无需操作计划状态）
- `Process`：直接调用 `OnProcess`

#### 用户扩展钩子（虚方法）

| 钩子 | 触发时机 |
|------|----------|
| `OnAfterSpawn(entity, ctx)` | AfterSpawn wiring 完成后 |
| `OnAfterLoad(entity, ctx)` | AfterLoad wiring 完成后 |
| `OnAfterAdd(entity, ctx)` | AfterAdd wiring 完成后 |
| `OnBeforeRemove(entity, ctx)` | 移除 Action 后、取消订阅前 |
| `OnBeforeQuit(entity, ctx)` | 移除 Action 后、取消订阅前 |
| `OnBeforeDead(entity, ctx)` | 移除 Action 后、取消订阅前 |
| `OnBeforeSave(entity, ctx)` | BeforeSave 密封钩子中 |
| `OnProcess(entity, delta, ctx)` | Process 帧中 |
| `OnIntentStarted(entity, intent)` | 新意图开始执行时 |
| `OnStepStarted(entity, stepType)` | 新步骤开始执行时 |
| `OnPlanCompleted(entity)` | 计划全部完成时 |
| `OnPlanFailed(entity)` | 计划因失败终止时 |

#### 内置行为

1. **订阅自动管理**：`AfterSpawn/AfterLoad/AfterAdd` 中订阅 `IntentKey` 和 `ActionStatusKey` 的数据变更通知；`BeforeRemove/BeforeQuit/BeforeDead` 中取消订阅。RAII 闭环由基类保证。
2. **计划推进**：intent 变更 → 重新开始计划；action 完成/失败 → 推进到下一步或终止计划。
3. **Action 策略生命周期**：每步自动 `AddStrategy(StepToActionIndex(step))`，推进/终止时自动 `RemoveStrategy`。
4. **计划终止**：所有步骤完成时，清空 intent、plan_step、action，设置 intent_status 为完成。

## 辅助扩展

### `ISndEntity.EnsureReplaceableStrategy(implKey, defaultStrategyIndex)`（扩展方法）

位于 `Origo.Core.Snd.Strategy.EntityStrategyExtensions`。

确保实体的某个 resident 策略已挂载，支持模板级覆写（`*_impl` 模式）：

```csharp
entity.EnsureReplaceableStrategy("character.path_impl", "character.pathfind.astar");
```

- 读取 `implKey` 的当前值作为配置覆盖，未设置时回退到 `defaultStrategyIndex`
- 使用 `implKey` 作为去重标记，重复调用无副作用
- 对应设计模式文档中的「可替换实现模式」

## 设计决策

1. **为什么 `sealed` 生命周期钩子？**
   防止用户覆写生命周期钩子时忘记调用基类导致 wiring 失效。通过 `virtual On*` 钩子提供扩展点。

2. **为什么 `ResolveNextStep` 不含 `ISndContext`？**
   计划分解应是对 entity 状态的纯函数。需要世界查询时通过 `FindByName` + `InvokeStrategy` 完成，不建议在计划推进链中注入 context——这会导致不确定性和测试困难。

3. **为什么单独命名空间 `Origo.Core.Planning`？**
   遵循 `Origo.Core.StateMachine` 的先例。`Planning` 是独立的行为子系统，不混入 `Snd.Strategy` 命名空间。

4. **为什么不在基类中内置 idle / 计时器步骤？**
   idle、patrol、standby 等步骤类型是游戏设计层面的概念，不应进入框架抽象。如果 idle 被内置，则 patrol 同理，框架将无边界膨胀。正确做法：用户将 idle 实现为一个普通的 `LifecycleStrategyBase` Action 策略，通过 `StepToActionIndex("idle")` 映射到对应的策略索引。框架只做调度编排，不做具体行为。

5. **为什么 `AfterLoad` 不重启计划？**
   Action 策略（如 `character.action.nav_to`）作为动态添加策略，通过 `SndEntity.BuildMetaData()` → `SndStrategyManager.GetStrategyIndices()` 参与实体序列化。读档时 `SndEntity.RecoverForLifecycle()` → `SndStrategyManager.RecoverStrategiesOnly()` 完整恢复全部策略。恢复后的 Action 策略在下一帧的 `Process` 中继续执行，计划从断点自然推进，无需 `AfterLoad` 中由 `PlanExecutionStrategyBase` 显式重启。`AfterLoad` 仅需重新建立 `IntentKey` 和 `ActionStatusKey` 的数据订阅连接——这是运行时非持久化资源的 RAII 恢复。

## 文件清单

| 文件 | 职责 |
|------|------|
| `PlanExecutionStrategyBase.cs` | 计划执行基类完整实现 |

---

[↑ 回到 Origo.Core](README.md)
