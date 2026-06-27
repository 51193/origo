# 会话模型

> [↑ 回到 usage](README.md)

## 概述

Origo 支持**前台会话**和**后台会话**并存的模型，两种会话通过相同的 `ISessionRun` 接口表达，差异仅在于注入的 `ISndSceneHost` 和 `IsFrontSession` 标志。

## 前台 vs 后台

| 属性 | 前台会话 | 后台会话 |
|------|---------|---------|
| 键名 | `__foreground__`（固定） | 用户自定义（如 `"dungeon"`） |
| 数量 | 至多一个 | 可多个 |
| 场景宿主 | GodotSndManager（引擎渲染） | FullMemorySndSceneHost（无渲染） |
| Strategy 访问 | 前台 ISndContext | 隔离的 SessionSndContext |
| 状态机 | 会话级 StateMachineContainer | 会话级 StateMachineContainer |
| 黑板 | 独立 SessionBlackboard | 独立 SessionBlackboard |

## 接口

### ISessionRun

```csharp
public interface ISessionRun : IDisposable
{
    IBlackboard SessionBlackboard { get; }
    ISndSceneHost SceneHost { get; }       // 支持完整实体操作
    string LevelId { get; }
    bool IsFrontSession { get; }
    IStateMachineContainer GetSessionStateMachines();
}
```

### ISessionManager

```csharp
public interface ISessionManager
{
    const string ForegroundKey = "__foreground__";
    bool CanCreateSessions { get; }
    ISessionRun? ForegroundSession { get; }
    IReadOnlyCollection<string> Keys { get; }
    ISessionRun? TryGet(string key);
    bool Contains(string key);
    ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false);
    void DestroySession(string key);
    void ProcessAllSessions(double delta, bool includeForeground = false);
}
```

## 创建后台会话

```csharp
var bgSession = sessionManager.CreateBackgroundSession(
    key: "dungeon",
    levelId: "dungeon_level",
    syncProcess: true);  // 参与 Process 帧更新

// 后台会话拥有独立的实体、黑板和状态机
bgSession.SceneHost.CreateEntity(dungeonEntityMeta);
bgSession.SessionBlackboard.SetValue("explored", true);
```

**levelId 唯一性约束：** 同一时刻，一个 levelId 只能被一个会话持有。若尝试创建后台会话时已有前台或其他后台使用了该 levelId，`CreateBackgroundSession` 会抛出 `InvalidOperationException`。

`SwitchForeground` 在执行前会**自动检测**是否有后台会话持有目标 `levelId`。若存在冲突，会先保存该后台会话的数据到 `current/`，再销毁它，最后创建新前台会话。调用方无需手动销毁后台会话。

若需要显式控制流程（例如不想保存后台的当前状态），仍可手动销毁后台后再调用切换：

```csharp
// 方式一：自动处理（推荐）—— SwitchForeground 内部完成保存 + 销毁
var bg = ctx.SessionManager.CreateBackgroundSession("gen", "game", false);
bg.SceneHost.CreateEntity(...);
bg.SessionBlackboard.SetValue("data", value);

// 直接切换，SwitchForeground 会自动保存并销毁 bg
ctx.RequestSwitchForegroundLevel("game");
ctx.FlushDeferredActionsForCurrentFrame();

// 方式二：手动控制——调用方显式逐步保存和销毁，获得更细粒度控制
var bg = ctx.SessionManager.CreateBackgroundSession("gen", "game", false);
bg.SceneHost.CreateEntity(...);

ctx.RequestSaveGameAuto();
ctx.FlushDeferredActionsForCurrentFrame();

ctx.SessionManager.DestroySession("gen");
ctx.RequestSwitchForegroundLevel("game");
ctx.FlushDeferredActionsForCurrentFrame();
```

## 会话拓扑

多个并发会话之间的关系通过 `SessionTopology` 编解码。编码格式（存储在 Progress 黑板中）：

```
key=levelId=syncProcess,key=levelId=syncProcess
```

例如：`__foreground__=town=false,dungeon=dungeon_level=true,farm=farm_level=false`

由于 levelId 唯一性约束，topology 中不会出现两个条目指向同一 levelId。

读档恢复时，`SessionTopologyCodec` 解析此字符串，重建所有后台会话。若解析到的 topology 中包含 levelId 重复，会因 `CreateBackgroundSession` 的 levelId 校验而抛异常——这确保了损坏的存档数据不会静默加载。

## 状态机上下文

状态机策略钩子通过 `IStateMachineContext` 访问黑板：

```csharp
public interface IStateMachineContext : ISndBlackboardAccess, ISndDeferredActions
{
    IBlackboard SystemBlackboard { get; }     // 系统级（继承 ISndBlackboardAccess）
    IBlackboard? ProgressBlackboard { get; }  // 流程级（继承 ISndBlackboardAccess）
    IBlackboard? SessionBlackboard { get; }   // 当前会话级
    ISndSceneAccess SceneAccess { get; }      // 当前会话场景
    void EnqueueBusinessDeferred(Action action);           // 继承 ISndDeferredActions
    void FlushDeferredActionsForCurrentFrame();            // 继承 ISndDeferredActions
    int GetPendingPersistenceRequestCount();               // 继承 ISndDeferredActions
}
```

`SessionStateMachineContext` 是会话级适配器，确保前台/后台会话的状态机钩子拿到各自的 `SessionBlackboard` 和 `SceneAccess`，前后台无语义分差。

## 生命周期

```
创建:
  SessionManager.CreateBackgroundSession(key, levelId)
    → 校验 key 和 levelId 均不与已有会话冲突
    → 创建 SessionRun
    → 注入 FullMemorySndSceneHost
    → 挂载到 _sessions 字典
    → 可选：从存档恢复 SessionBlackboard + 状态机 + 实体

运行:
  SessionManager.ProcessAllSessions(delta)
    → 遍历所有 syncProcess=true 的后台会话
    → 调用 session.SceneHost.ProcessAll(delta)

 关卡切换:
   RequestSwitchForegroundLevel(newLevelId)
     → 系统延迟队列 FIFO 执行（排在 Save 之后）
     → PersistForegroundLevelState（显式持久化旧前台关卡数据到 current/）
     → PersistAndDestroyBackgroundIfExists（若后台会话持有目标 levelId，先保存再销毁）
     → ResetForeground(true)（销毁当前前台，Dispose 不隐式持久化）
     → LoadAndMountForeground（创建新前台，从 current/ 解析新关卡数据）
     → PersistProgress（写完整会话拓扑到 current/）

销毁:
  SessionManager.DestroySession(key)
    → Dispose SessionRun（仅清理资源：卸载、弹出状态机、清理实体和黑板）
    → 从字典移除
    → 释放该会话持有的 levelId

退出:
  ProgressRun dispose
    → 按序销毁所有后台会话和前台会话
    → 清理 current/ 临时目录
    → 不触发持久化（保存由应用层显式调用 RequestSaveGame 负责）
```

## 设计原则

- 前后台共享同一接口，无类型分叉
- 管理层全权管理生命周期，会话层仅暴露内部状态
- 后台会话通过 `FullMemorySndSceneHost` 获得完整策略能力，但不渲染
- 关卡切换在系统延迟队列中执行，与 Save 操作同队 FIFO。同帧内 `RequestSaveGameAuto` 后调用 `RequestSwitchForegroundLevel` 时，Save 先写入 `current/`，Switch 加载时能发现数据
- **levelId 唯一性**：每个 levelId 同一时刻只被一个会话持有。若后台会话与目标前台 levelId 冲突，`SwitchForeground` 会在切换前自动保存并销毁该后台会话
- **Dispose 不持久化**：`ISessionRun.Dispose` 和 `ProgressRun.Dispose` 仅负责资源清理，不触发任何持久化操作。关卡切换时的旧前台数据由 `SwitchForeground` 显式调用 `PersistSession` 持久化。退出前如需保存进度，应用层应显式调用 `RequestSaveGame`

## 相关文档

- [持久化流程](persistence-flow.md) — 会话数据的存档/恢复
- [状态机](state-machine.md) — 会话级状态机

---
[↑ 回到 usage](README.md)
