# Origo

[简体中文](README.zh-CN.md)

**Origo** is a lightweight, platform-agnostic C# game framework.  
Write your game logic as strategies — Origo handles entity lifecycle, persistence, and runtime orchestration.  
Engine integration is isolated behind an adapter layer (official Godot 4 adapter included).

## What You Can Do

### Write game logic as strategies

Every piece of gameplay behavior is a **strategy** — a plain C# class. No base engine class required.
Strategies are stateless, pooled, and validated at registration so they don't silently break at runtime.

```csharp
[StrategyIndex("my_game.health")]
public class HealthStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("hp", 100);
    }

    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, hp) = entity.TryGetData<int>("hp");
        if (found && hp <= 0)
            entity.OwningSession.RequestKillEntity(entity.Name);
    }
}
```

- **SND model**: Strategy (behavior), Node (presentation), and Data (state) are separated by design.
- **Full lifecycle hooks**: `AfterSpawn`, `AfterLoad`, `AfterAdd`, `Process`, `BeforeRemove`, `BeforeSave`, `BeforeQuit`, `BeforeDead` — hook into any phase.
- **Data observation**: subscribe to data changes on any entity (self or cross-entity) with observer strategies. Bindings persist across save/load.
- **Active strategies**: type-safe cross-entity service calls with `InvokeStrategy<TInput, TOutput>`.
- **Add/remove strategies at runtime**: mount and unmount strategies dynamically with full lifecycle awareness.
- **TypedData**: compile-time generated strongly-typed data accessors via Roslyn source generator. Zero boxing, zero string keys in hot paths.

### Manage state and persistence

- **Built-in save system**: current workspace + snapshot slots. Two-phase write (`current/` → `save_xxx/`) with hash-based idempotent dedup. Strict read validation on load.
- **Background sessions**: run AI simulation, procedural generation, or off-screen world updates in a background session — same strategy logic, same data contracts as the foreground. Create one with `ctx.SessionManager.CreateBackgroundSession(key, levelId)`.
- **Snapshot management**: enumerate, inspect metadata, and select saves at runtime.

### Navigation, AI, and planning

- **Grid coordinate system**: `GridPos` with single/dual-axis conversion.
- **A\* pathfinding**: built-in, grid-based.
- **State machine**: string-stack state machine with push/pop strategy hooks. Stack state is serialized and restored on load.
- **Intent-driven planning**: `PlanExecutionStrategyBase` for sequences of actions with scoped parameter store.
- **Deferred action scheduling**: thread-safe queue with snapshot-and-drain pattern.

### Utility systems

- **RNG**: `XorShift128+` (period 2^128−1), no global state. `PersistentRandom` for save-safe reproducible randomness.
- **Noise generation**: OpenSimplex2 + Worley cellular noise for procedural terrain/content.
- **Blackboard**: in-memory key-value store, serializable, for runtime configuration and shared state.
- **Archetype loading**: load entity data from key-value pair files with automatic type inference.

### Development tools

- **TCP remote console** (port 9876): send commands and receive output over a network connection — designed for agent-driven development and automated testing. 11 built-in commands for entity inspection, data manipulation, and strategy invocation. Extensible with custom commands.

```bash
nc localhost 9876
```

- **Source generator**: Roslyn incremental generator emits compile-time typed data accessors, eliminating boxing and string-key lookups in hot paths. 4 diagnostics (`ORIGOSG001`–`004`) catch misconfigurations at build time.
- **Test infrastructure**: `StrategyTestScenario` for declarative strategy unit tests (Configure → Simulate → Inspect). Architecture guardrail tests enforce dependency direction and strategy constraints.

### Godot 4 adapter

- **File system**: `res://` and `user://` access through `IFileSystem`, with path traversal protection.
- **Logging proxy**: bridges Core logging to `GD.Print` / `PushWarning` / `PushError`.
- **Scene node factory**: instantiate `PackedScene` nodes via logical scene aliases.
- **Entity-node bridge**: `GodotSndEntity` links `ISndEntity` lifecycle with Godot `Node` lifecycle. Godot types (14 vector/math types) serialize to JSON with full round-trip fidelity.

## Quick Start (Godot 4)

### 1. Add packages

**NuGet** (recommended): download `.nupkg` files from the [latest release](https://github.com/51193/origo/releases/latest), place them in `./packages/origo/`, and configure a local package source:

```xml
<!-- nuget.config in your Godot project root -->
<configuration>
  <packageSources>
    <add key="origo-local" value="./packages/origo/" />
  </packageSources>
</configuration>
```

```xml
<PackageReference Include="Origo.Core" />
<PackageReference Include="Origo.GodotAdapter" />
```

### 2. Create folder structure

```
res://origo/
  entry/entry.json
  maps/scene_aliases.map
  maps/snd_templates.map
  initial/
```

### 3. Add entry node

Attach `OrigoDefaultEntry` to your startup scene and configure paths.  
> If Godot can't resolve the `[GlobalClass]`, create a one-line bridge class:
> ```csharp
> [GlobalClass]
> public partial class MyOrigoEntry : GodotAdapter.Bootstrap.OrigoDefaultEntry { }
> ```

### 4. Write a strategy and define entities

```csharp
[StrategyIndex("game.player_move", Priority = 100)]
public sealed class PlayerMoveStrategy : LifecycleStrategyBase
{
    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, speed) = entity.TryGetData<float>("speed");
        if (!found) return;
        // movement logic...
    }
}
```

```json
{
  "name": "Player",
  "node": { "pairs": { "sprite": "player_sprite" } },
  "strategy": { "indices": ["game.player_move"] },
  "data": { "pairs": { "speed": { "type": "Single", "data": 200.0 } } }
}
```

### 5. Run

`OrigoDefaultEntry._Ready()` discovers all `[StrategyIndex]` strategies, loads aliases and templates, and boots the game.

> Full walkthrough: [Quick Start](docs/usage/quick-start.en.md) &middot; [Architecture Overview](docs/usage/architecture-overview.en.md) &middot; [SND Entity Model](docs/usage/snd-entity-model.en.md)

## Documentation

Full documentation lives in this repository under **[`docs/`](docs/README.md)** — a bottom-up structural mirror of the source tree.

Development workflow and agent rules: **[`AGENTS.md`](AGENTS.md)**.

> Documentation is available in Chinese and English — browse [`docs/`](docs/README.md) in either language.

| I want to... | Go to |
|---|---|
| Browse all capabilities | [Capabilities](docs/usage/capabilities.en.md) |
| Understand the architecture | [Architecture Overview](docs/usage/architecture-overview.en.md) |
| Learn the SND model | [SND Entity Model](docs/usage/snd-entity-model.en.md) |
| Test my strategies | [Strategy Testing](docs/usage/strategy-testing.en.md) |
| Use the save system | [Persistence Flow](docs/usage/persistence-flow.en.md) |
| Use the state machine | [State Machine](docs/usage/state-machine.en.md) |
| Use the console | [Console Commands](docs/usage/console-commands.en.md) |
| Reference for AI agents | [Agent Reference](docs/usage/agent-reference.en.md) |

## Development

```bash
bash scripts/ci.sh        # Full CI pipeline (format + test + benchmarks + Godot integration)
bash scripts/test.sh      # Build + test + coverage gates (dev iteration)
bash scripts/format.sh    # Format check only
```

| Module | Description |
|---|---|
| `Origo.Core` | Platform-agnostic core: SND entities, runtime, persistence, state machines |
| `Origo.SourceGeneration` | Roslyn incremental source generator for TypedData |
| `Origo.ConsoleBridge` | TCP remote console bridge |
| `Origo.GodotAdapter` | Godot 4 adapter: file system, logging, serialization, bootstrap |

| Test project | Coverage gate |
|---|---|
| All test projects | ≥ 90% |

## License

MIT. See [LICENSE](LICENSE).
