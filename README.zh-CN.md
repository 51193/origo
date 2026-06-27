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
- **TCP 远程控制台**：`Origo.ConsoleBridge` 用于 Agent 驱动开发，通过网络连接执行控制台命令。
- **源码生成**：`Origo.SourceGeneration` 通过 Roslyn 增量生成器在编译期生成强类型 `TypedData` 访问器。
- **主动策略（Active Strategy）**：类型安全的实体间服务调用，通过 `InvokeStrategy<TInput, TOutput>` 实现。
- **动态策略管理**：运行时增删策略，完整生命周期钩子支持（`AfterAdd`/`BeforeRemove`）。

## 文档

完整文档已并入本仓库 **[`docs/`](docs/README.md)** ——一个自底向上、镜像源码结构的文档树。

开发循环与 Agent 准则见 **[`AGENTS.md`](AGENTS.md)**。

## 特殊能力：Background Session

Origo 支持创建后台 Session，并让它执行与前台 Session 完全一致的玩法逻辑路径。
这使你可以在内存中进行 AI 仿真、程序化生成或离屏世界更新，同时保持同一套策略行为与数据契约。

可通过 `ctx.SessionManager.CreateBackgroundSession(key, levelId)` 创建，并接入相同的 Session 处理管线。

## 控制台桥接

`Origo.ConsoleBridge` 提供一个基于 TCP 协议的远程控制台，用于 Agent 驱动开发。外部工具（如 AI 编程 Agent）可通过 TCP 连接发送控制台命令并接收命令输出，实现自动化玩法测试与运行时检查。

```bash
# 通过任意 TCP 客户端连接
nc localhost 9876
```

> **注意**：控制台桥接仅承载控制台 I/O——命令输入与命令输出。运行时日志由引擎/日志系统独立处理，不会通过桥接传输。

## 5 分钟上手（Godot 4）

### 1）引用项目

#### 方案 A：NuGet（推荐）

```xml
<PackageReference Include="Origo.Core" />
<PackageReference Include="Origo.GodotAdapter" />
<PackageReference Include="Origo.ConsoleBridge" />
```

> **NuGet 包通过 GitHub Releases 发布**。请从
> [最新 Release](https://github.com/51193/origo/releases/latest) 下载
> `.nupkg` 文件，放入 `./packages/origo/` 目录，并配置 `nuget.config` 添加
> 本地包源（Release 附件中已包含 `nuget.config` 模板）。
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
>
> 建议将 `nuget.config` 提交到你的仓库，确保所有协作者共享同一包源配置。
> 同时将 `packages/` 加入 `.gitignore`——`.nupkg` 二进制文件不应纳入版本控制。

#### 方案 B：项目引用

```xml
<ProjectReference Include="../Origo.Core/Origo.Core.csproj" />
<ProjectReference Include="../Origo.GodotAdapter/Origo.GodotAdapter.csproj" />
```

> **Godot `[GlobalClass]` 解析问题**：无论使用 NuGet 还是 ProjectReference，
> Godot 按脚本资源路径解析 `[GlobalClass]` 时都可能找不到 `OrigoDefaultEntry`。
> 解决方案：在你的项目中创建桥接类：
>
> ```csharp
> [GlobalClass]
> public partial class MyOrigoEntry : GodotAdapter.Bootstrap.OrigoDefaultEntry { }
> ```
>
> 然后将 `.tscn` 中的节点脚本指向你的桥接类。

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

### 4）编写一个策略

```csharp
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;

[StrategyIndex("game.player_move", Priority = 100)]
public sealed class PlayerMoveStrategy : LifecycleStrategyBase
{
    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, speed) = entity.TryGetData<float>("speed");
        if (!found) return;
        // 移动逻辑...
    }
}
```

> **Priority**：策略优先级（默认 6205）。Process 等生命周期回调按优先级升序执行；
> 同优先级按插入顺序（FIFO）。优先级越小，越先执行。

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
Origo.SourceGeneration/
Origo.ConsoleBridge/
Origo.GodotAdapter/
Origo.Core.Tests/
Origo.ConsoleBridge.Tests/
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
