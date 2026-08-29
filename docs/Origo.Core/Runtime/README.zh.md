<!-- docsync-pair: Origo.Core/Runtime/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Runtime

> [↑ 回到 Origo.Core](../README.zh.md)

## 模块能力

Origo 的运行时核心。管理从系统级到会话级的四层生命周期，提供控制台命令系统、会话管理器、以及状态机容器。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Console](Console/README.zh.md) | 控制台命令系统 | 命令解析/路由 + 输入队列 + 输出通道 |
| [Console/CommandHandlers](Console/CommandHandlers/README.zh.md) | 内置命令 | help / bb_get / bb_set / bb_keys / spawn / find_entity / kill_all / snd_count / entity_get_data / entity_set_data / invoke_strategy（共 11 个） |
| [Lifecycle](Lifecycle/README.zh.md) | 四层运行时生命周期 | SystemRun → ProgressRun → SessionManager → SessionRun |
| [StateMachine](StateMachine/README.zh.md) | 状态机容器 | `StateMachineContainer`：CreateOrGet / 序列化 / 批量操作 |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `OrigoRuntime.cs` | 运行时聚合容器：持有 SystemBlackboard、SndWorld、Console、Logger；SndContext 在其上构造 SystemRun 与 ProgressRun |
| `OrigoAutoInitializer.cs` | `internal` — 策略自动发现与注册（反射扫描程序集）；仅由 `SndContext.Bootstrap` 编排调用 |

### OrigoRuntime

运行时入口点，集中持有所有运行时子系统引用：

```
OrigoRuntime
├── OrigoMeta (名称/版本/横幅)
├── ILogger
├── IBlackboard (SystemBlackboard + PersistentBlackboard)
├── SndWorld (策略池 + 类型映射 + 转换器)
├── OrigoConsole (控制台命令路由)
├── IOrigoFrameDriver (帧循环驱动)
└── (由 SndContext 持有) SystemRun (系统级生命周期)
    └── ProgressRuntime → ProgressRun
        └── SessionManagerRuntime → SessionManager
            └── SessionRun (foreground + background)
```

## 运行时四层

```
SystemRuntime → SystemRun
    ├── SndWorld (全局共享)
    ├── SystemBlackboard
    └── ProgressRuntime → ProgressRun
        ├── ProgressBlackboard
        ├── SaveContext (存档编排)
        └── SessionManagerRuntime → SessionManager
            ├── SessionRun (foreground: "__foreground__")
            │   ├── SessionBlackboard
            │   ├── ISndSceneHost (Godot 或内存)
            │   └── StateMachineContainer
            └── SessionRun (background: user-defined keys)
                ├── SessionBlackboard
                ├── FullMemorySndSceneHost
                └── StateMachineContainer
```

能力单向向下传递，下层不得反向依赖上层。

---
[↑ 回到 Origo.Core](../README.zh.md)
