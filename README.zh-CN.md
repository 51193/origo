# Origo

[English](README.md)

**Origo** 是一个轻量、平台无关的 C# 游戏框架。  
它基于 **SND（Strategy-Node-Data）** 模型，并通过适配层隔离引擎代码。

## 核心特性

- **无引擎依赖 Core**：`Origo.Core` 不依赖具体引擎。
- **SND 实体模型**：将行为（`Strategy`）、表现（`Node`）、状态（`Data`）解耦。
- **无状态策略池**：策略共享复用，注册时做约束校验。
- **分层运行时**：`SystemRun -> ProgressRun -> SessionManager -> SessionRun`。
- **前后台 Session 同构能力**：后台 Session 与前台 Session 走同一套策略逻辑与生命周期。
- **内置存档流程**：`current/` 工作区 + `save_xxx/` 快照。
- **官方 Godot 4 适配器**：`Origo.GodotAdapter` 负责引导和运行时接入。

## 特殊能力：Background Session

Origo 支持创建后台 Session，并让它执行与前台 Session 完全一致的玩法逻辑路径。
这使你可以在内存中进行 AI 仿真、程序化生成或离屏世界更新，同时保持同一套策略行为与数据契约。

可通过 `ctx.SessionManager.CreateBackgroundSession(key, levelId)` 创建，并接入相同的 Session 处理管线。

## 5 分钟上手（Godot 4）

### 1）引用项目

#### 方案 A：NuGet（推荐）

```xml
<PackageReference Include="Origo.Core" />
<PackageReference Include="Origo.GodotAdapter" />
```

> **NuGet 包通过 GitHub Releases 发布**。请从
> [最新 Release](https://github.com/51193/origo.stable/releases/latest) 下载
> `.nupkg` 文件，放入本地目录，并配置 `nuget.config` 添加本地包源。
> 仓库根目录已提供 [nuget.config](nuget.config) 模板。
>
> ```xml
> <?xml version="1.0" encoding="utf-8"?>
> <!-- nuget.config（放在你的 Godot 项目根目录） -->
> <configuration>
>   <packageSources>
>     <add key="origo-local" value="./packages/origo/" />
>   </packageSources>
> </configuration>
> ```

#### 方案 B：项目引用

```xml
<ProjectReference Include="../Origo.Core/Origo.Core.csproj" />
<ProjectReference Include="../Origo.GodotAdapter/Origo.GodotAdapter.csproj" />
```

### 2）最小目录结构

```text
res://origo/
  entry/entry.json
  maps/scene_aliases.map
  maps/snd_templates.map
  initial/
```

### 3）添加 Origo 入口节点

在启动场景挂载 `OrigoDefaultEntry`，并配置：

- `ConfigPath`
- `SceneAliasMapPath`
- `SndTemplateMapPath`
- `SaveRootPath`
- `InitialSaveRootPath`

> **使用 ProjectReference 时**：若以 `<ProjectReference>` + symlink 方式引用 Origo 源码
> （非 NuGet 包或编译 DLL），Godot 按脚本资源路径解析 `[GlobalClass]`，可能找不到
> `OrigoDefaultEntry` 类。解决方案：在你的项目中创建桥接节点：
>
> ```csharp
> [GlobalClass]
> public partial class MyOrigoEntry : GodotAdapter.Bootstrap.OrigoDefaultEntry { }
> ```
>
> 然后将 `.tscn` 中的节点脚本指向你的桥接类。

### 4）编写一个策略

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
        // 移动逻辑...
    }
}
```

### 5）定义一个实体

```json
{
  "name": "Player",
  "node": { "pairs": { "sprite": "player_sprite" } },
  "strategy": { "indices": ["game.player_move"] },
  "data": { "pairs": { "speed": { "type": "Single", "data": 200.0 } } }
}
```

## 典型运行流程

1. `OrigoDefaultEntry` 启动运行时。
2. 加载入口存档/配置。
3. 按元数据生成实体。
4. 每帧执行策略 `Process`。
5. 先写入 `current/`，再快照到 `save_xxx/`。

## 仓库结构

```text
Origo.Core/
Origo.GodotAdapter/
Origo.Core.Tests/
Origo.GodotAdapter.Tests/
scripts/
Origo.sln
```

## 测试

在仓库根目录执行与 CI 一致的入口：

```bash
bash scripts/ci.sh
```

仅跑测试可用：

```bash
bash scripts/run-test.sh
```

## 许可证

MIT，详见 [LICENSE](LICENSE)。
