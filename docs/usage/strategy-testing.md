# 策略测试

> [↑ 回到 usage](README.md)
> [↔ 相关测试: 策略测试框架](../Origo.Core.Tests/StrategyTestScenario.md)

## 概述

`StrategyTestScenario` 是 Core 层提供的策略隔离测试框架。无需启动完整运行时，可在单元测试中孤立地测试单个策略的所有生命周期行为。

## EntityStrategy 测试

用于测试继承自 `LifecycleStrategyBase` 的被动策略（有 Process 和生命周期钩子）。

### 三阶段模式

### Phase 1: 配置

```csharp
var harness = StrategyTestScenario
    .For<HealthStrategy>("core.health")  // 策略类型 + 索引
    .WithEntityName("player")
    .WithData("hp", 100)
    .WithData("max_hp", 100)
    .WithSystemConfig("difficulty", "hard")
    .Build();  // 创建 Harness（自动触发 AfterSpawn）
```

配置方法：

| 方法 | 说明 |
|------|------|
| `WithEntityName(string)` | 设置实体名（默认 `__test_entity__`） |
| `WithData<TValue>(key, value)` | 向实体注入初始数据 |
| `WithSystemConfig<TValue>(key, value)` | 向系统黑板写入配置 |
| `WithProgressConfig<TValue>(key, value)` | 向流程黑板写入配置 |
| `WithSessionConfig<TValue>(key, value)` | 向会话黑板写入配置 |
| `WithTemplate(key, SndMetaData)` | 注册模板 |

### Phase 2: 模拟

```csharp
harness.RunFrame();          // 1 帧（Process + flush deferred actions）
harness.RunFrames(60);       // 60 帧
harness.TriggerAfterLoad();  // 手动触发生命周期钩子
harness.TriggerBeforeSave();
harness.TriggerBeforeQuit();
```

钩子触发方法：`TriggerAfterSpawn` / `TriggerAfterLoad` / `TriggerAfterAdd` / `TriggerBeforeRemove` / `TriggerBeforeSave` / `TriggerBeforeQuit` / `TriggerBeforeDead`

### Phase 3: 检查

```csharp
// 数据访问
int hp = harness.GetEntityData<int>("hp");
var (found, lives) = harness.TryGetEntityData<int>("lives");

// 黑板访问
harness.SystemBlackboard.TryGet("difficulty", out string diff);
harness.SessionBlackboard.TryGet("score", out int score);

// 副作用检查
harness.SaveRequests.Count;       // 请求保存次数
harness.LoadRequests.Count;       // 请求加载次数
harness.LevelSwitchRequests;      // 请求关卡切换列表
harness.DeferredActionCount;      // 延迟动作执行数
harness.ConsoleCommands;          // 控制台命令记录
```

## ActiveStrategy 测试

用于测试继承自 `ActiveStrategyBase` 的主动策略（仅 `Invoke` 方法，无生命周期钩子）。

### 配置与执行

```csharp
var harness = StrategyTestScenario
    .ForActive<GenerateFoodKeyStrategy>("food.generate_key")
    .WithData("food.registry", "[]")
    .WithData("food.next_id", 1)
    .WithSessionConfig("seed", 42)
    .Build();

// 调用策略，传入可选参数
var key = harness.Invoke() as string;
// 或带输入参数
var result = harness.Invoke("some_input");

// 检查实体数据变化
var nextId = harness.GetEntityData<int>("food.next_id");
Assert.Equal(2, nextId);

// 通过实体的 InvokeStrategy 方法调用（确保委托链正确）
var key2 = harness.InvokeViaEntity();
```

### 与 EntityStrategy Harness 的差异

| 能力 | `For<T>` Harness | `ForActive<T>` Harness |
|------|-----------------|----------------------|
| `Invoke(object?)` | ❌ 无 | ✅ 核心方法 |
| `InvokeViaEntity()` | ❌ 无 | ✅ 委托验证 |
| `RunFrame` / `RunFrames` | ✅ | ❌ 无（ActiveStrategy 不参与帧更新） |
| `TriggerAfterSpawn` 等钩子 | ✅ | ❌ 无 |
| `FlushDeferredActions()` | （自动在 RunFrame 中） | ✅ 手动调用 |
| 黑板/副作用/模板 | ✅ | ✅ |
| 自动触发 AfterSpawn | ✅（在 Build 中） | ❌（ActiveStrategy 无此钩子） |

### 完整示例

#### 主动调用策略

```csharp
[Test]
public void GenerateFoodKey_Invoke_ReturnsUniqueKey()
{
    var harness = StrategyTestScenario
        .ForActive<GenerateFoodKeyStrategy>("food.generate_key")
        .WithData("food.registry", "[]")
        .WithData("food.next_id", 1)
        .Build();

    var key = harness.Invoke() as string;

    Assert.That(key, Does.StartWith("Food_"));
    Assert.That(harness.GetEntityData<int>("food.next_id"), Is.EqualTo(2));
}
```

#### 带输入参数的策略

```csharp
[Test]
public void DamageCalc_Invoke_AppliesModifier()
{
    var harness = StrategyTestScenario
        .ForActive<DamageCalcStrategy>("combat.damage_calc")
        .WithData("base_damage", 50)
        .Build();

    var result = harness.Invoke(1.5f);  // 传入倍率

    Assert.That(result, Is.EqualTo(75.0f));
}
```

#### 业务延迟动作跟踪

```csharp
[Test]
public void AutoSaveStrategy_EnqueuesSave()
{
    var harness = StrategyTestScenario
        .ForActive<AutoSaveStrategy>("system.auto_save")
        .WithProgressConfig("auto_save_interval", 300)
        .Build();

    harness.Invoke();
    harness.FlushDeferredActions();

    Assert.That(harness.SaveRequests, Has.Count.EqualTo(1));
}
```

#### 模板克隆

```csharp
[Test]
public void TemplateStrategy_ClonesRegisteredTemplate()
{
    var template = new SndMetaData { Name = "base_enemy", ... };

    var harness = StrategyTestScenario
        .ForActive<EnemyFactoryStrategy>("factory.enemy")
        .WithTemplate("enemy_template", template)
        .Build();

    var enemyName = harness.Invoke() as string;

    Assert.That(enemyName, Is.Not.EqualTo("base_enemy"));
}
```

## 完整示例（EntityStrategy）

### 伤害策略测试

```csharp
[Test]
public void DamageStrategy_ReducesHp_EachFrame()
{
    var harness = StrategyTestScenario
        .For<DamageTickStrategy>("core.damage_tick")
        .WithData("hp", 100f)
        .Build();

    harness.RunFrame();

    var hp = harness.GetEntityData<float>("hp");
    Assert.That(hp, Is.LessThan(100f));
}
```

### 存档请求测试

```csharp
[Test]
public void HealthStrategy_RequestsSave_WhenHpReachesZero()
{
    var harness = StrategyTestScenario
        .For<DeathCheckStrategy>("core.death_check")
        .WithData("hp", 5f)
        .Build();

    harness.RunFrames(10);

    Assert.That(harness.SaveRequests.Count, Is.GreaterThan(0));
}
```

## 限制

### 支持的能力

- 实体数据读写 (SetData / GetData / TryGetData)
- 三级黑板访问 (System / Progress / Session)
- EntityStrategy: 所有 8 个生命周期钩子 + Process 帧更新
- ActiveStrategy: Invoke 调用 + 通过实体 InvokeStrategy 的委托验证
- 延迟动作 (BusinessDeferred)
- 持久化请求记录 (Save/Load/LevelSwitch)
- 控制台命令输入输出
- 模板注册

### 不支持的能力

- 实体节点访问 (`GetNode`) — 测试实体不支持节点
- 后台会话创建 — 需要完整 `SndContext` 集成测试
- 多实体交互 — 每个 Harness 对应一个实体
- 引擎类型操作 — 如 `Vector2` 的位置计算（需要注册 Godot 转换器）

## 相关文档

- [SND 实体模型](snd-entity-model.md) — 策略编写
- [Agent Reference](agent-reference.md) — 完整接口签名和测试模式

---
[↑ 回到 usage](README.md)
