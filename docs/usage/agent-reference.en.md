<!-- docsync-pair: usage/agent-reference -->
<!-- docsync-revision: 9 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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
    void FlushDeferredActionsForCurrentFrame();
    int GetPendingPersistenceRequestCount();
}

// Templates
public interface ISndTemplateAccess {
    SndMetaData CloneTemplate(string templateKey, string? overrideName = null);
}

// Console
public interface ISndConsoleAccess {
    bool TrySubmitConsoleCommand(string commandLine);
    void ProcessConsolePending();
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
    //   void FlushDeferredActionsForCurrentFrame();
    //   int GetPendingPersistenceRequestCount();

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

```
OrigoAutoHost._Ready()
│
├── 1. Create IFileSystem (GodotFileSystem)
├── 2. Create ILogger (GodotLogger)
├── 3. Create GodotSndManager
├── 4. Register TypeStringMapping + Converters (BCL + Godot types)
├── 5. Create PersistentBlackboard → LoadFromDisk
├── 6. Create ConsoleInputBuffer + ConsoleOutputChannel
├── 7. Create OrigoRuntime
│   ├── SndWorld (strategy pool + converter registry)
│   ├── SystemRun (holds SystemBlackboard)
│   └── OrigoConsole (command routing)
│
├── 8. BindRuntimeDependencies (World + Logger to SndManager)
│
└── OrigoDefaultEntry._Ready() [override]
    ├── 9. Register adapter-layer command handlers (press_button, tree_debug)
    ├── 10. Create SndContext (inject Runtime + FileSystem + saveRoot + config)
    ├── 11. SndManager.BindContext(context)
    ├── 12. ConfigureSaveMetadataContributors(context)
    └── 13. SndContext.Bootstrap()
          ├── 13a. ConfigureConverters
          ├── 13b. OrigoAutoInitializer.DiscoverAndRegisterStrategies (reflection scan)
          ├── 13c. LoadSceneAliases + LoadTemplates
          └── 13d. RequestLoadMainMenuEntrySave → FlushDeferredActions
```

## Complete Strategy Example

```csharp
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

// Entity strategy: initialize data, and mount an observer strategy to respond to hp changes
[StrategyIndex("example.simple_health", Priority = 6205)]
public sealed class SimpleHealthStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("hp", 100);
        entity.SetData("max_hp", 100);

        // Mount a self-observation strategy (bindings persist with the entity, auto-restore on load, no manual reconnect/unsubscribe needed)
        entity.MountObserverStrategy(entity.Name, "example.hp_watcher");
    }

    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, hp) = entity.TryGetData<int>("hp");
        if (!found) return;

        entity.SetData("hp", hp - 1);
    }
}

// Observer strategy: respond to hp data changes
[StrategyIndex("example.hp_watcher")]
[ObserveData("hp")]
public sealed class HpWatcherStrategy : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        if (newValue.TryGetInt32(out var hp) && hp <= 0)
            ctx.Save.RequestSaveGame("entity_died");
    }
}
```

## File Access Example

```csharp
using Origo.Core.Abstractions.Entity;
using Origo.Core.DataSource;
using Origo.Core.Snd.Strategy;

[StrategyIndex("example.config_loader")]
public class ConfigLoadStrategy : LifecycleStrategyBase
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
    INodeHandle Create(ISndEntity parentEntity, string resourceId, string nodeName);
}
```

### IConsoleInputSource / IConsoleOutputChannel (Console I/O)

```csharp
public interface IConsoleInputSource
{
    bool TryDequeue(out string? command);
}

public interface IConsoleOutputChannel
{
    void Publish(string message);
}
```

Created by `OrigoAutoHost` for use by `ConsoleBridgeServer` and custom command handlers.

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
