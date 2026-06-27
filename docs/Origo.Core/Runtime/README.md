# Runtime

> [↑ 回到 Origo.Core](../README.md)

## 模块能力

Origo 的运行时核心。管理从系统级到会话级的四层生命周期，提供控制台命令系统、会话管理器、以及状态机容器。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Console](Console/README.md) | 控制台命令系统 | 命令解析/路由 + 输入队列 + 输出通道 |
| [Console/CommandHandlers](Console/CommandHandlers/README.md) | 内置命令 | help / bb_get/set/keys / spawn / find_entity / kill_all / snd_count |
| [Lifecycle](Lifecycle/README.md) | 四层运行时生命周期 | SystemRun → ProgressRun → SessionManager → SessionRun |
| [StateMachine](StateMachine/README.md) | 状态机容器 | `StateMachineContainer`：CreateOrGet / 序列化 / 批量操作 |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `OrigoRuntime.cs` | 运行时聚合容器：持有 SystemRun、SystemBlackboard、SndWorld、Console、Logger |
| `OrigoAutoInitializer.cs` | 策略自动发现与注册（反射扫描程序集） |

### OrigoRuntime

运行时入口点，集中持有所有运行时子系统引用：

```
OrigoRuntime
├── OrigoMeta (名称/版本/横幅)
├── ILogger
├── IBlackboard (SystemBlackboard + PersistentBlackboard)
├── SndWorld (策略池 + 类型映射 + 转换器)
├── OrigoConsole (控制台命令路由)
├── SystemRun (系统级生命周期)
│   └── ProgressRuntime
│       └── SessionManagerRuntime
│           └── SessionRun (foreground + background)
└── IScheduler (帧循环驱动)
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
[↑ 回到 Origo.Core](../README.md)
