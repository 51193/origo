<!-- docsync-pair: Origo.Core.Tests/StateMachine -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 状态机 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/StateMachine](../Origo.Core/StateMachine/README.zh.md)
> [↔ 被测行为: usage/state-machine](../usage/state-machine.zh.md)

## 被测行为概览

验证 StackStateMachine 的字符串栈操作：Push/PopRuntime/PopOnQuit 触发对应策略钩子、
Snapshot/RestoreStackWithoutHooks/FlushAfterLoad 两阶段恢复、
StateMachineContainer 的 CreateOrGet/TryGet/序列化往返/批量 Pop 操作、
StateMachineStrategyBase 默认钩子语义、StateMachineStrategyContext 快照、会话 Dispose 触发 PopAllOnQuit。

`RandomAndStateMachineTests.Random.cs` 虽在本测试类的分部文件中，但只包含 RandomNumberGenerator（XorShift128+）的随机数测试，属随机数能力，记录于 [Random.md](Random.zh.md)，本文档不重复收录。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `StateMachineStrategyBaseTests.cs` | 默认钩子不调度动作、Push/Pop/Quit/AfterLoad 钩子触发、容器退出钩子 |
| `StackStateMachineTests.cs` | StackStateMachine 原子操作边界：Push/Pop/Peek/Dispose/Restore 全场景 |
| `RandomAndStateMachineTests.StringStack.cs` | StringStack 核心操作：Snapshot/Restore 往返、Push/Pop 钩子顺序、FlushAfterLoad |
| `RandomAndStateMachineTests.Container.cs` | Container：CreateOrGet/序列化/反序列化/批量 Pop/原子替换 |
| `RandomAndStateMachineTests.SessionAndAdapter.cs` | 会话 Dispose 触发 PopAllOnQuit、StateMachineStrategyContext 快照 |
| `RandomAndStateMachineTests.Random.cs` | RandomNumberGenerator 随机数测试（见 [Random.md](Random.zh.md)，本文档不收录） |
| `RandomAndStateMachineTests.TestStrategies.cs`（辅助文件，含 0 个 `[Fact]`） | 测试辅助策略类定义，见 [测试辅助策略](#测试辅助策略) |

## StateMachineStrategyBaseTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Push_TriggersOnPushRuntime` | Push 触发 OnPushRuntime（AfterTop=入栈值），不触发 OnPushAfterLoad | state-machine: Push |
| `Pop_TriggersOnPopRuntime` | TryPopRuntime 触发 OnPopRuntime（BeforeTop=出栈值） | state-machine: TryPopRuntime |
| `Quit_PopTriggersOnPopBeforeQuit` | TryPopOnQuit 触发 OnPopBeforeQuit | state-machine: TryPopOnQuit |
| `AfterLoad_TriggersOnPushAfterLoad_BottomToTop` | FlushAfterLoad 按 bottom→top 顺序触发 OnPushAfterLoad | state-machine: 读档恢复 |
| `Container_PopAllOnQuit_TriggersPopBeforeQuit_OnAllMachines` | 容器 PopAllOnQuit 对所有机器触发 OnPopBeforeQuit | state-machine: 容器操作 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `DefaultHooks_DoNotScheduleActions` | 全部 4 个默认钩子调用 | EnqueueCount = 0 |

## StackStateMachineTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Push_ValidValue_SetsPeek` | Push("state_a") → Peek 返回 (true, "state_a") | state-machine |
| `Push_MultipleValues_PeekReturnsLast` | Push a→b→c → Peek 返回 c | state-machine |
| `TryPopRuntime_AfterPush_ReturnsTrueAndPopsTop` | Push a→b → TryPop → Peek 返回 a | state-machine |
| `PushPopPush_RoundTrip_PreservesStackState` | Push→Pop→Push→Pop 全往返栈状态正确 | state-machine |
| `RestoreStackWithoutHooks_ThenPeek_ReturnsTop` | Restore {x,y} → Peek 返回 y | state-machine |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Push_NullValue_Throws` | Push(null) | ArgumentException |
| `Push_EmptyString_Throws` | Push("") | ArgumentException |
| `Push_WhitespaceString_Throws` | Push("   ") | ArgumentException |
| `Push_AfterDispose_Throws` | Dispose 后 Push | ObjectDisposedException |
| `Push_WhenPushHookThrows_RollsBackPushedValue` | OnPushRuntime 抛异常 | 原异常传播，栈恢复为 Push 前状态 |
| `TryPopRuntime_AfterDispose_Throws` | Dispose 后 TryPopRuntime | ObjectDisposedException |
| `Peek_AfterDispose_Throws` | Dispose 后 Peek | ObjectDisposedException |
| `RestoreStackWithoutHooks_NullList_Throws` | Restore(null) | ArgumentNullException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TryPopRuntime_EmptyStack_ReturnsFalse` | 空栈 TryPopRuntime | false |
| `TryPopOnQuit_EmptyStack_ReturnsFalse` | 空栈 TryPopOnQuit | false |
| `Peek_EmptyStack_ReturnsNull` | 空栈 Peek | (false, null) |
| `Dispose_IsIdempotent` | 连续两次 Dispose | 不抛异常 |
| `RestoreStackWithoutHooks_EmptyList_ResultsInEmptyStack` | Restore(empty) | Peek = (false, null) |

## StringStack 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `StringStackStateMachine_Snapshot_RestoreStackWithoutHooks_RoundTrip` | Snapshot → Restore 后栈快照一致 | state-machine: 读档恢复（两阶段） |
| `StringStackStateMachine_PushPopRuntime_AfterAddAndBeforeRemove_OrderAndContext` | Push→Pop 触发正确钩子，BeforeTop/AfterTop 上下文与顺序正确 | state-machine: TryPopRuntime |
| `StringStackStateMachine_PushPopOnQuit_AfterAddAndBeforeQuit_OrderAndContext` | PopOnQuit 触发 beforeQuit 钩子，顺序与上下文正确 | state-machine: TryPopOnQuit |
| `StringStackStateMachine_FlushAfterLoad_CallsAfterLoadInPushOrder` | RestoreWithoutHooks → FlushAfterLoad 按入栈顺序重放 afterload | state-machine: 读档恢复 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `StringStackStateMachine_Throws_WhenStrategyNotRegistered` | 使用未注册的策略索引创建状态机 | InvalidOperationException |

## Container 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `StateMachineContainer_PopAllRuntime_InvokesBeforeRemoveTopToBottom` | PopAllRuntime 按 LIFO 触发 runtime 钩子弹空 | state-machine: 容器操作 |
| `StateMachineContainer_PopAllOnQuit_InvokesBeforeQuitTopToBottom` | PopAllOnQuit 按 LIFO 触发 beforeQuit 钩子 | state-machine: 容器操作 |
| `StateMachineContainer_PopAllOnQuit_TraversesMachinesInInsertionOrder` | 多状态机按插入顺序遍历 | state-machine |
| `StateMachineContainer_SerializeDeserialize_RoundTrip` | 序列化→反序列化后状态机栈一致 | state-machine: 序列化格式 |
| `StateMachineContainer_DeserializeWithoutHooks_SwapsAtomically` | 无钩子反序列化原子替换旧状态 | state-machine |
| `StateMachineContainer_CreateOrGet_IdempotentForSameKeyAndIndices` | 同 key+同索引 CreateOrGet 返回同实例 | state-machine |
| `StateMachineContainer_FlushAllAfterLoad_NotifiesPushStrategy` | FlushAllAfterLoad 按入栈顺序重放 afterload | state-machine |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `StateMachineContainer_CreateOrGet_ConflictingIndices_Throws` | 同 key 不同索引的 CreateOrGet | InvalidOperationException |
| `StateMachineContainer_DeserializeFromNode_DuplicateMachineKey_Throws` | 反序列化含重复 key | InvalidOperationException |
| `StateMachineContainer_DeserializeFromNode_ThrowsOnNullNode` | null 节点反序列化 | ArgumentNullException |
| `StateMachineContainer_DeserializeFromNode_ArrayRoot_Throws` | 结构错误的载荷（数组根节点）反序列化 | InvalidOperationException（fail-fast，而非静默清空机器） |
| `StateMachineContainer_DeserializeFromNode_MissingMachinesKey_Throws` | 载荷缺少 machines 键 | InvalidOperationException |
| `StateMachineContainer_Clear_ReleasesAllMachines_WhenOneDisposeThrows` | 其中一台机器释放抛异常 | 异常传播，但其余机器仍释放且容器清空 |

## SessionAndAdapter 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SessionRun_Dispose_InvokesPopAllOnQuit_TopToBottom` | 会话 Dispose 触发容器 PopAllOnQuit，push/beforeQuit 事件序列按 top→bottom 正确 | state-machine: 容器操作 |
| `StateMachineStrategyContext_HoldsMachineKeyAndStackSnapshot` | StateMachineStrategyContext 正确保存 MachineKey、BeforeTop、AfterTop | state-machine: 策略上下文 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `SmPushStrategy` | RandomAndStateMachineTests.TestStrategies.cs | Push 钩子：记录 `push:runtime:before->after` 与 `push:afterload:before->after` 格式事件 |
| `SmPopStrategy` | RandomAndStateMachineTests.TestStrategies.cs | Pop 钩子：记录 `pop:runtime:...` 与 `pop:beforeQuit:...` 事件 |
| `SmPopOrderProbeStrategy` | RandomAndStateMachineTests.TestStrategies.cs | OnPopBeforeQuit 记录 MachineKey，验证多状态机遍历顺序 |
| `SwapTestPushStrategy` | RandomAndStateMachineTests.Container.cs | 空 Push 钩子，用于序列化原子替换测试 |
| `SwapTestPopStrategy` | RandomAndStateMachineTests.Container.cs | 空 Pop 钩子，用于序列化原子替换测试 |
| `SmPushStub` | StackStateMachineTests.cs | 空 StateMachineStrategyBase Push 桩，仅用于驱动栈操作 |
| `SmPopStub` | StackStateMachineTests.cs | 空 StateMachineStrategyBase Pop 桩，仅用于驱动栈操作 |
| `TrackingPushStrategy` | StateMachineStrategyBaseTests.cs | 记录 OnPushRuntime/OnPushAfterLoad 调用次数与 AfterTop |
| `TrackingPopStrategy` | StateMachineStrategyBaseTests.cs | 记录 OnPopRuntime/OnPopBeforeQuit 调用次数与 BeforeTop |
| `TestSmStrategy` | StateMachineStrategyBaseTests.cs | 默认 StateMachineStrategyBase（未覆盖任何钩子），验证默认实现不调度动作 |
| `StubStateMachineContext` | StateMachineStrategyBaseTests.cs | IStateMachineContext 桩，统计 EnqueueBusinessDeferred 调用次数 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 容器在含多个状态机时部分反序列化失败的事务性 | 反序列化中途异常是否保持原状态不变 | state-machine: 序列化格式 |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
