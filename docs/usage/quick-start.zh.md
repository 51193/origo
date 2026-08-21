<!-- docsync-pair: usage/quick-start -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 快速开始

> [↑ 回到 usage](README.zh.md)

## 概述

在 Godot 4 项目中接入 Origo 框架的最短路径。

## 前提

- Godot 4.7 项目（.NET 版本）
- .NET 10.0 SDK（或更高）
- 项目中已引用 Origo（NuGet 包或 ProjectReference）

## 步骤

### 1. 创建文件夹结构

在 Godot 项目根目录下创建以下文件夹结构：

```
res://origo/
├── entry/
│   └── entry.json             # 入口配置
├── maps/
│   ├── scene_aliases.map      # 场景别名映射
│   └── snd_templates.map      # 模板定义
└── initial/                   # 初始存档数据
    └── ...                    # 初始关卡 JSON
```

### 2. 创建入口节点

在 Godot 场景中创建一个 Node，为其附加 `OrigoDefaultEntry` 脚本：

1. 新建场景，根节点类型选 `Node`
2. 在 Node 的脚本属性中选择 `OrigoDefaultEntry.cs`
3. 保存场景（例如 `Main.tscn`）

### 3. 配置 entry.json

创建 `res://origo/entry/entry.json`：

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

### 4. 编写策略

创建一个 Strategy：

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

### 5. 定义实体模板

创建实体模板 JSON（通过 `snd_templates.map` 或直接 JSON）：

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

### 6. 运行

运行 Godot 项目，`OrigoDefaultEntry._Ready()` 将自动：
1. 创建 `OrigoRuntime`
2. 发现并注册所有 `[StrategyIndex]` 策略
3. 创建 `SndContext`
4. 加载别名和模板
5. 从 `initial/` 加载入口关卡

## 运行时流程

```
OrigoAutoHost._Ready()
  → 创建 GodotFileSystem, GodotLogger
  → 创建 GodotSndManager
  → 注册 TypeStringMapping (BCL + Godot types) + DataSourceConverters
  → 创建 PersistentBlackboard → LoadFromDisk
  → 创建 ConsoleInputBuffer + ConsoleOutputChannel
  → 创建 OrigoRuntime (内含 SndWorld + SystemRun + OrigoConsole)
  → BindRuntimeDependencies → SndManager

OrigoDefaultEntry._Ready()
  → 注册适配层命令处理器 (press_button, tree_debug, camera_view)
  → 创建 SndContext
  → SndManager.BindContext(context)
  → ConfigureSaveMetadataContributors(context)
  → SndContext.Bootstrap()
      → DiscoverAndRegisterStrategies
      → LoadSceneAliases + LoadTemplates
      → RequestLoadMainMenuEntrySave → 启动游戏

每帧: _Process → IOrigoFrameDriver.DriveFrame(delta)
  → Snd.ProcessAll(delta)          # 实体帧处理
  → FlushEndOfFrameDeferred()      # 业务队列 → KillPendingEntities → 系统队列
  → Core 内部：Console.ProcessPending()  # 控制台命令处理
```

## 下一步

- [架构总览](architecture-overview.zh.md) — 理解 Origo 的整体设计
- [SND 实体模型](snd-entity-model.zh.md) — 学习如何编写策略
- [策略测试](strategy-testing.zh.md) — 使用 StrategyTestScenario 测试策略

---
[↑ 回到 usage](README.zh.md)
