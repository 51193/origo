<!-- docsync-pair: usage/design-patterns -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Design Patterns

> [↑ Back to usage](README.en.md)

This document collects common design patterns used when developing with the Origo framework. These patterns are distilled from real projects and apply to any game based on the SND model.

---

## Naming Conventions

### Strategy Indices

Format: `{domain}.{module}.{function}`

```
camera.main.participant
map.generator
character.pathfind.astar
ui.main_menu
```

- Use lowercase snake_case
- Domain represents a functional area (`camera`, `map`, `character`, `ui`, `item`)
- Module is an optional sub-category
- Function describes the strategy's behavior

### Data Keys

Format: `{domain}.{key_name}`

```
character.hp
camera.zoom_level
map.grid_size
```

- Entity-specific data uses short key names (e.g., `hp`, `speed`)
- Cross-entity shared config uses namespace-prefixed keys (e.g., `camera.stack.pair`)
- Avoid key collisions across different strategies

---

## Strategy-Node Interaction

Strategies obtain engine nodes via `entity.GetNode("name")?.GetNativeNode()` (extension method in the `Origo.GodotAdapter.Snd` namespace). They can also use `entity.GetNodeFromSnd<TNode>("name")` to directly fetch a strongly-typed node. Core rules:

1. **Always null-check**: In background sessions, `GetNode()` returns `NullNodeHandle`, and `?.GetNativeNode()` is null
2. **Encapsulate node operations in helper classes**: Strategies should not directly manipulate the scene tree (`AddChild`, `QueueFree`, etc.)
3. **Access via node name mapping**: Use node names defined in Origo templates; do not traverse the scene subtree

```csharp
public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
{
    var node = entity.GetNode("root")?.GetNativeNode();
    if (node is Node3D node3d)
    {
        var (fx, gx) = entity.TryGetData<int>("grid_x");
        var (fz, gz) = entity.TryGetData<int>("grid_z");
        if (fx && fz)
            node3d.GlobalPosition = GridToWorld(gx, gz);
    }
}

public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    var node = entity.GetNode("root")?.GetNativeNode();
    if (node is Node3D node3d)
    {
        var (fx, gx) = entity.TryGetData<int>("grid_x");
        var (fz, gz) = entity.TryGetData<int>("grid_z");
        if (fx && fz)
            node3d.GlobalPosition = GridToWorld(gx, gz);
    }
}
```

Both `AfterSpawn` and `AfterLoad` must null-check and perform the same node initialization logic, because entities created in a background session do not receive a real node until the foreground session is restored.

---

## Self-Destruct Initializer Pattern

A one-shot initialization strategy removes itself after setup completes, avoiding idle overhead:

```csharp
[StrategyIndex("game.entity.init")]
public class EntityInitStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        // Load numeric recipe
        LoadArchetypeAttributes(entity, ctx);

        // Dynamically add runtime strategies
        entity.AddStrategy("game.entity.perception");
        entity.AddStrategy("game.entity.scheduling");

        // Self-destruct
        entity.RemoveStrategy("game.entity.init");
    }
}
```

Applicable scenarios:
- Complex initialization logic when an entity is first created (expanding attributes, dynamically constructing strategy composition)
- Initialization logic only needs to run once; no per-frame Process needed
- The template only declares this initializer strategy; runtime strategies are dynamically built by it

---

## Manager Entity + ActiveStrategy Service Pattern

Use an entity as a global service, exposing query/mutation interfaces through ActiveStrategies:

```csharp
// Manager strategy: maintains data, no Process loop
[StrategyIndex("food.manager")]
public class FoodManagerStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        entity.AddActiveStrategy("food.register");
        entity.AddActiveStrategy("food.unregister");
        entity.AddActiveStrategy("food.find_nearest");
    }
}

// ActiveStrategy: provides query services
[StrategyIndex("food.find_nearest")]
public class FoodFindNearestStrategy : ActiveStrategyBase
{
    public override object? Execute(ISndEntity entity, object? input, ISndContext ctx)
    {
        // Read food registry from entity Data, compute nearest food
        // Return result
    }
}
```

Characteristics:
- The Manager entity acts as a pure data container + on-demand ActiveStrategies, with no Process loop
- Other entities call services via `managerEntity.InvokeStrategy("food.find_nearest", position)`
- Manager state participates in persistence (service state auto-recovers after save/load)

---

## Replaceable Implementation Pattern (`*_impl` Keys)

Use data keys to store strategy indices, enabling runtime behavior switching:

```csharp
// Scheduler reads impl keys and dynamically selects strategies
[StrategyIndex("game.scheduling")]
public class SchedulingStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        // Set default implementations
        entity.SetData("pathfind_impl", "game.pathfind.astar");
        entity.SetData("move_impl", "game.move.walk");
    }

    private static void EnsureImpl(ISndEntity entity, string implKey)
    {
        var (found, index) = entity.TryGetData<string>(implKey);
        if (found && !string.IsNullOrEmpty(index))
            entity.AddStrategy(index);
    }
}
```

Applicable scenarios:
- Multiple implementations of the same capability (e.g., A* pathfinding vs. straight-line movement)
- Need to switch behavior at runtime (e.g., ground movement → flight movement)
- Avoid baking implementation choices into templates

---

## Inter-Entity Communication

### InvokeStrategy: Synchronous Request/Response

```csharp
var target = entity.OwningSession.FindByName("TraversabilityManager");
var path = target.InvokeStrategy<GridPos[], List<GridPos>>(
    "traversability.find_path", new[] { start, end });
```

### Observer Strategies: Async Data Change Notification

Mount observer strategies (`ObserverStrategyBase` + `[ObserveData]`) to respond to target entity data changes; bindings persist with saves and auto-restore on load:

```csharp
[StrategyIndex("schedule.intent_watcher")]
[ObserveData("intent")]
public sealed class IntentWatcher : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        if (newValue.TryGetString(out var intent) && intent == "idle")
            ResetSchedule(entity);
    }
}

// Mount (self-observation or cross-entity target resolved via FindByName)
entity.MountObserverStrategy(entity.Name, "schedule.intent_watcher");
```

### Rules

- **Never hold direct entity references** (entities may be destroyed at any time)
- Look up on demand via `entity.OwningSession.FindByName(name)`
- Implement synchronous request/response via `InvokeStrategy`
- Implement async data change notification via observer strategies (`MountObserverStrategy` + `[ObserveData]`)

---

## UI/Logic Separation

Keep strategies as pure logic; delegate Godot node operations to `internal static` helper classes:

```csharp
// Strategy: decision logic
[StrategyIndex("ui.main_menu")]
public class MainMenuStrategy : LifecycleStrategyBase
{
    public override void AfterLoad(ISndEntity entity, ISndContext ctx)
    {
        var container = entity.GetNode("root")?.GetNativeNode();
        if (container is Control ctrl)
            MenuBuilder.BuildMainMenu(ctrl, ctx);
    }
}

// Helper: pure UI operations
internal static class MenuBuilder
{
    internal static void BuildMainMenu(Control container, ISndContext ctx)
    {
        // Godot node operations...
    }
}
```

Benefits:
- Strategy logic can be independently tested via `StrategyTestScenario`
- Helper classes can be reused across strategies
- Separation of concerns: the strategy controls "when to do", the helper controls "how to do"

---

## Priority Layered Execution

Use different `Priority` values to divide strategy execution layers (smaller values execute first):

```
P4   Perception layer      Read environment / self state → produce intent
P5   Scheduling layer       Based on intent → decompose action plan
P6   Action layer           Execute specific behavior → report completion/failure
P20  Pathfinding layer      Read target → compute path
P30  Movement layer         Read next step → execute displacement
P35  Detection layer        Per-frame condition detection → trigger outcomes
```

Design points:
- Within the same frame, lower Priority executes first; produced data is consumed by higher Priority strategies in the same frame
- Continuously running subsystems (pathfinding, movement) use different priority bands from decision systems (perception, scheduling)
- Different layers communicate through data keys with no direct coupling

### Scheduling Layer's PlanExecutionStrategyBase

The framework provides [`PlanExecutionStrategyBase`](../Origo.Core/Planning/README.en.md) as the standard base class for the scheduling layer (P5). It encapsulates the complete lifecycle of intent → plan → step → action:

- **Subscription wiring**: Auto-manages the RAII closed loop for `intent` and `action_status` data subscriptions
- **Plan advancement**: Intent change restarts the plan; action completion/failure advances to the next step or terminates
- **Action lifecycle**: Each step automatically does `AddStrategy(StepToActionIndex(step))`; removal happens on advance/termination

Any step type (idle, patrol, standby, etc.) should be implemented as an independent `LifecycleStrategyBase` Action strategy, registered via `StepToActionIndex`. The base class treats all steps uniformly and does not hardcode any specific behavior.

Users only need to implement two abstract methods:

```csharp
[StrategyIndex("character.scheduling", Priority = 5)]
public sealed class MySchedulingStrategy : PlanExecutionStrategyBase
{
    protected override string IntentKey => "my.intent";
    protected override string IntentStatusKey => "my.intent_status";
    protected override string PlanStepKey => "my.plan_step";
    protected override string ActionKey => "my.action";
    protected override string ActionStatusKey => "my.action_status";

    protected override string? ResolveNextStep(
        string intent, string currentStep, bool failed, ISndEntity entity) { ... }

    protected override string? StepToActionIndex(string stepType) { ... }
}
```

---

## Template Best Practices

### Templates Must Include All Data Keys Required by Strategies

Strategies read data at runtime via `TryGetData<T>("key")`. The interface does not throw on missing keys (returns `found=false`), but missing required keys in templates cause strategy logic to silently skip or take fallback paths, making debugging difficult.

```json
{
  "strategy": { "lifecycle_indices": ["game.camera.zoom"] },
  "data": {
    "pairs": {
      "camera.zoom_level": { "type": "Single", "data": 0.5 },
      "camera.height": { "type": "Single", "data": 20.0 }
    }
  }
}
```

### Template vs. Archetype Responsibility Separation

- **Template (entity topology)** — The complete definition of an entity: strategy composition, node bindings, data key declarations and default values. A template is self-sufficient — it can produce a fully functional entity without any external data.
- **Archetype (numeric recipe)** — A flexible numeric externalization tool: a collection of properties in flat key-value pair form, containing no behavioral definitions.

There is no mandatory correspondence between the two. A template can work without recipes (relying on its own defaults), can load one recipe at creation time to override defaults, or can load multiple recipes on demand during its lifecycle:

```json
{
  "strategy": { "lifecycle_indices": ["item.food"] },
  "data": {
    "pairs": {
      "food.archetype": { "type": "String", "data": "berry" }
    }
  }
}
```

The strategy loads the corresponding numeric recipe file on demand based on the `food.archetype` data key, obtaining property values like `food.hunger_restore`, `food.texture_index` and writing them into entity data.

Usage principles:
- Different behavioral capabilities (strategy composition changes) → create a new Template
- Attribute values need external management (avoid hardcoding, enable reuse and tuning) → create a new Archetype
- Archetype loading timing and quantity are entirely up to the strategy — batch loading at AfterSpawn, on-demand queries during lifecycle (e.g., equipment stat calculation), or no use at all, are all valid
- Archetype file format is determined by the framework's file abstraction; not limited to specific suffixes

### Only Declare Initial Strategies

In template `strategy.indices`, only write initializer strategies (e.g., `game.entity.init`); runtime strategies are dynamically added by the initializer strategy. This avoids baking implementation choices into templates and having to modify templates every time a new strategy is added.

### Background Session Nodes Are Null

Entities created in background sessions have no engine node (`GetNode()` returns `NullNodeHandle`). Node access in strategy `AfterSpawn` and `AfterLoad` must always null-check:

```csharp
var node = entity.GetNode("root")?.GetNativeNode();
if (node is Node3D n)
{
    // Operations when node exists (foreground session)
}
// Silently skip when no node (background session)
```

---

## Use Origo-Registered Names for `type` Fields

The `"type"` field in `data.pairs` within template JSON must use Origo's registered short names:

| Actual Type | Correct | Incorrect |
|-------------|---------|-----------|
| `int` | `Int32` | `int`, `Integer` |
| `float` | `Single` | `Float32`, `float` |
| `string` | `String` | `string` |
| `bool` | `Boolean` | `bool`, `Bool` |
| `double` | `Double` | `double` |
| `long` | `Int64` | `long`, `Long` |

Adapter-layer registered types (e.g., Godot's `Vector3`, `Transform3D`) use their .NET type short names.

---

## Next Document

- [Strategy Lifecycle](strategy-lifecycle.en.md) — Closed-loop pairing and resource management
- [SND Entity Model](snd-entity-model.en.md) — Model fundamentals

---
[↑ Back to usage](README.en.md)
