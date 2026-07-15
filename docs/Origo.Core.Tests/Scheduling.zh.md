<!-- docsync-pair: Origo.Core.Tests/Scheduling -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 调度 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Scheduling](../Origo.Core/Scheduling/README.zh.md)

## 被测行为概览

验证 ConcurrentActionQueue 的延迟动作队列：入队/排空（ExecuteAll）、
批次 drain 快照语义（ExecuteAll 中可安全 re-enqueue）、
递归深度保护（max re-entrant drain depth）、并发入队安全、空队列幂等。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `ConcurrentActionQueueTests.cs` | 基础链路：入队/排空/清空/重入/空队列 |
| `ConcurrentActionQueueConcurrencyTests.cs` | 并发安全：多线程入队、递归深度保护、Clear 后排空 |

## ConcurrentActionQueueTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ConcurrentActionQueue_Enqueue_IncreasesCount` | Enqueue 后 Count 递增 | Scheduling |
| `ConcurrentActionQueue_ExecuteAll_RunsAllActions` | ExecuteAll 执行全部入队 action，Count 归零 | Scheduling |
| `ConcurrentActionQueue_ExecuteAll_ActionThatReenqueues` | ExecuteAll 中 re-enqueue 的 action 在同一次调用中执行 | Scheduling |
| `ConcurrentActionQueue_ExecuteAll_PropagatesException` | 单个 action 异常时 propagate | Scheduling |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ConcurrentActionQueue_Enqueue_ThrowsOnNull` | Enqueue(null) | ArgumentNullException |
| `ConcurrentActionQueue_Constructor_ThrowsOnNullLogger` | new ConcurrentActionQueue(null) | ArgumentNullException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `ConcurrentActionQueue_ExecuteAll_EmptyQueue_ReturnsZero` | 空队列 ExecuteAll | 返回 0 |
| `ConcurrentActionQueue_Clear_EmptiesQueue` | Clear 后 Count=0 | Count 归零 |

## ConcurrentActionQueueConcurrencyTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Enqueue_FromManyThreads_ExecuteAllRunsAllActions` | 8 线程 × 50 动作并发入队，ExecuteAll 执行全部 400 个 | Scheduling |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ExecuteAll_WhenActionsKeepReenqueueing_ThrowsAtMaxReentrantDepth` | 无限 re-enqueue | InvalidOperationException（含 "max re-entrant"） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `ExecuteAll_EmptyQueue_IsIdempotent` | 连续 3 次 ExecuteAll | 每次返回 0 |
| `ExecuteAll_AfterClear_DoesNotExecuteClearedActions` | Clear 后 ExecuteAll | 返回 0，action 不被执行 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本能力的测试不定义辅助策略，纯队列行为测试 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 递归深度精确值验证（文档说 max depth = 100） | 深度保护边界偏移 | Scheduling |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
