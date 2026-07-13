# Origo

[English](README.md)

**Origo** 是一个轻量、平台无关的 C# 游戏框架。  
游戏逻辑写成策略（Strategy），框架负责实体生命周期、持久化、运行时调度。  
引擎通过适配层隔离接入（官方提供 Godot 4 适配器）。

## 能做什么

### 策略驱动编写游戏逻辑

每一块玩法行为都是一个**策略**——一个普通的 C# 类，无需继承引擎基类。
策略无状态、对象池共享，注册时自动校验约束，杜绝运行时静默出错。

```csharp
[StrategyIndex("my_game.health")]
public class HealthStrategy : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("hp", 100);
    }

    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, hp) = entity.TryGetData<int>("hp");
        if (found && hp <= 0)
            ctx.RequestKillEntity(entity.Id);
    }
}
```

- **SND 模型**：Strategy（行为）、Node（表现）、Data（状态）三者解耦，各司其职。
- **完整生命周期钩子**：`AfterSpawn`、`AfterLoad`、`AfterAdd`、`Process`、`BeforeRemove`、`BeforeSave`、`BeforeQuit`、`BeforeDead`——可按需接入任意阶段。
- **数据观测**：通过观察者策略订阅任意实体的数据变更（自身或跨实体），绑定关系随存档持久化、读档自动恢复。
- **主动策略**：类型安全的跨实体服务调用，`InvokeStrategy<TInput, TOutput>` 零样板代码。
- **运行时动态增删策略**：运行时挂载/卸载策略，完整的生命周期感知。
- **TypedData**：编译期生成强类型数据访问器，告别装箱，告别热路径上的字符串 key 查找。

### 状态管理与持久化

- **内置存档系统**：当前工作区 + 快照槽位。两阶段写入（`current/` → `save_xxx/`），基于哈希的幂等去重，加载时严格校验完整性。
- **后台 Session**：在后台运行 AI 仿真、程序化生成或离屏世界更新——与前台 Session 走同一套策略逻辑和数据契约。通过 `ctx.SessionManager.CreateBackgroundSession(key, levelId)` 创建。
- **快照管理**：运行时枚举存档、查看元数据、自由选择加载。

### 导航、AI 与规划

- **网格坐标系**：`GridPos` 类型，支持单/双轴坐标转换。
- **A\* 寻路**：内置网格寻路算法。
- **状态机**：字符串栈状态机，Push/Pop 附带策略钩子，栈状态随存档序列化与恢复。
- **意图驱动计划**：`PlanExecutionStrategyBase` 支持带作用域参数存储的动作序列执行。
- **延迟动作调度**：线程安全队列，快照-排干模式，支持执行中继续入队。

### 辅助工具

- **随机数**：`XorShift128+`（周期 2^128−1），无全局状态。`PersistentRandom` 提供存档安全的可恢复随机状态。
- **噪声生成**：OpenSimplex2 + Worley Cellular 混合噪声，用于程序化地形/内容。
- **黑板**：内存键值存储，可序列化，用于运行时配置和共享状态。
- **数值配方加载**：从键值对文件加载实体数据，自动类型推断。

### 开发工具

- **TCP 远程控制台**（端口 9876）：通过网络连接发送控制台命令、接收输出，专门为 Agent 驱动开发和自动化测试设计。11 个内置命令，覆盖实体检查、数据操作、策略调用，支持自定义命令扩展。

```bash
nc localhost 9876
```

- **源码生成器**：Roslyn 增量生成器在编译期生成类型化数据访问器，消除装箱和字符串 key 查找。4 个诊断规则（`ORIGOSG001`–`004`）在编译时捕获配置错误。
- **测试基础设施**：`StrategyTestScenario` 声明式策略单元测试框架（Configure → Simulate → Inspect）。架构护栏测试强制执行依赖方向和策略约束。

### Godot 4 适配器

- **文件系统**：通过 `IFileSystem` 访问 `res://` 和 `user://`，带路径穿越防护。
- **日志代理**：将 Core 层日志桥接到 `GD.Print` / `PushWarning` / `PushError`。
- **场景节点工厂**：通过逻辑场景别名实例化 `PackedScene` 节点。
- **实体-节点桥接**：`GodotSndEntity` 将 `ISndEntity` 生命周期与 Godot `Node` 生命周期关联。14 种 Godot 向量/数学类型完整 JSON 往返序列化。

## 快速开始（Godot 4）

### 1. 添加包引用

**NuGet**（推荐）：从[最新 Release](https://github.com/51193/origo/releases/latest) 下载 `.nupkg` 文件，放入 `./packages/origo/`，配置本地包源：

```xml
<!-- nuget.config，放在 Godot 项目根目录 -->
<configuration>
  <packageSources>
    <add key="origo-local" value="./packages/origo/" />
  </packageSources>
</configuration>
```

```xml
<PackageReference Include="Origo.Core" />
<PackageReference Include="Origo.GodotAdapter" />
```

### 2. 创建目录结构

```
res://origo/
  entry/entry.json
  maps/scene_aliases.map
  maps/snd_templates.map
  initial/
```

### 3. 添加入口节点

在启动场景挂载 `OrigoDefaultEntry`，配置各路径属性。  
> 若 Godot 无法解析 `[GlobalClass]`，创建一行桥接类：
> ```csharp
> [GlobalClass]
> public partial class MyOrigoEntry : GodotAdapter.Bootstrap.OrigoDefaultEntry { }
> ```

### 4. 编写策略与定义实体

```csharp
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

```json
{
  "name": "Player",
  "node": { "pairs": { "sprite": "player_sprite" } },
  "strategy": { "indices": ["game.player_move"] },
  "data": { "pairs": { "speed": { "type": "Single", "data": 200.0 } } }
}
```

### 5. 运行

`OrigoDefaultEntry._Ready()` 自动发现所有 `[StrategyIndex]` 策略、加载别名和模板、启动游戏。

> 完整教程：[快速开始](docs/usage/quick-start.md) · [架构概览](docs/usage/architecture-overview.md) · [SND 实体模型](docs/usage/snd-entity-model.md)

## 文档

完整文档已并入本仓库 **[`docs/`](docs/README.md)**——自底向上、镜像源码结构的文档树。

开发循环与 Agent 准则见 **[`AGENTS.md`](AGENTS.md)**。

| 我想... | 去这里 |
|---|---|
| 浏览全部能力 | [能力清单](docs/usage/capabilities.md) |
| 理解架构设计 | [架构概览](docs/usage/architecture-overview.md) |
| 学习 SND 模型 | [SND 实体模型](docs/usage/snd-entity-model.md) |
| 测试我的策略 | [策略测试](docs/usage/strategy-testing.md) |
| 使用存档系统 | [持久化流程](docs/usage/persistence-flow.md) |
| 使用状态机 | [状态机](docs/usage/state-machine.md) |
| 使用控制台 | [控制台命令](docs/usage/console-commands.md) |
| AI Agent 参考 | [Agent Reference](docs/usage/agent-reference.md) |

## 开发

```bash
bash scripts/ci.sh        # 完整 CI 流水线（格式检查 + 测试 + 基准 + Godot 集成）
bash scripts/test.sh      # 构建 + 测试 + 覆盖率门禁（日常迭代）
bash scripts/format.sh    # 仅格式检查
```

| 模块 | 说明 |
|---|---|
| `Origo.Core` | 平台无关核心：SND 实体、运行时、持久化、状态机 |
| `Origo.SourceGeneration` | Roslyn 增量源码生成器，TypedData 强类型访问器 |
| `Origo.ConsoleBridge` | TCP 远程控制台桥接 |
| `Origo.GodotAdapter` | Godot 4 适配层：文件系统、日志、序列化、启动 |

| 测试项目 | 覆盖率门禁 |
|---|---|
| `Origo.Core.Tests` | ≥ 90% |
| `Origo.GodotAdapter.Tests` | ≥ 85% |
| `Origo.ConsoleBridge.Tests` | ≥ 80% |
| `Origo.SourceGeneration.Tests` | ≥ 85% |

## 许可证

MIT，详见 [LICENSE](LICENSE)。
