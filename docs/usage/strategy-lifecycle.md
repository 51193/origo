# 策略生命周期

> [↑ 回到 usage](README.md)

## 三对闭环 + 一个特殊钩子

策略的生命周期钩子采用**成对闭环**设计，类似 RAII 模式。每一对钩子管理一个特定作用域的资源获取与释放。**在某个钩子中获取的资源，必须在其对应的配对钩子中释放。**

| 闭环 | 获取钩子 | 释放钩子 | 作用域 | 典型资源 |
|------|----------|----------|--------|----------|
| **游戏逻辑闭环** | `AfterSpawn` | `BeforeDead` | 实体的整个游戏生命周期（创建→死亡） | 管理器注册/注销、全局事件广播、世界级统计 |
| **运行期资源闭环** | `AfterLoad` | `BeforeQuit` | 一次运行会话（加载→退出/切换存档） | 输入捕获、音频通道、鼠标显隐、向运行时管理器订阅 |
| **策略级闭环** | `AfterAdd` | `BeforeRemove` | 策略挂载期间（动态添加→移除） | 策略专属的临时数据字段、独占引用 |
| **(特殊)** | `BeforeSave` | 无配对 | 保存时刻触发 | 引擎状态→Data 延迟同步 |

## 设计原则

### 1. 闭环必须配对

在 `AfterLoad` 中获取的运行期非持久化资源，必须在 `BeforeQuit` 中释放。不能依赖 `BeforeRemove`——退出时策略不一定被移除。

```csharp
// ✅ 正确：AfterLoad ↔ BeforeQuit 配对（运行期非持久化资源）
public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    ctx.FindEntity("InputRouter")?.InvokeStrategy("input.capture_begin", entity.Name);
}

public override void BeforeQuit(ISndEntity entity, ISndContext ctx)
{
    ctx.FindEntity("InputRouter")?.InvokeStrategy("input.capture_end", entity.Name);
}
```

```csharp
// ❌ 错误：AfterLoad 中获取，BeforeRemove 中释放（闭环不匹配）
public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    ctx.FindEntity("InputRouter")?.InvokeStrategy("input.capture_begin", entity.Name);
}

public override void BeforeRemove(ISndEntity entity, ISndContext ctx)
{
    ctx.FindEntity("InputRouter")?.InvokeStrategy("input.capture_end", entity.Name); // 退出时不触发 BeforeRemove
}
```

### 2. 不同闭环不等价

- `AfterSpawn` 只在实体**首次创建**时触发一次
- `AfterLoad` 每次**从存档恢复**都触发

在 `AfterSpawn` 中注册到管理器的实体，从存档恢复时不需要重复注册（存档已包含管理器状态）。

```csharp
// 游戏逻辑闭环：注册到全局管理器（仅首次创建时）
public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
{
    var mgr = ctx.FindEntity("FoodManager");
    mgr.InvokeStrategy("food.register", entity.Name);
}

public override void BeforeDead(ISndEntity entity, ISndContext ctx)
{
    var mgr = ctx.FindEntity("FoodManager");
    mgr.InvokeStrategy("food.unregister", entity.Name);
}
```

### 3. AfterLoad 不做业务初始化

存档恢复后实体的所有 Data 已经是正确的持久化状态。`AfterLoad` 只需恢复**运行时非持久化资源**（如向运行时管理器注册、重建运行时缓存等），不应重置业务流程或重新启动逻辑。数据观察由观察者策略承载，其绑定随存档持久化、读档自动恢复，无需在 `AfterLoad` 中重建。

```csharp
// ✅ 正确：AfterLoad 只恢复运行时非持久化资源（如向运行时管理器注册）
public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    ctx.FindEntity("CombatManager")?.InvokeStrategy("combat.register", entity.Name);
}

// ❌ 错误：AfterLoad 重置业务状态（Data 中已有正确值）
public override void AfterLoad(ISndEntity entity, ISndContext ctx)
{
    entity.SetData("hp", 100);  // 覆盖了存档中的正确值
}
```

### 4. BeforeQuit 可安全访问会话资源

`BeforeQuit` 执行期间，`ctx.CurrentSession.SceneHost` 和 `ctx.CurrentSession.SessionBlackboard` 保证可访问。框架的 Dispose 流程使用两阶段标志确保会话资源在 BeforeQuit 钩子执行完毕后才标记为 disposed。

即使某个 BeforeQuit 钩子抛出异常，框架通过 `try/finally` 保证实体清理和场景容器清空必定完成，不会残留实体导致无限错误循环。

```csharp
// ✅ 安全：BeforeQuit 中访问会话资源
public override void BeforeQuit(ISndEntity entity, ISndContext ctx)
{
    var session = ctx.CurrentSession;
    if (session == null) return;

    var mgr = session.SceneHost.FindByName("MyManager");
    mgr?.InvokeStrategy("my.unregister", entity.Name);
}
```

> 注意：数据观察由观察者策略承载，其绑定在实体退出/死亡时由框架自动卸载（触发 `OnUnmounted`），无需在 BeforeQuit 中手动退订。BeforeQuit 仅用于释放观察者策略之外的运行时资源（如向外部管理器注销）。

### 5. BeforeSave 用于延迟同步

对于引擎管理的状态（如 `Node3D.GlobalTransform`），不需要每帧写入 entity Data。只在 `BeforeSave` 时一次性同步，减少不必要的数据写入开销。

```csharp
public override void BeforeSave(ISndEntity entity, ISndContext ctx)
{
    var node = entity.GetNode("root")?.GetNativeNode();
    if (node is Node3D node3d)
        entity.SetData("transform", node3d.GlobalTransform);
}
```

## 策略级闭环：动态策略的资源管理

动态添加/移除的策略（如行动策略）使用 `AfterAdd` / `BeforeRemove` 管理策略专属的临时数据：

```csharp
[StrategyIndex("game.action.move_to")]
public class MoveToActionStrategy : LifecycleStrategyBase
{
    public override void AfterAdd(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("action.progress", 0f);
    }

    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        // 执行移动逻辑...
    }

    public override void BeforeRemove(ISndEntity entity, ISndContext ctx)
    {
        entity.SetData("action.progress", -1f);
    }
}
```

## 完整闭环示例

```csharp
[StrategyIndex("game.character.core", Priority = 10)]
public sealed class CharacterCoreStrategy : LifecycleStrategyBase
{
    // 游戏逻辑闭环：注册/注销到管理器（AfterSpawn ↔ BeforeDead 配对）
    public override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        ctx.FindEntity("CharacterManager")?.InvokeStrategy("character.register", entity.Name);

        // 观察闭环：挂载 hp 观察者策略（绑定随存档持久化、读档自动恢复、死亡自动卸载）
        entity.MountObserverStrategy(entity.Name, "game.character.hp_death");
    }

    public override void BeforeDead(ISndEntity entity, ISndContext ctx)
    {
        ctx.FindEntity("CharacterManager")?.InvokeStrategy("character.unregister", entity.Name);
    }

    // 特殊钩子：保存时同步引擎状态
    public override void BeforeSave(ISndEntity entity, ISndContext ctx)
    {
        var node = entity.GetNode("root")?.GetNativeNode();
        if (node is Node3D n)
            entity.SetData("transform", n.GlobalTransform);
    }
}

// 观察者策略：hp 归零时标记死亡
[StrategyIndex("game.character.hp_death")]
[ObserveData("hp")]
public sealed class HpDeathObserver : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        if (newValue.TryGetInt32(out var hp) && hp <= 0)
            entity.SetData("is_dead", true);
    }
}
```

## 常见错误

| 错误 | 后果 | 正确做法 |
|------|------|---------|
| 在生命周期钩子里手动订阅/退订数据变更 | 与存档持久化、读档恢复脱节，易泄漏 | 用观察者策略（`ObserverStrategyBase`），绑定自动持久化与卸载 |
| 在 `AfterSpawn` 中做运行时资源初始化 | 从存档恢复时资源未建立 | 运行时资源在 `AfterLoad` 中初始化 |
| 在 `AfterLoad` 中重置 Data 值 | 覆盖存档中的正确持久化状态 | `AfterLoad` 只恢复非持久化资源 |
| 每帧同步引擎状态到 Data | 不必要的写入开销 | 用 `BeforeSave` 延迟同步 |
| 混用不同闭环的获取/释放 | 资源泄漏或异常 | 严格配对同一闭环的获取/释放钩子 |

---

## 意图驱动计划执行（Planning）

对于需要多步骤计划执行的实体（如 AI 角色调度），Origo 提供了 `PlanExecutionStrategyBase`（位于 `Origo.Core.Planning` 命名空间）作为高级生命周期封装。

`PlanExecutionStrategyBase` 继承 `LifecycleStrategyBase`，通过 `sealed` 生命周期钩子自动管理订阅配对、Action 策略插拔和计划推进，将 RAII 闭环保留在框架层。用户仅需实现两个领域映射函数：

| 抽象成员 | 职责 |
|----------|------|
| `ResolveNextStep(intent, currentStep, failed, entity)` | 意图 → 计划步骤分解 |
| `StepToActionIndex(stepType)` | 步骤类型 → Action 策略索引映射 |

**与原始生命周期钩子的关系：**

- 基类 `sealed` 了全部 8 个 `LifecycleStrategyBase` 生命周期钩子
- 用户通过虚的 `On*` 钩子（`OnAfterSpawn`、`OnAfterLoad`、`OnProcess` 等）扩展行为
- 订阅和 Action 策略生命周期完全由基类管理，用户无需关心
- 不能覆写原始钩子 → 不可能忘记调用基类 → 消除 wiring 失效风险

**示例：**

```csharp
[StrategyIndex("character.scheduling", Priority = 5)]
public sealed class CharacterSchedulingStrategy : PlanExecutionStrategyBase
{
    public override string IntentKey => "character.intent";
    public override string IntentStatusKey => "character.intent_status";
    public override string PlanStepKey => "character.plan_step";
    public override string ActionKey => "character.action";
    public override string ActionStatusKey => "character.action_status";

    public override string? ResolveNextStep(string? intent, string? currentStep, 
        bool failed, ISndEntity entity)
    {
        return intent switch
        {
            "forage" => "find_target",
            "combat" => "find_enemy",
            "wander" => "wander",
            _ => null
        };
    }

    public override string? StepToActionIndex(string stepType)
    {
        return stepType switch
        {
            "find_target" => "character.action.find_target",
            "find_enemy" => "character.action.find_enemy",
            "wander" => "character.action.wander_target",
            _ => null
        };
    }
}
```

详见：[Planning 子系统文档](../Origo.Core/Planning/README.md) 和 [设计模式 - 调度层](design-patterns.md)。

---

## 下一个文档

- [设计模式](design-patterns.md) — 策略系统常用设计模式
- [SND 实体模型](snd-entity-model.md) — 模型基础

---
[↑ 回到 usage](README.md)
