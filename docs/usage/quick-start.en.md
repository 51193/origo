<!-- docsync-pair: usage/quick-start -->
<!-- docsync-revision: 13 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Quick Start

> [↑ Back to usage](README.en.md)

## Overview

The shortest path to integrating the Origo framework into a Godot 4 project.

## Prerequisites

- Godot 4.7 project (.NET version)
- .NET 10.0 SDK (or later)
- Project already references Origo (via NuGet package or ProjectReference)

## Steps

### 1. Create Folder Structure

Create the following folder structure under the Godot project root:

```
res://origo/
├── entry/
│   └── entry.json             # Entry configuration
├── maps/
│   ├── scene_aliases.map      # Scene alias mapping
│   └── snd_templates.map      # Template definitions
└── initial/                   # Initial save data
    └── ...                    # Initial level JSON
```

### 2. Create Entry Node

Create a Node in a Godot scene and attach the `OrigoDefaultEntry` script to it:

1. Create a new scene, select `Node` as the root node type
2. In the Node's script property, select `OrigoDefaultEntry.cs`
3. Save the scene (e.g., `Main.tscn`)

### 3. Configure entry.json

Create `res://origo/entry/entry.json`:

```json
{
  "levels": {
    "main_menu": {
      "snd_scene": "res://origo/initial/levels/main_menu/snd_scene.json",
      "type": "main_menu"
    }
  },
  "main_menu_level": "main_menu"
}
```

### 4. Write a Strategy

Create a Strategy:

```csharp
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd.Strategy;

[StrategyIndex("my_game.health")]
public sealed class HealthInitStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("hp", 100);
        entity.SetData("max_hp", 100);
    }

    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, hp) = entity.TryGetData<int>("hp");
        if (found && hp <= 0)
            ctx.Save.RequestSaveGame("game_over");
    }
}
```

### 5. Define Entity Templates

Each key in `snd_templates.map` points to a file containing a **single
SndMetaData object**, for example `res://origo/templates/player.json`:

```json
{
  "name": "player_template",
  "node": { "pairs": {} },
  "strategy": {
    "lifecycle_indices": ["my_game.health"],
    "active_indices": [],
    "observer_indices": []
  },
  "data": {
    "pairs": {
      "hp": { "type": "Int32", "data": 100 },
      "max_hp": { "type": "Int32", "data": 100 }
    }
  }
}
```

Prefer `SndMetaFluentBuilder` for programmatic metadata. New builders emit
spawn-ready empty Node/Strategy/Data sections by default:

```csharp
var marketMeta = new SndMetaFluentBuilder("MarketSim")
    .AddLifecycleStrategy("game.market_sim")
    .Build();
session.Spawn(marketMeta);
```

Use `AddObserverBinding(target, observerIndices)` for observer bindings. It
generates the expected `{ "target_entity": ["observer.index"] }` shape, so
hand-authored `observer_indices` JSON cannot accidentally use the wrong form:

```csharp
var watcher = new SndMetaFluentBuilder("Watcher")
    .AddObserverBinding("Player", "watch.hp")
    .Build();
```

### 6. Spawning Template Entities at Runtime

Templates are loaded during `Bootstrap` from `SndTemplateMapPath`. Strategies or game code spawn entities like this:

```csharp
// Spawn one template entity: deep-clone and rename, then use the unified session spawn pipeline
var heroMeta = ctx.Template.CloneTemplate("player_template", "Player_01");
var hero = entity.OwningSession.Spawn(heroMeta);

// Resolve an entity list from a JSON array file (templateKey/sndName shorthand supported), then batch spawn
var waveMeta = ctx.Template.LoadMetaListFromFile(
    "res://origo/initial/levels/main_menu/snd_scene.json");
entity.OwningSession.SpawnMany([.. waveMeta]);

// Reload template/alias maps at runtime (for mods or hot config reload)
ctx.Template.LoadTemplates("res://origo/maps/snd_templates.map");
ctx.Template.LoadSceneAliases("res://origo/maps/scene_aliases.map");
```

### 7. Run

Run the Godot project. `OrigoDefaultEntry._Ready()` will automatically:
1. Create `OrigoRuntime`
2. Discover and register all `[StrategyIndex]` strategies
3. Create `SndContext`
4. Load aliases and templates
5. Load the entry level from `initial/`

`OrigoDefaultEntry.Context` is available after `_Ready()` for presentation
queries such as save listing, continue availability, and lifecycle entry points:

```csharp
public override void _Ready()
{
    base._Ready();
    var entries = Context.Save.ListSavesWithMetaData();
    // Build a save-selection UI from entries
}
```

## Runtime Flow

```
OrigoAutoHost._Ready()
  → Create GodotFileSystem, GodotLogger
  → Create GodotSndManager
  → Register TypeStringMapping (BCL + Godot types) + DataSourceConverters
  → Create PersistentBlackboard → LoadFromDisk
  → Create ConsoleInputBuffer + ConsoleOutputChannel
  → Create OrigoRuntime (containing SndWorld + SystemRun + OrigoConsole)
  → BindRuntimeDependencies → SndManager

OrigoDefaultEntry._Ready()
  → Register adapter-layer command handlers (press_button, tree_debug, camera_view)
  → Create SndContext
  → Context = sndContext (exposed to presentation)
  → SndManager.BindContext(context)
  → ConfigureSaveMetadataContributors(context)
  → SndContext.Bootstrap()
      → DiscoverAndRegisterStrategies
      → LoadSceneAliases + LoadTemplates
      → RequestLoadMainMenuEntrySave → Start game

Per frame: _Process → IOrigoFrameDriver.DriveFrame(delta)
  → Snd.ProcessAll(delta)          # Entity frame processing
  → FlushEndOfFrameDeferred()      # Business queue → KillPendingEntities → System queue
  → Core-internal: Console.ProcessPending()  # Console command processing
```

## Next Steps

- [Architecture Overview](architecture-overview.en.md) — Understand Origo's overall design
- [SND Entity Model](snd-entity-model.en.md) — Learn how to write strategies
- [Strategy Testing](strategy-testing.en.md) — Test strategies using StrategyTestScenario

---
[↑ Back to usage](README.en.md)
