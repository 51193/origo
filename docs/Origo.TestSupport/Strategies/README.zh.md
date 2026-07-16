<!-- docsync-pair: Origo.TestSupport/Strategies/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# Strategies

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

所有测试项目共享的测试策略基类和策略索引常量。提供帧计数、黑板读写、实体互查、延迟探测等常用测试行为的可复用策略实现。

## 包含文件

| 文件 | 职责 |
|------|------|
| `SharedTestStrategies.cs` | 抽象策略基类：`SharedFrameCounterStrategy`（每帧自增 count 数据）、`SharedBlackboardReaderStrategy`（读黑板值到实体数据）、`SharedBlackboardWriterStrategy`（写实体数据到黑板）、`SharedKillOnProcessStrategy`（首次 Process 时请求 Kill）、`SharedPeerLookupStrategy`（通过 Session.FindByName 互查）、`SharedDeferredProbeStrategy`（通过 Deferred.EnqueueBusinessDeferred 验证延迟动作）、`SharedConsoleCommandStrategy`（订阅控制台输出）。 |
| `TestStrategyIndices.cs` | 所有测试策略索引的静态常量集合（`test.frame_counter`、`test.bb_reader`、`test.bb_writer` 等），带自动重复检测。 |

## 设计决策

### 为什么测试策略使用抽象基类而非接口

测试策略需要在 `Process`、`AfterSpawn` 等钩子中读写实体数据和黑板。抽象基类提供默认空实现让测试仅覆写关注的钩子，简化测试策略编写。策略通过 `StrategyPool` 注册为标准 `LifecycleStrategyBase`。

---

[↑ 回到 TestSupport](../README.zh.md)
