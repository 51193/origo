# Testing

> [↑ 回到 Origo.Core](../README.md) · [↔ Usage: strategy-testing](../../usage/strategy-testing.md)

## 概述

Core 层提供的策略单元测试基础设施。`StrategyTestScenario` 是一个声明式测试构建器，允许在不启动完整运行时的情况下，孤立地测试单个策略（`LifecycleStrategyBase`）的行为。配套提供内存测试上下文和实体。

## 包含文件

| 文件 | 职责 |
|------|------|
| `StrategyTestScenario.cs` | 声明式测试 Builder + Harness：配置 → 构建 → 运行 → 检查 |
| `StrategyTestContext.cs` | 测试用完整 ISndContext 实现 + 辅助测试类型 |

## 类型体系

```
BaseStrategyTestScenarioBuilder (internal)
├── StrategyTestScenarioBuilder<T> (public, T : LifecycleStrategyBase)
└── ActiveStrategyTestScenarioBuilder<T> (public, T : ActiveStrategyBase)

BaseStrategyTestHarness (internal)
├── StrategyTestHarness (public)
└── ActiveStrategyTestHarness (public)
```

两个 Builder 共享配置方法（`WithEntityName`、`WithData`、`WithSystemConfig` 等），差异仅在 `Build()` 中创建策略的类型和初始钩子调用。共享基类消除了约 100 行重复代码。

## 模块详解

### StrategyTestScenario 三阶段模式

**Phase 1: 配置**
```csharp
var harness = StrategyTestScenario
    .For<HealthStrategy>("core.health")
    .WithEntityName("player")
    .WithData("hp", 100)
    .Build();
```

**Phase 2: 模拟**
```csharp
harness.RunFrame();            // 1 帧 Process
harness.RunFrames(10);         // 10 帧
harness.TriggerAfterLoad();    // 手动触发钩子
```

**Phase 3: 检查**
```csharp
int hp = harness.GetEntityData<int>("hp");
harness.SystemBlackboard.TryGet("game_over", out bool over);
```

### StrategyTestContext

测试用 `ISndContext` 实现：
- 三级黑板：System / Progress / Session（均可读写）
- SessionManager：仅前台会话，不支持创建后台会话
- 持久化请求收集：`SaveRequests` / `LoadRequests` / `LevelSwitchRequests`
- 控制台命令收集：`ConsoleCommands` / `ConsoleOutput`
- 延迟队列：`FlushDeferredActionsForCurrentFrame()` 手动排空
- 模板注册：`RegisterTemplate`
- **文件访问**：内置 `MemoryFileSystem` → `IDataSourceIoGateway` → `DataSourceConverterRegistry` 完整链路，支持 `ReadFile` / `WriteFile` / `ReadObject<T>` / `WriteObject<T>` 等操作。所有文件 I/O 在内存中完成，不涉及真实磁盘

### 辅助类型

| 类型 | 说明 |
|------|------|
| `MinimalTestEntity` | 仅支持数据读写的简单实体，不支持节点/策略 |
| `TestSessionManager` | 单前台会话的管理器 |
| `TestSessionRun` | 只含黑板 + 测试 SceneHost |
| `TestSceneHost` | 可通过 Spawn 添加简单实体的宿主 |

## 设计决策

### 为什么测试框架放在 Core 而非 Tests 项目

`StrategyTestScenario` 导出为 `public` API，允许外部项目（或 future plugin 项目）编写策略测试。将测试工具放在 Core 使其与其他 Core 类型一同打包分发。

### 为什么不支持后台会话创建

`StrategyTestScenario` 用于孤立测试单个策略。多会话交互需要完整的 `SndContext` 集成测试。将两者边界明确化避免在单策略测试中引入不必要的复杂度。

### 为什么 Harness.RunFrame 同时执行 Process 和 flush deferred

单帧的完整语义是"策略逻辑 + 策略产生的延迟动作"。分离未 flush 的帧会在测试中断言时产生令人困惑的状态不一致。

---
[↑ 回到 Origo.Core](../README.md)
