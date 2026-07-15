<!-- docsync-pair: usage/snd-entity-model -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# SND Entity Model

> [↑ Back to usage](README.en.md)

## Model

```
Strategy + Node + Data
```

The SND model decomposes game entities into three orthogonal dimensions:

| Dimension | Purpose | Storage Location | Mutability |
|-----------|---------|-----------------|------------|
| **Strategy** | Behavioral logic | Strategy pool (globally reused) | Immutable (shared statelessly) |
| **Node** | Presentation-layer mapping | Engine scene tree | Engine-managed |
| **Data** | Mutable state | SndDataManager (TypedData) | Mutable (single authoritative source) |

## Strategy System

### Base Classes

```
BaseStrategy (abstract, base class for all strategies)
├── LifecycleStrategyBase     → 8 entity lifecycle hooks
├── ActiveStrategyBase     → Invoke(entity, ctx, input)
├── ObserverStrategyBase   → OnMounted / OnDataChanged / OnUnmounted
└── StateMachineStrategyBase → 4 state machine hooks
```

### Observer Strategies

`ObserverStrategyBase` is a first-class citizen in the SND strategy hierarchy, on par with `LifecycleStrategyBase` and `ActiveStrategyBase`. Observer strategies respond to data changes and binding lifecycles; the framework automatically manages wiring persistence.

Three virtual methods:

| Hook | Trigger | Typical Use |
|------|---------|------------|
| `OnMounted(entity, ctx, target)` | Binding established (mount / load recovery) | Initialize derived state from current data |
| `OnDataChanged(entity, ctx, target, dataKey, oldValue, newValue)` | Observed data changes | Reactive logic (e.g., intent status changes) |
| `OnUnmounted(entity, ctx, target)` | Binding terminated (explicit unmount / target death / observer exit) | Clean up state |

Declare keys to observe:

```csharp
[StrategyIndex("character.intent_watcher")]
[ObserveData("character.intent_status")]
public sealed class IntentWatcher : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        if (newValue.TryGetString(out var status) && status is "completed" or "failed")
            entity.SetData("character.intent", "");
    }
}
```

Mounting and unmounting:

```csharp
// EntityStrategy establishes self-observation in AfterSpawn
entity.MountObserverStrategy(entity.Name, "character.intent_watcher");

// Unmount in BeforeDead
entity.UnmountObserverStrategy(entity.Name, "character.intent_watcher");
```

- Observation strategies are mounted via `MountObserverStrategy(targetName, strategyIndex)`
- `entity` = observer entity, `target` = observed entity; both are the same entity for self-observation
- `observer_indices` serializes cross-entity bindings into save data; wiring is auto-restored on load
- Self-observation and cross-entity observation use the same mount API and the same binding topology format
- Like Data/Strategy, Observer bindings are persisted on save and auto-restored on load
### Entity Strategy Lifecycle Hooks

Hooks listed in execution order:

| Hook | Trigger | Typical Use |
|------|---------|------------|
| `AfterSpawn(entity, ctx)` | After new entity creation (uniformly fired by `SndEntityFactory.Spawn` / `SpawnMany`) | Initialize properties (hp, position, etc.) |
| `AfterLoad(entity, ctx)` | After recovering from a save (batch RecoverFromMetaList does not fire; fired uniformly by upper layer) | Re-register events, etc., after recovery |
| `AfterAdd(entity, ctx)` | Strategy dynamically added to entity | Add state logic dynamically |
| `Process(entity, delta, ctx)` | Every frame | Continuous logic (movement, timers, etc.) |
| `BeforeRemove(entity, ctx)` | Before strategy is removed | Clean up subscriptions/resources |
| `BeforeSave(entity, ctx)` | Before saving for serialization | Flush pending write data |
| `BeforeQuit(entity, ctx)` | Entity normal exit | Save progress, cleanup |
| `BeforeDead(entity, ctx)` | Before entity destruction (batch RemoveAllEntities does not fire; fired uniformly by upper layer) | Death effects, drops, etc. |

### Writing Strategies

```csharp
[StrategyIndex("my_game.damage_tick", Priority = 100)]
public class DamageTickStrategy : LifecycleStrategyBase
{
    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, hp) = entity.TryGetData<float>("hp");
        if (!found) return;

        entity.SetData("hp", hp - 5f * (float)delta);

        if (hp <= 0)
            ctx.RequestSaveGame("player_died");
    }
}
```

### Strategy Pool Rules

- **Statelessness enforcement**: Strategy classes must not declare instance fields or writable properties (validated via reflection at registration)
- **Registration**: `[StrategyIndex("xxx.yyy")]` attribute + assembly scanning
- **Index naming**: Dot-separated namespace + lowercase snake_case segments (e.g., `core.player.health`)
- **Priority**: The `Priority` attribute determines execution order of multiple strategies on the same entity (default 6205; lower executes earlier)
- **Reference counting**: When the same strategy is referenced by multiple entities, count increments; only recycled when all are released

### Forbidden Actions in Strategies

- Holding `IDisposable` fields or unmanaged resources
- Caching cross-frame mutable context objects
- Declaring instance fields to store runtime data (should be stored in entity Data)

## Entity Data

### TypedData

`readonly partial struct`, achieving value-type inline storage via discriminated union. At compile time, the Source Generator emits strongly-typed accessors for registered types.

```csharp
public readonly partial struct TypedData : IEquatable<TypedData>
{
    internal byte _kind;          // 0=Null, 1=Int32, 2=Single, ...
    internal long _inlineBits;    // Value types ≤ 8 bytes use inline storage
    internal object? _ref;        // Reference types or large value types fallback

    public Type DataType { get; }  // e.g., typeof(int)
    public object? Data { get; }   // Box on demand, used at serialization boundary
    public bool IsNull { get; }

    // Strongly-typed accessors generated by Source Generator
    public bool TryGetInt32(out int value);
    public bool TryGetSingle(out float value);
    public bool TryGetString(out string? value);
    // ... one TryGetXxx per registered type
}
```

- **On write**: Generic `SetData<T>` inline-writes the value into `_inlineBits` via `TypedDataFactory<T>.Create(value)` (system types) or bridges into `_ref` via `TypedDataObjectConverter` (adapter-layer registered types), with zero boxing and zero heap allocation
- **On read**: `TypedDataFactory<T>.TryExtract(td, out var value)` dispatches by discriminant; registered types use direct field reading or Kind checks, unregistered types fall back to `is T`
- **On serialization**: The `TypedData.Data` property boxes the value on demand; `TypedDataConverter` converts types to strings and maps bidirectionally via `TypeStringMapping`
- **Multi-adapter support**: GodotAdapter registers 14 engine types via `[assembly: SndInlineTypes(startKind: 128, ...)]`; the runtime resolves Kind values through the `TypedDataLayeredRegistry` delegate chain. `TypedDataTypeMap.GetKindForType(typeof(Vector3))` returns the deterministic value 130, avoiding `is T` pattern matching
- **Unregistered types**: Fall back to the `_ref` path (kind=`TypedData.UnregisteredKind`), with performance equivalent to the class-based approach

### Safe Reading

```csharp
// The interface only provides TryGetData<T>; check found before using the value
var (found, hp) = entity.TryGetData<int>("hp");
if (found) { /* use hp */ }

// Cross-precision numeric reading (int/float/long/double unified to float output)
if (entity.TryGetNumeric("speed", out var speed)) { /* use speed */ }
```

### Data Observation

Data changes are responded to via `ObserverStrategyBase`'s `OnDataChanged` callback:

```csharp
[StrategyIndex("my_game.hp_watcher")]
[ObserveData("hp")]
public sealed class HpWatcher : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        if (newValue.TryGetInt32(out var hp) && hp <= 0)
            ctx.RequestSaveGame("entity_died");
    }
}
```

After mounting the observer strategy, the framework auto-manages wiring and persistence:

```csharp
// Mount in EntityStrategy's AfterSpawn
entity.MountObserverStrategy(entity.Name, "my_game.hp_watcher");
```

Observer binding relationships are persisted into save files via the `StrategyMetaData.ObserverIndices` field and auto-restored on load; no need to manually re-mount in AfterLoad.

## Strategy Execution Order

Multiple strategies on the same entity execute in ascending `Priority` order; equal priority uses insertion order:

```
Priority: 10  →  Strategy A  (executes first)
Priority: 50  →  Strategy B
Priority: 100 →  Strategy C
Priority: 6205 (default) → Strategy D
```

All hooks (Process / AfterSpawn / etc.) follow this order.

## Entity Metadata

The entity serialization format:

```json
{
  "name": "player",
  "node": {
    "pairs": {
      "main": "res://scenes/player.tscn"
    }
  },
  "strategy": {
    "lifecycle_indices": ["my_game.health", "my_game.movement"],
    "active_indices": ["my_game.move_to"],
    "observer_indices": [
      { "player": ["my_game.hp_watcher"] }
    ]
  },
  "data": {
    "pairs": {
      "hp": { "type": "Int32", "data": 100 },
      "pos": { "type": "Vector2", "data": { "x": 10, "y": 20 } }
    }
  }
}
```

## Next Document

- [Session Model](session-model.en.md) — Foreground/background sessions
- [Persistence Flow](persistence-flow.en.md) — Save/load
- [Strategy Testing](strategy-testing.en.md) — StrategyTestScenario

---
[↑ Back to usage](README.en.md)
