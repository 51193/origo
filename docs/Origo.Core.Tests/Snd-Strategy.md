# SND 策略 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Snd/Strategy](../Origo.Core/Snd/Strategy/README.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.md)

## 被测行为概览

验证 SND 策略系统的全部行为：策略优先级排序、池引用计数/回收、实体策略的 8 个生命周期钩子、
主动策略的 Invoke 调用、策略注册时的类型安全校验。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `StrategyPriorityTests.cs` | 策略按 Priority 升序排列、同优先级按插入顺序、所有生命周期钩子遵循优先级 |
| `StrategyPoolAndRuntimeTests.cs` | 策略池注册/复用/释放/引用计数、SndRuntime 代理到 SceneHost |
| `StrategyPoolTypeSafetyAndExtensionTests.cs` | 策略池类型安全、无状态校验、注册与释放的边缘情况 |
| `LifecycleStrategyBaseTests.cs` | 默认钩子不改变实体数据；Process 中 RequestKill 对自身和其他实体的影响；多策略 Process 中 Kill 自己的后续策略执行验证 |
| `ActiveStrategyTests.cs` | 主动策略 Invoke 调用、实体 InvokeStrategy 委托链 |
| `SndStrategyPerformanceTests.cs` | 策略池 Get/Release 吞吐基准、Process 策略数缩放、TriggerAll ToArray 分配 |

## StrategyPriorityTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Pool_GetPriority_ReturnsExplicitPriorityFromAttribute` | Priority=100 属性正确解析 | snd-entity-model: 优先级 |
| `Pool_GetPriority_ReturnsDefault6205WhenNotSpecified` | 未指定优先级时返回默认值 6205 | snd-entity-model: 优先级 |
| `Add_DifferentPriorities_SortedAscending` | 不同 priority 按升序排列 | snd-entity-model: 策略执行顺序 |
| `Add_SamePriority_MaintainsInsertionFifoOrder` | 同 priority 保持 FIFO 插入序 | snd-entity-model |
| `Add_MixedPriorities_SortedAscWithStableFifoInSamePriority` | 混合优先级排正确，同优先保持插入序 | snd-entity-model |
| `Add_InsertBetweenExisting_PositionsCorrectly` | 中间插入排到正确位置 | snd-entity-model |
| `Process_ExecutesInPriorityAscendingOrder` | Process 按优先级升序执行 | snd-entity-model |
| `Process_SamePriority_ExecutesInInsertionOrder` | 同优先级按插入序执行 | snd-entity-model |
| `Spawn_DifferentPriorities_SortedAscending` | Spawn 时按优先级排序 | snd-entity-model |
| `Spawn_SamePriority_MaintainsInputOrder` | Spawn 同优先级保持输入序 | snd-entity-model |
| `Load_DifferentPriorities_ResortedAscending` | Load 恢复时重排为升序 | snd-entity-model |
| `SerializeIndices_ReturnsIndicesInPriorityOrder` | 序列化索引按优先级排列 | snd-entity-model |
| `SaveLoadRoundtrip_MaintainsProcessingOrder` | 序列化→恢复后 Process 顺序一致 | snd-entity-model |
| `AfterSpawn_ExecutesInPriorityAscendingOrder` | AfterSpawn 钩子按优先级 | snd-entity-model |
| `BeforeQuit_ExecutesInPriorityAscendingOrder` | BeforeQuit 钩子按优先级 | snd-entity-model |
| `AfterLoad_ExecutesInPriorityAscendingOrder` | AfterLoad 钩子按优先级 | snd-entity-model |
| `Remove_Middle_RemainingOrderPreserved` | 删除中间策略，余下顺序不变 | — |
| `Remove_First_RemainingOrderPreserved` | 删除首个策略，余下顺序不变 | — |
| `Remove_Last_RemainingOrderPreserved` | 删除末个策略，余下顺序不变 | — |
| `AddAfterRemove_InsertsAtCorrectPosition` | 删除后新增正确插入 | — |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `EmptyList_ProcessDoesNotThrow` | 空策略列表 Process | 不抛异常 |
| `EmptyList_SerializeIndicesReturnsEmpty` | 空策略列表序列化 | 返回空 |
| `Pool_GetPriority_ReturnsZeroForUnknownIndex` | 未知索引查优先级 | 返回 0 |
| `SingleStrategy_Works` | 单策略 | 正常工作 |
| `NegativePriorities_SortedCorrectly` | 负优先级正确排序 | — |
| `IntMinAndIntMaxPriority_SortedCorrectly` | int.MinValue 和 int.MaxValue 排序 | — |
| `DescendingPriorityInsertion_SortedAscending` | 降序插入自动升序排列 | — |
| `AscendingPriorityInsertion_SortedAscending` | 升序插入保持升序 | — |
| `AlternatingPriorityInsertion_SortedCorrectly` | 交替优先级插入后排升序 | — |
| `Remove_NonexistentStrategy_NoEffect` | 删除不存在的策略不影响 | — |
| `AllDefaultPriority6205_MaintainsInsertionOrder` | 全部默认优先级保持插入序 | snd-entity-model |

## StrategyPoolAndRuntimeTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndStrategyPool_ReusesAndReleasesByReferenceCount` | 引用计数复用：获取两次返回同实例，释放多余次数后重新创建 | snd-entity-model: 引用计数 |
| `SndStrategyManager_Recover_WhenFailed_RollsBackAcquiredStrategies` | 部分策略 Spawn 失败时回滚已获取的策略 | snd-entity-model |
| `SndRuntime_DelegatesToSceneHost` | SndRuntime.SpawnEntity/SpawnMany/RemoveAllEntities 代理到 SceneHost | Snd/Scene |
| `OrigoRuntime_CreatesSndWorldAndSupportsInjectedSystemBlackboard` | OrigoRuntime 正确创建 SndWorld，注入黑板可用 | Runtime |
| `OrigoRuntime_ResetConsoleState_ClearsInputQueueOnly` | 重置控制台只清理输入队列，不清理输出订阅 | Console |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SndStrategyPool_GetUnknownStrategy_ThrowsInvalidOperation` | 获取未注册策略 | InvalidOperationException |
| `SndStrategyPool_ReleaseWithoutAcquire_OrDoubleRelease_ThrowsInvalidOperation` | 未获取或重复释放 | InvalidOperationException |
| `SndRuntime_SpawnEntity_DuplicateName_ThrowsInvalidOperation` | 重复实体名 | InvalidOperationException |

## LifecycleStrategyBaseTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `DefaultHooks_DoNotMutateEntityData` | 默认钩子实现不改变实体数据 | snd-entity-model: 策略生命周期钩子 |

## ActiveStrategyTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| 验证 ActiveStrategy 的 Invoke 调用 | Invoke 正确调用策略方法 | snd-entity-model |
| 验证 InvokeStrategy 委托链 | 实体 InvokeStrategy 正确委托到 ActiveStrategy | snd-entity-model |
| `SameEntity_HasBothTypeStrategies` | 同一实体同时挂 EntityStrategy 和 ActiveStrategy | snd-entity-model |
| `RemoveEntityStrategy_LeavesActiveStrategy` | 移除 EntityStrategy 后 ActiveStrategy 仍可 Invoke | snd-entity-model |
| `RemoveActiveStrategy_LeavesEntityStrategy` | 移除 ActiveStrategy 后 EntityStrategy 仍可 Process | snd-entity-model |

## LifecycleStrategyBaseTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `DefaultHooks_DoNotMutateEntityData` | 默认钩子实现不改变实体数据 | snd-entity-model |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Process_AddsNewStrategy_DoesNotThrow` | Process 中调用 Add 添加新策略 | 不抛异常 |
| `Process_KillsItself_MarksEntity` | Process 中调用 RequestKillEntity | 实体被标记为 pending kill |
| `Remove_NonexistentStrategy_DoesNotThrow` | 移除不存在的策略 | 不抛异常 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `SP50 / SP100 / SP200` | StrategyPriorityTests.cs | 不同 Priority 的策略，Process 中记录执行日志 |
| `SA / SB / SC` | StrategyPriorityTests.cs | 默认优先级的策略，观察 FIFO 插入序 |
| `S10A / S10B / S10C` | StrategyPriorityTests.cs | 同 Priority=10 的多策略 |
| `LC10 / LC20 / LC30` | StrategyPriorityTests.cs | 重写 AfterSpawn 钩子，验证生命周期钩子优先级 |
| `Q10 / Q20 / Q30` | StrategyPriorityTests.cs | 重写 BeforeQuit 钩子 |
| `LD10 / LD20 / LD30` | StrategyPriorityTests.cs | 重写 AfterLoad 钩子 |
| `SN10 / SN5 / SN0` | StrategyPriorityTests.cs | 负优先级策略 |
| `SMin / SMax` | StrategyPriorityTests.cs | int.MinValue / int.MaxValue 优先级策略 |
| `DemoStrategy` | StrategyPoolAndRuntimeTests.cs | 简单标记策略，验证池引用计数 |
| `RecoverSafeStrategy` | StrategyPoolAndRuntimeTests.cs | 验证 Spawn 失败时回滚 |
| `TestEntityStrategy` | LifecycleStrategyBaseTests.cs | 不重写任何钩子的空白策略 |
| `Rec`（AsyncLocal 记录器） | StrategyPriorityTests.cs | 执行顺序日志收集器（使用 AsyncLocal 实现线程隔离） |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|

（无已知缺口）

---

[↑ 回到 Origo.Core.Tests](README.md)
