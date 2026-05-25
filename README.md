# Origo

[简体中文](README.zh-CN.md)

**Origo** is a lightweight, platform-agnostic C# game framework.
It uses the **SND (Strategy-Node-Data)** model and isolates engine code through adapters.

## Core Features

- **Engine-free Core**: `Origo.Core` has no engine dependency.
- **SND Entity Model**: behavior (`Strategy`), view (`Node`), and state (`Data`) are separated.
- **Stateless Strategy Pool**: strategies are shared and validated at registration.
- **Layered Runtime**: `SystemRun -> ProgressRun -> SessionManager -> SessionRun`.
- **Foreground/Background Session Parity**: background sessions run the same strategy logic and lifecycle as foreground sessions.
- **Built-in Save Flow**: current workspace + snapshot slots.
- **Official Godot 4 Adapter**: `Origo.GodotAdapter` for bootstrap and runtime integration.

## Special Capability: Background Session

Origo supports creating background sessions that execute the same gameplay logic path as the foreground session.
This means you can run AI simulation, procedural generation, or long-running world updates in memory while keeping exactly the same strategy behavior and data contracts.

Create one with `ctx.SessionManager.CreateBackgroundSession(key, levelId)` and process it through the same session pipeline.

## 5-Minute Setup (Godot 4)

### 1) Reference projects

#### Option A: NuGet (recommended)

```xml
<PackageReference Include="Origo.Core" />
<PackageReference Include="Origo.GodotAdapter" />
```

> **NuGet packages are published via GitHub Releases.** Download the `.nupkg` files
> from the [latest release](https://github.com/51193/origo.stable/releases/latest),
> place them in `./packages/origo/`, and configure `nuget.config` to add the local
> package source (a sample `nuget.config` is included in the release assets).
>
> ```xml
> <?xml version="1.0" encoding="utf-8"?>
> <!-- nuget.config (place in your Godot project root) -->
> <configuration>
>   <packageSources>
>     <add key="origo-local" value="./packages/origo/" />
>   </packageSources>
> </configuration>
> ```
>
> Commit `nuget.config` to your repository so all contributors share the same
> package source. Add `packages/` to your `.gitignore` — `.nupkg` binaries should
> not be tracked by version control.

#### Option B: Project reference

```xml
<ProjectReference Include="../Origo.Core/Origo.Core.csproj" />
<ProjectReference Include="../Origo.GodotAdapter/Origo.GodotAdapter.csproj" />
```

> **Godot `[GlobalClass]` resolution**: Godot resolves `[GlobalClass]` by script
> resource path and may fail to locate `OrigoDefaultEntry` regardless of which
> option you chose above (NuGet or ProjectReference). Workaround: create a bridge
> class in your project:
>
> ```csharp
> [GlobalClass]
> public partial class MyOrigoEntry : GodotAdapter.Bootstrap.OrigoDefaultEntry { }
> ```
>
> Then point your `.tscn` node script to `MyOrigoEntry` instead.

### 2) Minimal folder layout

```text
res://origo/
  entry/entry.json
  maps/scene_aliases.map
  maps/snd_templates.map
  initial/
```

### 3) Add Origo entry node

Attach `OrigoDefaultEntry` to your startup scene, then set:

- `ConfigPath`
- `SceneAliasMapPath`
- `SndTemplateMapPath`
- `SaveRootPath`
- `InitialSaveRootPath`

### 4) Write one strategy

```csharp
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;

[StrategyIndex("game.player_move")]
public sealed class PlayerMoveStrategy : EntityStrategyBase
{
    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, speed) = entity.TryGetData<float>("speed");
        if (!found) return;
        // movement logic...
    }
}
```

### 5) Define one entity

```json
{
  "name": "Player",
  "node": { "pairs": { "sprite": "player_sprite" } },
  "strategy": { "indices": ["game.player_move"] },
  "data": { "pairs": { "speed": { "type": "Single", "data": 200.0 } } }
}
```

## Typical Runtime Flow

1. `OrigoDefaultEntry` boots runtime.
2. Load entry save/config.
3. Spawn entities from metadata.
4. Execute strategy `Process` each frame.
5. Save to `current/`, then snapshot to `save_xxx/`.

## Repository Layout

```text
Origo.Core/
Origo.GodotAdapter/
Origo.Core.Tests/
Origo.GodotAdapter.Tests/
scripts/
Origo.sln
```

## Test

Run the same pipeline as CI from repository root:

```bash
bash scripts/ci.sh
```

Quick test-only run:

```bash
bash scripts/run-test.sh
```

## License

MIT. See [LICENSE](LICENSE).
