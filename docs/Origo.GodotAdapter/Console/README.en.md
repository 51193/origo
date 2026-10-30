# Console

> [↑ Back to Origo.GodotAdapter](../README.en.md) · [↔ Core: Runtime/Console](../../Origo.Core.Kernel/Runtime/Console/README.en.md)

## Overview

Console command extensions for the Godot adapter layer. An adapter-layer command handler base class (providing stable runtime access through `IOrigoRuntime`), plus three Godot-specific commands — `press_button` to simulate Button clicks, `tree_debug` to print entity node trees, and `camera_view` to output camera coordinate information (reporting separately when entities exist but none of their nodes are visible).

## Files

| File | Responsibility |
|------|------|
| `CommandHandlerBase.cs` | Adapter-layer command handler base class, holds an `IOrigoRuntime` reference, validates argument count, and resolves named Godot entities for commands that require them |
| `PressButtonCommandHandler.cs` | Godot-specific command: finds and emits Button.Pressed signal by entity + path |
| `TreeDebugCommandHandler.cs` | Godot-specific command: prints the complete Godot node tree of an entity (including dynamically created child nodes) |
| `CameraViewCommandHandler.cs` | Godot-specific command: displays screen coordinates and depth of all visible entity nodes from the active camera's perspective (engine visibility flag + camera frustum) |
| `ProjectionHelper.cs` | Camera projection math utility: world coordinates → screen pixel coordinates (frustum clipping) |

## Module Details

### CommandHandlerBase

Inherits from Core's `ConsoleCommandHandlerBase` (argument-count validation and error messaging come from the base class) and additionally holds an `IOrigoRuntime` reference directly, simplifying command implementation on the Godot side. Custom commands register through `IOrigoRuntime.RegisterConsoleCommandHandler`.

### PressButtonCommandHandler

```
press_button <entity> <path>
```

Flow:
1. `TryResolveGodotEntity` in `CommandHandlerBase` finds the foreground-session entity and verifies that it is a `GodotSndEntity`
2. Resolves `godotEntity.GetNodeOrNull<Button>(path)` and requires `godotEntity.IsAncestorOf(button)`; an absolute or `../` path that escapes the entity is rejected
3. `button.EmitSignal(BaseButton.SignalName.Pressed)` simulates the press

### TreeDebugCommandHandler

```
tree_debug <entity>
```

Flow:
1. `TryResolveGodotEntity` in `CommandHandlerBase` finds the foreground-session entity and verifies that it is a `GodotSndEntity`
2. Recursively traverses the entity's Godot node tree, printing `[Type] "Name"` for each node
3. Outputs the full node tree for debugging path resolution issues

### CameraViewCommandHandler

```
camera_view
```

Flow:
1. Gets SceneTree → Root Viewport via `Engine.GetMainLoop()`
2. `viewport.GetCamera3D()` gets the active camera
3. Iterates all `GodotSndEntity` instances in the foreground session
4. Recursively traverses each entity's child nodes, skipping nodes the engine would not render (the node or an ancestor of the same render type has `Visible = false`, i.e. `IsVisibleInTree()` is false):
   - `Node3D` → computes 2D screen coordinates and depth via `ProjectionHelper.ProjectWorldToScreen`
   - `Control` → reads `GlobalPosition` directly (UI space)
5. Output format: `entity / node [Type] screen=(X, Y) depth=D`

> Occlusion detection is not yet implemented; only the engine visibility flag (`IsVisibleInTree()`) and camera-frustum culling are applied. Depth values can be used for manual sorting to determine front-to-back order.

## Design Decisions

### Why the adapter layer needs its own CommandHandlerBase

Core `ConsoleCommandHandlerBase` and adapter commands both need stable runtime access for sessions, entities, and the world. The adapter layer base class provides a consistent `Runtime` property access pattern, avoiding repeated injection boilerplate in every Godot command handler, and registration goes through `IOrigoRuntime.RegisterConsoleCommandHandler`.

### Why entity lookup and Godot type checking are shared

`ForegroundSession.FindByName` returns the abstract `ISndEntity` interface, but both `press_button` and `tree_debug` need Godot node access. `CommandHandlerBase.TryResolveGodotEntity` owns the shared lookup and type check, so a missing entity or a pure in-memory entity (for example, `StubSndEntity`) produces the same clear error before either command accesses Godot nodes.

---
[↑ Back to Origo.GodotAdapter](../README.en.md)
