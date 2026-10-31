# Strategy Testing

> [↑ Back to usage](README.en.md)
> [↔ Related Tests: Strategy Testing Framework](../Origo.Core.Tests/StrategyTestScenario.en.md)

## Overview

`StrategyTestScenario` is a repository test-support facility for strategy isolation testing (`Origo.Core.Tests/TestSupport`). It is not part of the published shell packages, so external package consumers should test their strategies through their own test harness or the package-consumer integration path described in [Shell Package Consumer](shell-package-consumer.en.md). Within this repository, it tests all lifecycle behaviors of a single strategy without starting a full runtime.

## EntityStrategy Testing

Used for testing passive strategies that inherit from `LifecycleStrategyBase` (with Process and lifecycle hooks).

Each scenario runs in three phases: configure, simulate, and inspect.

### Phase 1: Configure

```csharp
var harness = StrategyTestScenario
    .For<HealthStrategy>("core.health")  // Strategy type + index
    .WithEntityName("player")
    .WithData("hp", 100)
    .WithData("max_hp", 100)
    .WithSystemConfig("difficulty", "hard")
    .Build();  // Creates Harness (auto-fires AfterSpawn)
```

Configuration methods:

| Method | Description |
|--------|-------------|
| `WithEntityName(string)` | Set entity name (default `__test_entity__`) |
| `WithData<TValue>(key, value)` | Inject initial data into the entity |
| `WithSystemConfig<TValue>(key, value)` | Write configuration to the system blackboard |
| `WithProgressConfig<TValue>(key, value)` | Write configuration to the progress blackboard |
| `WithSessionConfig<TValue>(key, value)` | Write configuration to the session blackboard |
| `WithTemplate(key, SndMetaData)` | Register a template |

### Phase 2: Simulate

```csharp
harness.RunFrame();          // 1 frame (Process + flush deferred actions)
harness.RunFrames(60);       // 60 frames
harness.TriggerAfterLoad();  // Manually trigger lifecycle hooks
harness.TriggerBeforeSave();
harness.TriggerBeforeQuit();
```

Hook trigger methods: `TriggerAfterSpawn` / `TriggerAfterLoad` / `TriggerAfterAdd` / `TriggerBeforeRemove` / `TriggerBeforeSave` / `TriggerBeforeQuit` / `TriggerBeforeDead`

### Phase 3: Inspect

```csharp
// Data access
int hp = harness.GetEntityData<int>("hp");
var (found, lives) = harness.TryGetEntityData<int>("lives");

// Blackboard access
var (hasDifficulty, difficulty) = harness.SystemBlackboard.TryGet<string>("difficulty");
var (hasScore, score) = harness.SessionBlackboard.TryGet<int>("score");

// Side-effect inspection
harness.SaveRequests.Count;       // Number of save requests
harness.LoadRequests.Count;       // Number of load requests
harness.LevelSwitchRequests;      // List of level switch requests
harness.DeferredActionCount;      // Number of deferred actions executed
harness.ConsoleCommands;          // Console command records
```

## ActiveStrategy Testing

Used for testing active strategies that inherit from `ActiveStrategyBase` (only `Invoke` method, no lifecycle hooks).

### Configure & Execute

```csharp
var harness = StrategyTestScenario
    .ForActive<GenerateFoodKeyStrategy>("food.generate_key")
    .WithData("food.registry", "[]")
    .WithData("food.next_id", 1)
    .WithSessionConfig("seed", 42)
    .Build();

// Invoke the strategy, with optional input
var key = harness.Invoke() as string;
// Or with an input parameter
var result = harness.Invoke("some_input");

// Inspect entity data changes
var nextId = harness.GetEntityData<int>("food.next_id");
Assert.Equal(2, nextId);

// Invoke via entity's InvokeStrategy method (ensures delegation chain is correct)
var key2 = harness.InvokeViaEntity();
```

### Differences from EntityStrategy Harness

| Capability | `For<T>` Harness | `ForActive<T>` Harness |
|------------|-----------------|----------------------|
| `Invoke(object?)` | ❌ None | ✅ Core method |
| `InvokeViaEntity()` | ❌ None | ✅ Delegation verification |
| `RunFrame` / `RunFrames` | ✅ | ❌ None (ActiveStrategy does not participate in frame updates) |
| `TriggerAfterSpawn` etc. hooks | ✅ | ❌ None |
| `FlushDeferredActions()` | (Auto in RunFrame) | ✅ Manual call |
| Blackboard/side-effects/templates | ✅ | ✅ |
| Auto-fire AfterSpawn | ✅ (in Build) | ❌ (ActiveStrategy has no such hook) |

### Complete Examples

#### Active Invocation Strategy

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

#### Strategy with Input Parameter

```csharp
[Test]
public void DamageCalc_Invoke_AppliesModifier()
{
    var harness = StrategyTestScenario
        .ForActive<DamageCalcStrategy>("combat.damage_calc")
        .WithData("base_damage", 50)
        .Build();

    var result = harness.Invoke(1.5f);  // Pass multiplier

    Assert.That(result, Is.EqualTo(75.0f));
}
```

#### Business Deferred Action Tracking

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

#### Template Cloning

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

## Complete Example (EntityStrategy)

### Damage Strategy Test

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

### Save Request Test

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

## Limitations

### Supported Capabilities

- Entity data read/write (SetData / GetData / TryGetData)
- Three-level blackboard access (System / Progress / Session)
- EntityStrategy: the full strategy lifecycle hooks + Process frame update
- ActiveStrategy: Invoke invocation + delegation verification via entity InvokeStrategy
- Deferred actions (BusinessDeferred)
- Persistence request recording (Save/Load/LevelSwitch)
- Console command input/output
- Template registration

### Unsupported Capabilities

- Entity node access (`GetNode`) — test entities do not support nodes
- Background session creation — requires full `SndContext` integration tests
- Multi-entity interaction — each Harness corresponds to one entity
- Engine-type operations — e.g., position calculation with `Vector2` (requires Godot converter registration)

## Related Documents

- [SND Entity Model](snd-entity-model.en.md) — Strategy writing
- [Agent Reference](agent-reference.en.md) — Task-oriented runtime guidance and generated API link

---
[↑ Back to usage](README.en.md)
