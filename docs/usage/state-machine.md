# 状态机

> [↑ 回到 usage](README.md)

## 概述

Origo 的状态机是一个**字符串栈**状态机。栈内仅存储字符串标识符，Push/Pop 的具体语义由关联的策略钩子实现。这样状态机本身保持轻量，所有业务逻辑集中在策略中。

## 核心接口

```csharp
public interface IStateMachine
{
    string MachineKey { get; }           // 状态机唯一标识
    string PushStrategyIndex { get; }    // Push 策略的索引
    string PopStrategyIndex { get; }     // Pop 策略的索引

    void Push(string value);             // 运行时入栈
    bool TryPopRuntime(out string? popped);  // 运行时出栈
    bool TryPopOnQuit(out string? popped);   // 退出时出栈
    (bool found, string? top) Peek();    // 查看栈顶
    IReadOnlyList<string> Snapshot();    // 栈快照
    void FlushAfterLoad();               // 读档后重放
    void RestoreStackWithoutHooks(IReadOnlyList<string> stack);  // 无钩子恢复
}
```

## 策略钩子

### StateMachineStrategyBase

```csharp
public abstract class StateMachineStrategyBase : BaseStrategy
{
    // 运行时 Push 成功后调用
    public virtual void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) { }

    // 读档恢复后，栈自底向顶对每层调用
    public virtual void OnPushAfterLoad(StateMachineStrategyContext context, IStateMachineContext ctx) { }

    // 运行时 Pop 前调用
    public virtual void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx) { }

    // 退出流程 Pop 前调用
    public virtual void OnPopBeforeQuit(StateMachineStrategyContext context, IStateMachineContext ctx) { }
}
```

### 钩子上下文

```csharp
public readonly struct StateMachineStrategyContext
{
    public string MachineKey { get; }   // 状态机标识
    public string? BeforeTop { get; }   // 操作前的栈顶
    public string? AfterTop { get; }    // 操作后的栈顶
}
```

## 使用示例

### 定义 Push/Pop 策略

```csharp
// Push 策略：入栈时执行的逻辑
[StrategyIndex("my_game.menu_push")]
public class MenuPushStrategy : StateMachineStrategyBase
{
    public override void OnPushRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
        // context.AfterTop 是新栈顶（刚 push 的值）
        if (context.AfterTop == "main_menu")
            ctx.SessionBlackboard?.SetValue("active_menu", context.AfterTop);
    }
}

// Pop 策略：出栈时执行的逻辑
[StrategyIndex("my_game.menu_pop")]
public class MenuPopStrategy : StateMachineStrategyBase
{
    public override void OnPopRuntime(StateMachineStrategyContext context, IStateMachineContext ctx)
    {
        // context.BeforeTop 是出栈前的栈顶
        var nextMenu = context.AfterTop;
        // 切换到下一个菜单状态...
    }
}
```

### 创建和使用状态机

```csharp
// 在 ProgressRun 或 SessionRun 中获取状态机容器
var container = sessionRun.GetSessionStateMachines();

// 创建状态机（若已存在则返回已有实例）
var sm = container.CreateOrGet(
    machineKey: "main_fsm",
    pushStrategyIndex: "my_game.menu_push",
    popStrategyIndex: "my_game.menu_pop");

// 运行时操作
sm.Push("main_menu");
sm.Push("settings");
sm.TryPopRuntime(out var popped);  // popped == "settings"
```

## 读档恢复（两阶段）

```
1. Deserialize → RestoreStackWithoutHooks(stack)
   → 恢复栈内容，不触发任何策略钩子

2. FlushAfterLoad()
   → 自栈底向栈顶对每层调用 OnPushAfterLoad
   → 重放初始状态逻辑
```

两阶段分离确保：栈结构调整时不触发钩子（避免不完整状态），调整完成后统一重放。

## 序列化格式

```json
{
  "machines": [
    {
      "key": "main_fsm",
      "pushIndex": "my_game.menu_push",
      "popIndex": "my_game.menu_pop",
      "stack": ["main_menu", "settings"]
    }
  ]
}
```

## 容器操作

> 下表前三行为 `IStateMachineContainer` 接口方法；`FlushAllAfterLoad`、`PopAllRuntime`、`PopAllOnQuit` 为具体类 `StateMachineContainer` 的内部方法（non-interface）。

| 操作 | 说明 | 来源 |
|------|------|------|
| `CreateOrGet(key, pushIdx, popIdx)` | 创建或获取（索引不同则抛异常） | 接口 |
| `TryGet(key)` | 查找已有状态机 | 接口 |
| `Remove(key)` | 移除并 Dispose | 接口 |
| `Clear()` | 释放所有状态机 | 接口 |
| `FlushAllAfterLoad()` | 所有机器按插入顺序重放 | 实现 |
| `PopAllRuntime()` | 按插入顺序逐个弹空 | 实现 |
| `PopAllOnQuit()` | 退出流程按插入顺序逐个弹空 | 实现 |

## 与实体策略的区别

| 特性 | 实体策略 (LifecycleStrategyBase) | 状态机策略 (StateMachineStrategyBase) |
|------|------|------|
| 挂载位置 | 实体 (SndEntity) | 状态机 (StackStateMachine) |
| 数据访问 | `entity.SetData/TryGetData` | `ctx.SessionBlackboard` |
| 生命周期 | 8 个钩子 (Spawn→Dead) | 4 个钩子 (Push/Pop) |
| 适用场景 | 实体行为（移动、血量、渲染） | 流程控制（菜单栈、关卡切换） |

## 相关文档

- [SND 实体模型](snd-entity-model.md) — 实体策略
- [会话模型](session-model.md) — 会话级状态机

---
[↑ 回到 usage](README.md)
