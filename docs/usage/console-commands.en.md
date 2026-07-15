<!-- docsync-pair: usage/console-commands -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Console Commands

> [↑ Back to usage](README.en.md)
> [↔ Related Tests: Console](../Origo.Core.Tests/Console.en.md)

## Overview

Origo's built-in console command system. Supports executing commands through the Godot console or TCP bridging (`Origo.ConsoleBridge`, port 9876). Commands support positional and named arguments.

## Available Commands

| Command | Arguments | Description |
|---------|-----------|-------------|
| `help` | None | List all available commands with help text |
| `bb_get` | `<layer> <key>` | Read blackboard key value. `layer`: system |
| `bb_set` | `<layer> <key> <value>` | Write a value to the blackboard (auto-infers type) |
| `bb_keys` | `<layer>` | List all keys of a blackboard layer |
| `spawn` | `<name> <template>` | Spawn entity from template |
| `spawn` | `name=<n> template=<t>` | Spawn entity from template (named arguments) |
| `find_entity` | `<name>` | Find entity and show node info |
| `kill_all` | None | Immediately mark all entities as pending kill (executed uniformly at end of frame) |
| `snd_count` | None | Show current entity count |
| `press_button` | `<entity> <path>` | Simulate pressing a Button in a Godot Node (adapter-layer command) |
| `entity_get_data` | `<entity> <key>` | Read entity data value and type |
| `entity_set_data` | `<entity> <key> <value>` | Set entity data (auto-infers type; preserves existing key's type) |
| `invoke_strategy` | `<entity> <index> [input]` | Invoke entity's active strategy and display result |
| `tree_debug` | `<entity>` | Print the entity's Godot scene tree structure (adapter-layer command) |
| `camera_view` | None | Show screen coordinates and depth of all visible entity nodes from the active camera's perspective (adapter-layer command) |

## Command Details

### bb_get

```
> bb_get system core.player.health
[system] core.player.health = 100 (type: Int32)
```

Currently only the `system` layer is supported.

### bb_set

```
> bb_set system debug_mode true
[system] debug_mode = true
```

Type inference rules: integer → Int32, floating point → Single, true/false → Boolean, everything else → String.

### spawn

```
> spawn player template_basic
Spawned 'player' from template 'template_basic'.

> spawn name=enemy template=template_enemy
Spawned 'enemy' from template 'template_enemy'.
```

Mixing positional and named arguments is not supported.

### find_entity

```
> find_entity player
Entity 'player' found. Nodes: [sprite, collider, shadow]
```

### kill_all

```
> kill_all
Marked 12 of 12 entities for kill (deferred to end of frame).
```

Immediately marks all entities in the current scene as pending kill (already-marked entities are skipped). Physical destruction happens uniformly at end of frame (after business deferred queue, before system deferred queue). The command marks by calling `ISndSceneHost.RequestKillEntity` on each entity individually.

### press_button (Adapter Layer)

```
> press_button main_menu_ui StartButton
Pressed button 'StartButton' on entity 'main_menu_ui'.
```

Finds the Button node via `GodotSndEntity.GetNodeOrNull<Button>(path)` and emits the `Pressed` signal.

### entity_get_data

```
> entity_get_data player health
health = 100 (type: Int32)
```

Reads entity data of any type via `TryGetData<object>`, displaying the value and runtime type. Returns `Key '...' not found` if the key does not exist.

### entity_set_data

```
> entity_set_data player health 50
[player] health = 50
```

Type inference rules are the same as `bb_set` (int/float/bool/string). If the key already exists, **the existing key's type is preserved** when writing (e.g., if `hunger` key already has type `float`, `entity_set_data player hunger 15` writes as `Single(15)` rather than `Int32(15)`).

### invoke_strategy

```
> invoke_strategy FoodManager food.get_registry
InvokeStrategy 'food.get_registry' on 'FoodManager': [{"K":"food_001","T":"berry",...}]
```

```
> invoke_strategy TraversabilityManager traversability.is_passable 10,10
InvokeStrategy 'traversability.is_passable' on 'TraversabilityManager': true
```

Invokes an active strategy (`ActiveStrategyBase`) on the specified entity. The first positional argument is the entity name, the second is the strategy index, and the optional third is a JSON input parameter. The result is output as a string. Outputs an error if the entity does not exist or the strategy is not of active type.

### tree_debug (Adapter Layer)

```
> tree_debug Player
Node tree of entity 'Player':
  [GodotSndEntity] "Player"
    [Sprite3D] "CharacterSprite"
    [CollisionShape3D] "Collider"
```

Prints the complete Godot node subtree of the specified entity, outputting node type name and node name. Requires 1 positional argument (entity name). Outputs an error if the entity does not exist or is not a Godot entity.

### camera_view (Adapter Layer)

```
> camera_view
Camera: Camera3D | Viewport: 1920x1080

player / CharacterBody3D [3D] screen=(960, 400) depth=5.2
player / WeaponMesh [3D] screen=(1020, 420) depth=5.5
enemy_01 / Sprite3D [3D] screen=(300, 350) depth=12.1
main_ui / MainMenu [UI] screen=(100, 50)

4 nodes (3 3D, 1 UI) visible from 3 entities.
```

No arguments required. Automatically discovers the active `Camera3D` and iterates all Godot entity child nodes:
- **3D nodes** (`Node3D`): Computes screen coordinates via frustum culling and projection, outputs `(screenX, screenY) depth`
- **UI nodes** (`Control`): Directly reads `GlobalPosition` as screen coordinates

> Occlusion / obstruction detection is not yet implemented; only frustum culling is performed. Depth values can be used for manual front/back judgment.

## Adding Custom Commands

> **Built-in vs. Custom:** All built-in command handlers are `internal sealed class` and are registered internally by `OrigoConsole`. User-defined commands must be declared as `public sealed class`, inherit `ConsoleCommandHandlerBase`, and be registered via `runtime.Console.RegisterHandler()`.

### Core-Layer Commands

Inherit `ConsoleCommandHandlerBase` (`public abstract` class; external projects can directly derive):

```csharp
public sealed class MyCommandHandler : ConsoleCommandHandlerBase
{
    private readonly OrigoRuntime _runtime;
    public MyCommandHandler(OrigoRuntime runtime) { _runtime = runtime; }

    public override string Name => "my_command";
    public override string HelpText => "my_command <arg> — description";
    public override int MinPositionalArgs => 1;
    public override int MaxPositionalArgs => 1;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel output,
        out string? error)
    {
        var arg = invocation.PositionalArgs[0];
        output.Publish($"Executed with arg: {arg}");
        error = null;
        return true;
    }
}
```

Registration: `runtime.Console.RegisterHandler(new MyCommandHandler(runtime));`

### Adapter-Layer Commands

Inherit `Origo.GodotAdapter.Console.CommandHandlerBase` (`public` class):

```csharp
public sealed class MyGodotCommand : CommandHandlerBase
{
    public MyGodotCommand(OrigoRuntime runtime) : base(runtime) { }
    public override string Name => "my_godot_cmd";
    public override string HelpText => "my_godot_cmd — does something";
    public override int MinPositionalArgs => 0;
    public override int MaxPositionalArgs => 0;

    protected override bool ExecuteCore(
        CommandInvocation invocation,
        IConsoleOutputChannel output,
        out string? error)
    {
        // Access Godot-specific API
        output.Publish("Done.");
        error = null;
        return true;
    }
}
```

## TCP Remote Console

```
# Start the bridge server
var server = new ConsoleBridgeServer(consoleInput, consoleOutput);
server.Start();  // Listen on localhost:9876

# Client connection
nc localhost 9876
> snd_count
Snd count: 42.
```

Single-connection mode: only one TCP client is allowed at a time. Console output is pushed to all listeners (including the bridge client) via publish-subscribe.

## Related Documents

- [Quick Start](quick-start.en.md) — How to integrate Origo
- [Origo.ConsoleBridge](../Origo.ConsoleBridge/README.en.md) — Bridge implementation details

---
[↑ Back to usage](README.en.md)
