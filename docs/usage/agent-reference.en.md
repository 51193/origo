# Agent Reference

> [↑ Back to usage](README.en.md)

## Overview

A complete runtime reference for AI Agent developers. Contains core interface signatures, lifecycle timelines, strategy writing templates, and testing patterns.

## Core Interfaces

### ISndEntity

```csharp
public interface ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess,
    ISndActiveStrategyAccess, ISndObserverStrategyAccess
{
    string Name { get; }
    bool IsPendingKill { get; }
    ISessionRun OwningSession { get; }
}

public interface ISndDataAccess
{
    void SetData<T>(string name, T value);
    (bool found, T? value) TryGetData<T>(string name);
    bool TryGetData<T>(string name, out T? value);
    T GetData<T>(string name) where T : notnull;
}

// TryGetNumeric extension methods (Origo.Core.Snd.TryGetNumericExtensions):
// bool entity.TryGetNumeric(string key, out float value)
// float entity.GetNumeric(string key, float fallback)
// Attempts reading in order: float → int → remaining integer types (byte/sbyte/short/ushort/char/uint/ulong) → long → double, bridging type mismatches.

public interface ISndObserverStrategyAccess
{
    // Mount/unmount observer strategies (ObserverStrategyBase).
    // targetName == own Name is self-observation; cross-entity name resolution requires the scene host; prefer the ISndEntity overload.
    void MountObserverStrategy(string targetName, string observerIndex);
    void UnmountObserverStrategy(string targetName, string observerIndex);
    void MountObserverStrategy(ISndEntity target, string observerIndex);
    void UnmountObserverStrategy(ISndEntity target, string observerIndex);
}

public interface ISndNodeAccess
{
    INodeHandle GetNode(string name);
    IReadOnlyCollection<string> GetNodeNames();
}

public interface ISndStrategyAccess
{
    void AddStrategy(string index);
    void RemoveStrategy(string index);
}

public interface ISndActiveStrategyAccess
{
    void AddActiveStrategy(string index);
    void RemoveActiveStrategy(string index);
    object? InvokeStrategy(string strategyIndex, object? input = null);
}

// ActiveStrategy generic extension methods (Origo.Core.Snd.ActiveStrategyExtensions):
// TOutput? entity.InvokeStrategy<TOutput>(string strategyIndex)
// TOutput? entity.InvokeStrategy<TInput, TOutput>(string strategyIndex, TInput input)
// Transparently handles JSON serialization/deserialization, eliminating caller-side boilerplate.
```

### IBlackboard

```csharp
public interface IBlackboard
{
    void SetValue<T>(string key, T value);
    (bool found, T value) TryGet<T>(string key);
    void Clear();
    IReadOnlyCollection<string> GetKeys();
    IReadOnlyDictionary<string, TypedData> SerializeAll();
    void DeserializeAll(IReadOnlyDictionary<string, TypedData> data);
}
```

### ISndContext

ISndContext is the unified facade interface received by strategy hooks. It does not inherit any role interface; all capabilities are accessed through 10 typed companion properties. Namespace `Origo.Core.Snd`.

```csharp
public interface ISndContext
{
    void Bootstrap();
    string SaveRootPath { get; }
    string InitialSaveRootPath { get; }
    string EntryConfigPath { get; }

    ISndBlackboardAccess Blackboard { get; }
    ISndDeferredActions Deferred { get; }
    ISndTemplateAccess Template { get; }
    ISndConsoleAccess ConsoleAccess { get; }
    ISndStateMachineAccess StateMachines { get; }
    ISndSaveOperations Save { get; }
    ISndLifecycleOperations Lifecycle { get; }
    ISndFileAccess FileAccess { get; }
    ISndArchiveFileAccess ArchiveFileAccess { get; }
    IStateMachineContext StateMachineContext { get; }
}

// === Role Interface Overview (type definitions of companion properties) ===

// Blackboard access
public interface ISndBlackboardAccess {
    IBlackboard SystemBlackboard { get; }
    IBlackboard? ProgressBlackboard { get; }
}

// Deferred actions
public interface ISndDeferredActions {
    void EnqueueBusinessDeferred(Action action);
    int GetPendingPersistenceRequestCount();
    bool IsPersistenceIdle { get; }
}

// Templates
public interface ISndTemplateAccess {
    SndMetaData CloneTemplate(string templateKey, string? overrideName = null);
    IReadOnlyList<SndMetaData> ResolveMetaListFromJsonArray(DataSourceNode root);
    IReadOnlyList<SndMetaData> LoadMetaListFromFile(string filePath);
    void LoadTemplates(string mapFilePath);
    void LoadSceneAliases(string mapFilePath);
}

// Console
public interface ISndConsoleAccess {
    bool TrySubmitConsoleCommand(string commandLine);
    long SubscribeConsoleOutput(Action<string> onLine);
    void UnsubscribeConsoleOutput(long subscriptionId);
}

// State machines
public interface ISndStateMachineAccess {
    IStateMachineContainer? GetProgressStateMachines();
}

// Save operations
public interface ISndSaveOperations {
    IReadOnlyList<string> ListSaves();
    IReadOnlyList<SaveMetaDataEntry> ListSavesWithMetaData();
    void DeleteSave(string saveId);
    void RequestLoadGame(string saveId);
    void RequestSaveGame(string newSaveId);
    string RequestSaveGameAuto(string? newSaveId = null);
    void SetContinueTarget(string saveId);
    void RequestSwitchForegroundLevel(string newLevelId);
    void RegisterSaveMetaContributor(ISaveMetaContributor contributor);
    void RegisterSaveMetaContributor(Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> contribute);
}

// Lifecycle entry points
public interface ISndLifecycleOperations {
    bool IsBootstrapCompleted { get; }
    bool HasContinueData();
    bool RequestContinueGame();
    void RequestLoadInitialSave();
    void RequestLoadMainMenuEntrySave();
}

// File access (via DataSource boundary, with built-in parsing)
public interface ISndFileAccess {
    DataSourceNode ReadFile(string path);
    void WriteFile(string path, DataSourceNode node, bool overwrite = true);
    bool FileExists(string path);
    T ReadObject<T>(string path);
    void WriteObject<T>(string path, T value, bool overwrite = true);
}

// In-save file access (paths relative to the save's active extra/ subdirectory, following save lifecycle)
public interface ISndArchiveFileAccess {
    DataSourceNode ReadFile(string relativePath);
    void WriteFile(string relativePath, DataSourceNode node, bool overwrite = true);
    bool FileExists(string relativePath);
    T ReadObject<T>(string relativePath);
    void WriteObject<T>(string relativePath, T value, bool overwrite = true);
    void DeleteFile(string relativePath);
}
```

### ISessionManager / ISessionRun

Namespace `Origo.Core.Abstractions.Lifecycle`.

```csharp
public interface ISessionManager
{
    const string ForegroundKey = "__foreground__";
    bool CanCreateSessions { get; }
    ISessionRun? ForegroundSession { get; }
    IReadOnlyCollection<string> Keys { get; }
    ISessionRun? TryGet(string key);
    bool Contains(string key);
    ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false);
    void DestroySession(string key);
    void ProcessAllSessions(double delta, bool includeForeground = false);
    void KillPendingAllSessions();
}
```

```csharp
public interface ISessionRun
{
    IBlackboard SessionBlackboard { get; }
    string LevelId { get; }
    bool IsFrontSession { get; }
    IStateMachineContainer GetSessionStateMachines();
    ISessionManager SessionManager { get; }

    // ── Entity operations (session scope) ──
    ISndEntity? FindByName(string name);
    IReadOnlyCollection<ISndEntity> GetEntities();
    ISndEntity Spawn(SndMetaData meta);
    void SpawnMany(params SndMetaData[] metaList);
    void RequestKillEntity(string entityName);
}
```

### IStateMachine / IStateMachineContext / IStateMachineContainer

```csharp
public interface IStateMachine
{
    string MachineKey { get; }
    string PushStrategyIndex { get; }
    string PopStrategyIndex { get; }
    void Push(string value);
    bool TryPopRuntime(out string? popped);
    bool TryPopOnQuit(out string? popped);
    (bool found, string? top) Peek();
    IReadOnlyList<string> Snapshot();
    void FlushAfterLoad();
    // internal: void RestoreStackWithoutHooks(IReadOnlyList<string> stackBottomToTop) — framework load pipeline only
}
```

```csharp
public interface IStateMachineContext : ISndBlackboardAccess, ISndDeferredActions
{
    // Inherited from ISndBlackboardAccess:
    //   IBlackboard SystemBlackboard { get; }
    //   IBlackboard? ProgressBlackboard { get; }
    // Inherited from ISndDeferredActions:
    //   void EnqueueBusinessDeferred(Action action);
    //   int GetPendingPersistenceRequestCount();
    //   bool IsPersistenceIdle { get; }

    IBlackboard? SessionBlackboard { get; }
    ISndSceneReadAccess SceneAccess { get; }
}
```

```csharp
public interface IStateMachineContainer
{
    IStateMachine CreateOrGet(string machineKey, string pushStrategyIndex, string popStrategyIndex);
    bool TryGet(string machineKey, out IStateMachine? machine);
    void Remove(string machineKey);
    void Clear();
}
```

## Initialization Timeline

Startup belongs to the host entry node; a consumer subclasses `OrigoDefaultEntry` (or `OrigoAutoHost`) and only interacts with the hooks below:

```csharp
public partial class MyEntry : OrigoDefaultEntry
{
    // Runs after the runtime exists and before Bootstrap() freezes strategy registration.
    protected override void ConfigureStrategies(ISndWorldAccess world)
        => world.RegisterStrategy(() => new MyStrategy());

    protected override void ConfigureSaveMetadataContributors(ISndContext context) { /* meta.map contributors */ }

    public override void _Ready()
    {
        // Export overrides must be assigned before base._Ready(); Godot re-applies scene defaults first.
        ConfigPath = "res://origo/entry/entry.json";
        base._Ready();   // creates runtime + scene host + ISndContext, then calls SndContext.Bootstrap()
    }
}
```

- What the consumer must do: supply the entry configuration (`ConfigPath`, `SaveRootPath`, `InitialSaveRootPath`, alias/template map paths) and make sure every strategy is registered before bootstrap — through `AutoDiscoverStrategies`, `ConfigureStrategies`, or `ISndWorldAccess.RegisterStrategy`; `ConfigureSaveMetadataContributors` and `RegisterCustomConverters` cover display metadata and custom converters.
- What the consumer observes after `base._Ready()` returns: `Runtime`, `Context`, `ConsoleInput`, and `ConsoleOutputChannel` are available on the node, strategy registration is sealed, the configured scene aliases and templates are loaded, and the main-menu entry load is enqueued as a deferred action that executes when `IOrigoFrameDriver.DriveFrame(delta)` flushes the frame.
- `_Process` drives `Runtime.DriveFrame(delta)`. A failure inside `_Ready` and any frame driven after a failed bootstrap throw instead of running a partially initialized runtime.

The internal adapter sequence (kernel port stages, scene-host binding, and the differing Core host path) is owned by [Origo.GodotAdapter → Startup Flow](../Origo.GodotAdapter/README.en.md#startup-flow).

## Complete Strategy Example

```csharp
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

// Entity strategy: initialize data, and mount an observer strategy to respond to hp changes
[StrategyIndex("example.simple_health", Before = new[] { "example.damage_resolution" })]
public sealed class SimpleHealthStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("hp", 100);
        entity.SetData("max_hp", 100);

        // Mount a self-observation strategy (bindings persist with the entity, auto-restore on load, no manual reconnect/unsubscribe needed)
        entity.MountObserverStrategy(entity.Name, "my_game.hp_watcher");
    }

    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, hp) = entity.TryGetData<int>("hp");
        if (!found) return;

        entity.SetData("hp", hp - 1);
    }
}
```

The `MountObserverStrategy` call above mounts `my_game.hp_watcher`, the observer strategy the [SND Entity Model data-observation example](snd-entity-model.en.md#data-observation) defines: an `ObserverStrategyBase` marked `[ObserveData("hp")]` whose `OnDataChanged` requests a save when hp reaches zero.

## File Access Example

```csharp
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Snd.Strategy;

[StrategyIndex("example.config_loader")]
public sealed class ConfigLoadStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        // Read JSON config as a DataSourceNode tree
        if (!ctx.FileAccess.FileExists("res://configs/enemies.json"))
            return;
        var cfg = ctx.FileAccess.ReadFile("res://configs/enemies.json");
        var baseHp = cfg["orc"]["base_hp"].As<int>();
        entity.SetData("orc_base_hp", baseHp);

        // Strongly-typed read/write
        var prefs = ctx.FileAccess.ReadObject<PlayerPrefs>("user://prefs.json");
        prefs.Volume = 0.8f;
        ctx.FileAccess.WriteObject("user://prefs.json", prefs);
    }
}
```

## Cross-Entity Observation Example

Observer strategies can be mounted on **other** entities, responding to target data changes and unmount:

```csharp
// Observer strategy: watch the boss's hp
[StrategyIndex("enemy.boss_hp_watcher")]
[ObserveData("hp")]
public sealed class BossHpWatcherStrategy : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        // entity = observer, target = the observed boss
        if (newValue.TryGetInt32(out var hp)) entity.SetData("boss_hp", hp);
    }

    public override void OnUnmounted(ISndEntity entity, ISndContext ctx, ISndEntity target)
    {
        // Fired when target dies or is explicitly unmounted
        entity.SetData("boss_alive", false);
    }
}

// Entity strategy: resolve the boss and mount cross-entity observation
[StrategyIndex("enemy.watcher")]
public sealed class EnemyWatcherStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        var boss = entity.OwningSession.FindByName("boss");
        if (boss is null) return;

        // Cross-entity observation: bindings persist with the entity, auto-restore on load; auto-unmount when boss or this entity dies
        entity.MountObserverStrategy(boss, "enemy.boss_hp_watcher");
    }
}
```

## Testing Patterns

```csharp
// Complete strategy test template
[Test]
public void MyStrategy_AfterSpawn_InitializesData()
{
    var harness = StrategyTestScenario
        .For<MyStrategy>("test.strategy")
        .WithEntityName("test_entity")
        .WithData("hp", 100)
        .Build();

    var hp = harness.GetEntityData<int>("hp");
    Assert.That(hp, Is.EqualTo(100));
}

[Test]
public void MyStrategy_Process_UpdatesState()
{
    var harness = StrategyTestScenario
        .For<MyStrategy>("test.strategy")
        .WithData("counter", 0)
        .Build();

    harness.RunFrame();

    var counter = harness.GetEntityData<int>("counter");
    Assert.That(counter, Is.GreaterThan(0));
}

// Architecture guardrail test
[Test]
public void Core_ContainsNoGodotReferences()
{
    var coreAssembly = typeof(SndEntity).Assembly;
    var adapterAssembly = typeof(GodotSndManager).Assembly;

    foreach (var type in coreAssembly.GetTypes())
    {
        Assert.That(type.Namespace, Does.Not.Contain("Godot"));
    }
}
```

## Auxiliary Interfaces

### INodeHandle (Node Abstraction)

```csharp
// Implemented by the adapter layer; Core does not hold engine node references
// Obtain the concrete node via SndEntityNodeExtensions.GetNativeNode() extension method
// GetNativeNode() → returns the Godot Node object (GodotAdapter layer)
```

### INodeFactory (Node Factory)

```csharp
public interface INodeFactory
{
    INodeHandle Create(string logicalName, string resourceId);
}
```

`Create` instantiates the engine resource identified by `resourceId` and attaches it to the host under `logicalName`.

### IConsoleInputSource / IConsoleOutputChannel (Console I/O)

```csharp
public interface IConsoleInputSource
{
    bool TryDequeueCommand([NotNullWhen(true)] out string? line);
    void Enqueue(string line);
    void Clear();
}

public interface IConsoleOutputChannel
{
    long Subscribe(Action<string> listener);
    bool Unsubscribe(long subscriptionId);
    void Publish(string line);
}
```

The adapter layer submits commands via `Enqueue`; Core consumes them per-frame via `TryDequeueCommand`, and `Clear` drops every pending command. `IConsoleOutputChannel` is a publish channel with no local history: `Subscribe` registers a listener and returns its subscription id, `Unsubscribe` cancels an id (false when it does not exist), and `Publish` sends a line to every subscriber.

The Godot adapter host creates the input source and exposes it through `OrigoAutoHost.ConsoleInput`; `ConsoleBridgeServer` and custom command handlers consume it.

---

## Common Pitfalls

1. **Do not store instance state in strategies** — all mutable data goes into entity Data
2. **Check `found` before using TryGetData** — value type default(T) is unreliable
3. **AfterLoad is a recovery hook** — it is not a substitute for AfterSpawn
4. **Data modified in BeforeSave will be written to the save** — this is by design, for flushing
5. **Deferred actions execute automatically after Process** — already handled in RunFrame
6. **Background sessions have no render nodes** — do not perform node-dependent operations
7. **Use observer strategies for data change observation** — implement `ObserverStrategyBase` + `[ObserveData("key")]`, mount via `MountObserverStrategy`, rather than subscribing delegates on entities
8. **Observer bindings persist with entities** — `ObserverIndices` are written to saves; wiring auto-restores on load; no need to manually re-mount in `AfterLoad`
9. **Observer bindings auto-unmount on death** — the framework uniformly fires `OnUnmounted` and unmounts bindings on entity exit/death; strategies generally do not need manual `UnmountObserverStrategy`
10. **File I/O must go through ISndFileAccess, do not use IFileSystem directly** — all file content read/write is unified through the `IDataSourceIoGateway` boundary; suffix routing and parsing are handled by the framework. `ISndFileAccess.ReadFile` / `ReadObject<T>` already include parsing; strategies should not parse raw text themselves

## Related Documents

- [SND Entity Model](snd-entity-model.en.md) — Detailed strategy writing guide
- [Strategy Testing](strategy-testing.en.md) — Detailed StrategyTestScenario usage

---
[↑ Back to usage](README.en.md)
