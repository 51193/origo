# Agent Reference

> [↑ Back to usage](README.en.md)

A task-oriented guide for agents working on an Origo game or on the framework.
The canonical public signatures are generated in
[contract-api.en.md](../generated/contract-api.en.md); do not copy them into
hand-written documentation again.

## Ground rules

- Read [AGENTS.md](../../AGENTS.md) before changing framework source; the
  repository development loop is mandatory.
- Game code uses public contracts. Do not reach into internal kernel types or
  hand-stitch lifecycle operations that the framework orchestrates.
- Prefer explicit failure over silent fallback. Invalid IDs, missing payloads,
  unknown strategies, and partial saves must surface as exceptions.
- The framework goal is RPG Maker-style focus: define content, scenes, and
  strategies; let Origo own runtime orchestration and persistence.

## Start a host

The Godot path is `OrigoDefaultEntry` (or a subclass that calls `base._Ready()`
first). The Core path is `OrigoHost.Create` followed by `Bootstrap` and
`DriveFrame` when the host should run.

```csharp
public partial class MyEntry : OrigoDefaultEntry
{
    protected override void ConfigureStrategies(ISndWorldAccess world)
        => world.RegisterStrategy(() => new MyStrategy());

    public override void _Ready()
    {
        AutoDiscoverStrategies = false; // manual registration and discovery are alternatives
        ConfigPath = "res://origo/entry/entry.json";
        base._Ready();
    }
}
```

Use discovery (`AutoDiscoverStrategies`, the default) **or** manual
registration, not both for the same index. Discovery scans every non-skipped
assembly and rejects duplicate indices.

After `base._Ready()`, `Runtime`, `Context`, `ConsoleInput`, and
`ConsoleOutputChannel` are available. The main-menu entry load is deferred and
runs when the frame driver flushes. `_Process` drives
`Runtime.DriveFrame(delta)`; a bootstrap failure makes later frames fail
instead of running a half-initialized runtime.

## Write a strategy

Strategies are stateless and pooled. Use `LifecycleStrategyBase` for entity
behavior and `ObserverStrategyBase` for data observation.

```csharp
[StrategyIndex("game.health")]
public sealed class HealthStrategy : LifecycleStrategyBase
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

`LifecycleStrategyBase` supplies the ordered lifecycle hooks (`AfterSpawn`,
`AfterLoad`, `AfterAdd`, `Process`, `BeforeRemove`, `BeforeSave`, `BeforeQuit`,
`BeforeDead`), but Core decides when they run.

### Observe data

```csharp
[StrategyIndex("game.hp_watcher")]
[ObserveData("hp")]
public sealed class HpWatcher : ObserverStrategyBase
{
    public override void OnDataChanged(
        ISndEntity entity, ISndContext ctx, ISndEntity target,
        string dataKey, TypedData oldValue, TypedData newValue)
    {
        var (found, hp) = newValue.TryGetInt32(out var value)
            ? (true, value)
            : (false, 0);
        if (found && hp <= 0)
            ctx.Save.RequestSaveGame("game_over");
    }
}
```

Mount with `MountObserverStrategy(target, observerIndex)`. Observer bindings
are persisted and restored by the framework; do not manually reconnect them in
`AfterLoad`.

### Invoke an active strategy

Active strategies are indexed services separate from passive lifecycle
strategies. Typed helpers live in the Core shell; see
[Core/Snd](../Origo.Core/Snd/README.en.md).

```csharp
var result = entity.InvokeStrategy<DamageInput, DamageResult>(
    "game.damage_service", new DamageInput(target, 10));
```

## Sessions and persistence

- The foreground session is reached through `entity.OwningSession`; global
  orchestration uses `IOrigoRuntime.SessionManager`.
- Background sessions share `ISessionRun`, so the same strategy and save
  contracts apply.
- A level ID is held by at most one session. Resolve conflicts through the
  documented session API, not by mounting a second session directly.
- Save requests are deferred and complete under the frame loop. Use the
  completion state (`IsPersistenceIdle`, `IsBootstrapCompleted`) when an agent
  must wait for the framework.
- `AfterLoad` is for restoring live state; framework-managed observer and
  session state is restored by the load pipeline.

Full data layout and recovery rules: [Persistence Flow](persistence-flow.en.md).

## Console

The TCP bridge is loopback-only and has no authentication or encryption. It is
for local debugging and agent automation, never public exposure.

Built-in commands cover entity inspection, data access, strategy invocation,
save-slot operations, and help. The authoritative command list is
[Console Commands](console-commands.en.md); custom handlers register through
`IOrigoRuntime.RegisterConsoleCommandHandler`.

## Contract surface

`ISndContext` exposes capabilities through typed companion properties:

- `Blackboard`
- `Deferred`
- `Template`
- `ConsoleAccess`
- `StateMachines`
- `Save`
- `Lifecycle`
- `FileAccess`
- `ArchiveFileAccess`
- `StateMachineContext`

It also exposes `Bootstrap`, `Shutdown`, `SaveRootPath`,
`InitialSaveRootPath`, and `EntryConfigPath`. `Shutdown` is idempotent and
rejects teardown while a load/save/change-level workflow is in progress or
while a level-switch teardown owns the session set.

Interface declarations, member signatures, package ownership, and overloads:
[generated contract API](../generated/contract-api.en.md).

## Verification expectations

- A bug fix needs a real-path regression test; a behavior change updates the
  affected docs and Changelog.
- Run the layer-appropriate tests first, then the full local gate.
- Do not claim a UI or gameplay behavior from headless projection alone.
- Do not add public surface for test convenience.
- Framework changes follow the [Review and Pull-Request
  Process](../review-process.en.md): P1/P2 review findings and their fixes are
  independently blind-audited, delivery uses one module or one cohesive behavior per pull request,
  and fixes expand tests around the changed behavior.

See [Strategy Testing](strategy-testing.en.md), [Strategy Lifecycle](strategy-lifecycle.en.md),
and [Design Patterns](design-patterns.en.md) for task-specific guidance.
