<!-- docsync-pair: usage/quick-start -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
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
public class HealthInitStrategy : LifecycleStrategyBase
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

Create entity template JSON (via `snd_templates.map` or direct JSON):

```json
[
  {
    "name": "player",
    "strategy": {
      "lifecycle_indices": ["my_game.health"]
    },
    "data": {
      "pairs": {
        "hp": { "type": "Int32", "data": 100 },
        "max_hp": { "type": "Int32", "data": 100 }
      }
    }
  }
]
```

### 6. Run

Run the Godot project. `OrigoDefaultEntry._Ready()` will automatically:
1. Create `OrigoRuntime`
2. Discover and register all `[StrategyIndex]` strategies
3. Create `SndContext`
4. Load aliases and templates
5. Load the entry level from `initial/`

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
  → SndManager.BindContext(context)
  → ConfigureSaveMetadataContributors(context)
  → SndContext.Bootstrap()
      → DiscoverAndRegisterStrategies
      → LoadSceneAliases + LoadTemplates
      → RequestLoadMainMenuEntrySave → Start game

Per frame: _Process → IOrigoFrameDriver.DriveFrame(delta)
  → Snd.ProcessAll(delta)          # Entity frame processing
  → FlushEndOfFrameDeferred()      # Business queue → KillPendingEntities → System queue
  → Console.ProcessPending()       # Console command processing
```

## Next Steps

- [Architecture Overview](architecture-overview.en.md) — Understand Origo's overall design
- [SND Entity Model](snd-entity-model.en.md) — Learn how to write strategies
- [Strategy Testing](strategy-testing.en.md) — Test strategies using StrategyTestScenario

---
[↑ Back to usage](README.en.md)
