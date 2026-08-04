<!-- docsync-pair: Origo.GodotAdapter/Console/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Console

> [↑ Back to Origo.GodotAdapter](../README.en.md) · [↔ Core: Runtime/Console](../../Origo.Core/Runtime/Console/README.en.md)

## Overview

Console command extensions for the Godot adapter layer. An adapter-layer command handler base class (providing an `OrigoRuntime` reference), plus three Godot-specific commands — `press_button` to simulate Button clicks, `tree_debug` to print entity node trees, and `camera_view` to output camera coordinate information.

## Files

| File | Responsibility |
|------|------|
| `CommandHandlerBase.cs` | Adapter-layer command handler base class, holds `OrigoRuntime` reference, validates argument count |
| `PressButtonCommandHandler.cs` | Godot-specific command: finds and emits Button.Pressed signal by entity + path |
| `TreeDebugCommandHandler.cs` | Godot-specific command: prints the complete Godot node tree of an entity (including dynamically created child nodes) |
| `CameraViewCommandHandler.cs` | Godot-specific command: displays screen coordinates and depth of all entity nodes from the active camera's perspective |
| `ProjectionHelper.cs` | Camera projection math utility: world coordinates → screen pixel coordinates (frustum clipping) |

## Module Details

### CommandHandlerBase

Inherits from Core's `ConsoleCommandHandlerBase` (argument-count validation and error messaging come from the base class) and additionally holds an `OrigoRuntime` reference directly, simplifying command implementation on the Godot side.

### PressButtonCommandHandler

```
press_button <entity> <path>
```

Flow:
1. `Runtime.SessionManager.ForegroundSession?.FindByName(entity)` finds the entity
2. Checks if the entity is of type `GodotSndEntity`
3. Uses `godotEntity.GetNodeOrNull<Button>(path)` to find the Button node
4. `button.EmitSignal(BaseButton.SignalName.Pressed)` simulates the press

### TreeDebugCommandHandler

```
tree_debug <entity>
```

Flow:
1. `Runtime.SessionManager.ForegroundSession?.FindByName(entity)` finds the entity
2. Checks if the entity is of type `GodotSndEntity`
3. Recursively traverses the entity's Godot node tree, printing `[Type] "Name"` for each node
4. Outputs the full node tree for debugging path resolution issues

### CameraViewCommandHandler

```
camera_view
```

Flow:
1. Gets SceneTree → Root Viewport via `Engine.GetMainLoop()`
2. `viewport.GetCamera3D()` gets the active camera
3. Iterates all `GodotSndEntity` instances in the foreground session
4. Recursively traverses each entity's child nodes:
   - `Node3D` → computes 2D screen coordinates and depth via `ProjectionHelper.ProjectWorldToScreen`
   - `Control` → reads `GlobalPosition` directly (UI space)
5. Output format: `entity / node [Type] screen=(X, Y) depth=D`

> Occlusion/culling detection is not yet implemented; currently shows all visible nodes within the frustum. Depth values can be used for manual sorting to determine front-to-back order.

## Design Decisions

### Why the adapter layer needs its own CommandHandlerBase

Core's `ConsoleCommandHandlerBase` requires subclasses to hold a reference to `OrigoRuntime`. The adapter layer base class provides a consistent `Runtime` property access pattern, avoiding repeated injection boilerplate in every Godot command handler.

### Why PressButton needs Godot entity type checking

`Runtime.SessionManager.ForegroundSession?.FindByName` returns the abstract `ISndEntity` interface, but `GetNodeOrNull<Button>` is a method on `Godot.Node`. Runtime checks ensure type safety — if the entity is a pure in-memory entity (e.g., `StubSndEntity`), a clear error message is given early rather than a NullReferenceException.

---
[↑ Back to Origo.GodotAdapter](../README.en.md)
