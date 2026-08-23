<!-- docsync-pair: usage/strategy-lifecycle -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Strategy Lifecycle

> [↑ Back to usage](README.en.md)

## Three Closed-Loop Pairs + One Special Hook

Strategy lifecycle hooks use a **paired closed-loop** design, similar to the RAII pattern. Each pair of hooks manages resource acquisition and release for a specific scope. **Resources acquired in one hook must be released in its corresponding paired hook.**

| Closed Loop | Acquisition Hook | Release Hook | Scope | Typical Resources |
|-------------|-----------------|-------------|-------|-------------------|
| **Game logic loop** | `AfterSpawn` | `BeforeDead` | The entity's entire game lifecycle (creation→death) | Manager register/unregister, global event broadcasting, world-level stats |
| **Runtime resource loop** | `AfterLoad` | `BeforeQuit` | One runtime session (load→quit/switch save) | Input capture, audio channels, mouse visibility, subscription to runtime managers |
| **Strategy-level loop** | `AfterAdd` | `BeforeRemove` | During strategy attachment (dynamic addition→removal) | Strategy-specific temporary data fields, exclusive references |
| **(Special)** | `BeforeSave` | No pair | Triggered at save time | Engine state→Data deferred sync |

## Design Principles

### 1. Closed Loops Must Be Paired

Runtime non-persistent resources acquired in `AfterLoad` must be released in `BeforeQuit`. Do not rely on `BeforeRemove` — strategies are not necessarily removed on exit.

```csharp
// ✅ Correct: AfterLoad ↔ BeforeQuit paired (runtime non-persistent resources)
public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    entity.OwningSession.FindByName("InputRouter")?.InvokeStrategy("input.capture_begin", entity.Name);
}

public override void BeforeQuit(ISndEntity entity, ISndContext ctx)
{
    entity.OwningSession.FindByName("InputRouter")?.InvokeStrategy("input.capture_end", entity.Name);
}
```

```csharp
// ❌ Wrong: Acquired in AfterLoad, released in BeforeRemove (loop mismatch)
public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    entity.OwningSession.FindByName("InputRouter")?.InvokeStrategy("input.capture_begin", entity.Name);
}

public override void BeforeRemove(ISndEntity entity, ISndContext ctx)
{
    entity.OwningSession.FindByName("InputRouter")?.InvokeStrategy("input.capture_end", entity.Name); // BeforeRemove is not fired on exit
}
```

### 2. Different Loops Are Not Equivalent

- `AfterSpawn` fires only once when the entity is **first created**
- `AfterLoad` fires every time the entity is **recovered from a save**

Entities registered to a manager in `AfterSpawn` do not need to re-register when recovered from a save (the save already contains the manager's state).

```csharp
// Game logic loop: register with global manager (only on first creation)
public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
{
    var mgr = entity.OwningSession.FindByName("FoodManager");
    mgr.InvokeStrategy("food.register", entity.Name);
}

public override void BeforeDead(ISndEntity entity, ISndContext ctx)
{
    var mgr = entity.OwningSession.FindByName("FoodManager");
    mgr.InvokeStrategy("food.unregister", entity.Name);
}
```

### 3. AfterLoad Should Not Do Business Initialization

After save recovery, all of the entity's Data is already in the correct persisted state. `AfterLoad` should only restore **runtime non-persistent resources** (such as registering with runtime managers, rebuilding runtime caches, etc.); it should not reset business flows or restart logic. Data observation is handled by observer strategies, whose bindings are persisted with saves and auto-restored on load; no need to rebuild in `AfterLoad`.

```csharp
// ✅ Correct: AfterLoad only restores runtime non-persistent resources (e.g., registering with runtime managers)
public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    entity.OwningSession.FindByName("CombatManager")?.InvokeStrategy("combat.register", entity.Name);
}

// ❌ Wrong: AfterLoad resets business state (Data already has the correct value)
public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    entity.SetData("hp", 100);  // Overwrites the correct value from the save
}
```

### 4. BeforeQuit Can Safely Access Session Resources

During `BeforeQuit` execution, `entity.OwningSession`'s entity operations (`FindByName`/`GetEntities`) and `entity.OwningSession.SessionBlackboard` are guaranteed to be accessible. The framework's Dispose flow uses a two-phase flag to ensure session resources are only marked as disposed after all BeforeQuit hooks have completed.

Even if a BeforeQuit hook throws an exception, the framework ensures — via `try/finally` — that entity cleanup and scene container clearing always complete, leaving no residual entities that could cause infinite error loops.

```csharp
// ✅ Safe: Accessing session resources in BeforeQuit
public override void BeforeQuit(ISndEntity entity, ISndContext ctx)
{
    var session = entity.OwningSession;

    var mgr = session.FindByName("MyManager");
    mgr?.InvokeStrategy("my.unregister", entity.Name);
}
```

> Note: Data observation is handled by observer strategies, whose bindings are auto-unmounted by the framework on entity exit/death (firing `OnUnmounted`). There is no need to manually unsubscribe in BeforeQuit. BeforeQuit is only for releasing runtime resources outside observer strategies (e.g., unregistering from external managers).

### 5. BeforeSave for Deferred Sync

For engine-managed state (such as `Node3D.GlobalTransform`), there is no need to write to entity Data every frame. Sync once at `BeforeSave` time to reduce unnecessary data write overhead.

> Creating or destroying sessions is forbidden while BeforeSave hooks run: the save coordinator snapshots the session topology and session set before invoking hooks, so `ISessionManager.CreateBackgroundSession` / `DestroySession` throw `InvalidOperationException`, preventing incomplete saves or topology/payload mismatches.

```csharp
public override void BeforeSave(ISndEntity entity, ISndContext ctx)
{
    var node = entity.GetNode("root")?.GetNativeNode();
    if (node is Node3D node3d)
        entity.SetData("transform", node3d.GlobalTransform);
}
```

## Strategy-Level Closed Loop: Dynamic Strategy Resource Management

Dynamically added/removed strategies (such as action strategies) use `AfterAdd` / `BeforeRemove` to manage strategy-specific temporary data:

```csharp
[StrategyIndex("game.action.move_to")]
public sealed class MoveToActionStrategy : LifecycleStrategyBase
{
    public override void AfterAdd(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("action.progress", 0f);
    }

    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        // Execute movement logic...
    }

    public override void BeforeRemove(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("action.progress", -1f);
    }
}
```

## Complete Closed-Loop Example

```csharp
[StrategyIndex("game.character.core", Priority = 10)]
public sealed class CharacterCoreStrategy : LifecycleStrategyBase
{
    // Game logic loop: register/unregister with manager (AfterSpawn ↔ BeforeDead paired)
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        entity.OwningSession.FindByName("CharacterManager")?.InvokeStrategy("character.register", entity.Name);

        // Observation loop: mount hp observer strategy (bindings persist with saves, auto-restore on load, auto-unmount on death)
        entity.MountObserverStrategy(entity.Name, "game.character.hp_death");
    }

    public override void BeforeDead(ISndEntity entity, ISndContext ctx)
    {
        entity.OwningSession.FindByName("CharacterManager")?.InvokeStrategy("character.unregister", entity.Name);
    }

    // Special hook: sync engine state on save
    public override void BeforeSave(ISndEntity entity, ISndContext ctx)
    {
        var node = entity.GetNode("root")?.GetNativeNode();
        if (node is Node3D n)
            entity.SetData("transform", n.GlobalTransform);
    }
}

// Observer strategy: mark death when hp reaches zero
[StrategyIndex("game.character.hp_death")]
[ObserveData("hp")]
public sealed class HpDeathObserver : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        if (newValue.TryGetInt32(out var hp) && hp <= 0)
            entity.SetData("is_dead", true);
    }
}
```

## Common Mistakes

| Mistake | Consequence | Correct Approach |
|---------|------------|-----------------|
| Manually subscribe/unsubscribe data changes in lifecycle hooks | Disconnected from save persistence & load recovery; prone to leaks | Use observer strategies (`ObserverStrategyBase`); bindings auto-persist and auto-unmount |
| Initialize runtime resources in `AfterSpawn` | Resources not established when recovering from a save | Initialize runtime resources in `AfterLoad` |
| Reset Data values in `AfterLoad` | Overwrites the correct persisted state from the save | `AfterLoad` only restores non-persistent resources |
| Sync engine state to Data every frame | Unnecessary write overhead | Use `BeforeSave` for deferred sync |
| Mix acquisition/release from different loops | Resource leaks or exceptions | Strictly pair acquisition/release hooks from the same loop |

---

## Intent-Driven Plan Execution (Planning)

For entities that need multi-step plan execution (such as AI character scheduling), Origo provides `PlanExecutionStrategyBase` (in the `Origo.Core.Planning` namespace) as a high-level lifecycle wrapper.

`PlanExecutionStrategyBase` inherits `LifecycleStrategyBase` and uses `sealed` lifecycle hooks to auto-manage subscription pairing, Action strategy insertion/removal, and plan advancement, keeping the RAII closed loop at the framework layer. Users only need to implement two domain mapping functions:

| Abstract Member | Responsibility |
|----------------|---------------|
| `ResolveNextStep(intent, currentStep, failed, entity)` | Intent → plan step decomposition |
| `StepToActionIndex(stepType)` | Step type → Action strategy index mapping |

**Relationship with original lifecycle hooks:**

- The base class `sealed`s all 8 `LifecycleStrategyBase` lifecycle hooks
- Users extend behavior through virtual `On*` hooks (`OnAfterSpawn`, `OnAfterLoad`, `OnProcess`, etc.)
- Subscription and Action strategy lifecycle are fully managed by the base class; users need not worry
- Original hooks cannot be overridden → impossible to forget calling base → eliminates wiring failure risk

**Example:**

```csharp
[StrategyIndex("character.scheduling", Priority = 5)]
public sealed class CharacterSchedulingStrategy : PlanExecutionStrategyBase
{
    public override string IntentKey => "character.intent";
    public override string IntentStatusKey => "character.intent_status";
    public override string PlanStepKey => "character.plan_step";
    public override string ActionKey => "character.action";
    public override string ActionStatusKey => "character.action_status";

    public override string? ResolveNextStep(string? intent, string? currentStep, 
        bool failed, ISndEntity entity)
    {
        return intent switch
        {
            "forage" => "find_target",
            "combat" => "find_enemy",
            "wander" => "wander",
            _ => null
        };
    }

    public override string? StepToActionIndex(string stepType)
    {
        return stepType switch
        {
            "find_target" => "character.action.find_target",
            "find_enemy" => "character.action.find_enemy",
            "wander" => "character.action.wander_target",
            _ => null
        };
    }
}
```

See: [Planning Subsystem Documentation](../Origo.Core/Planning/README.en.md) and [Design Patterns - Scheduling Layer](design-patterns.en.md).

---

## Next Document

- [Design Patterns](design-patterns.en.md) — Common design patterns for the strategy system
- [SND Entity Model](snd-entity-model.en.md) — Model fundamentals

---
[↑ Back to usage](README.en.md)
