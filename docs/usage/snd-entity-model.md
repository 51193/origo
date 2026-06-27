# SND 实体模型

> [↑ 回到 usage](README.md)

## 模型

```
Strategy + Node + Data
```

SND 模型将游戏实体拆分为三个正交维度：

| 维度 | 用途 | 存储位置 | 可变性 |
|------|------|---------|--------|
| **Strategy** | 行为逻辑 | 策略池（全局复用） | 不可变（无状态共享） |
| **Node** | 表现层映射 | 引擎场景树 | 由引擎管理 |
| **Data** | 可变状态 | SndDataManager (TypedData) | 可变（唯一权威来源） |

## 策略系统

### 根基类

```
BaseStrategy (抽象，所有策略的基类)
├── LifecycleStrategyBase     → 8 个实体生命周期钩子
├── ActiveStrategyBase     → Invoke(entity, ctx, input)
├── ObserverStrategyBase   → OnMounted / OnDataChanged / OnUnmounted
└── StateMachineStrategyBase → 4 个状态机钩子
```

### 观察者策略

`ObserverStrategyBase` 是 SND 策略体系中与 `LifecycleStrategyBase`、`ActiveStrategyBase` 平级的第一公民。观察者策略负责响应数据变更和绑定生命周期，框架自动管理接线持久化。

三个虚方法：

| 钩子 | 触发时机 | 典型用途 |
|------|---------|---------|
| `OnMounted(entity, ctx, target)` | 绑定建立（挂载/读档恢复） | 基于当前数据初始化派生状态 |
| `OnDataChanged(entity, ctx, target, dataKey, oldValue, newValue)` | 被观察数据变更 | 响应式逻辑（如 intent 状态变化） |
| `OnUnmounted(entity, ctx, target)` | 绑定终止（显式卸载/目标死亡/观察者退出） | 清理状态 |

声明需要观察的数据 key：

```csharp
[StrategyIndex("character.intent_watcher")]
[ObserveData("character.intent_status")]
public sealed class IntentWatcher : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        if (newValue.TryGetString(out var status) && status is "completed" or "failed")
            entity.SetData("character.intent", "");
    }
}
```

挂载和卸载：

```csharp
// EntityStrategy 在 AfterSpawn 中建立自观察
entity.MountObserverStrategy(entity.Name, "character.intent_watcher");

// 在 BeforeDead 中卸载
entity.UnmountObserverStrategy(entity.Name, "character.intent_watcher");
```

- 观察策略通过 `MountObserverStrategy(targetName, strategyIndex)` 挂载
- `entity` = 观察者实体，`target` = 被观察实体；自观察时两者为同一实体
- `observer_indices` 将跨实体绑定序列化到存档中，读档时自动恢复接线
- 自观察和跨实体观察使用同一挂载 API 和同一绑定拓扑格式
- 与 Data/Strategy 相同，Observer 绑定在存盘时持久化、读档时自动恢复
### 实体策略生命周期钩子

钩子按执行顺序排列：

| 钩子 | 触发时机 | 典型用途 |
|------|---------|---------|
| `AfterSpawn(entity, ctx)` | 新实体生成后（由 `SndRuntime.Spawn` / `SpawnMany` 统一触发） | 初始化属性（hp, position 等） |
| `AfterLoad(entity, ctx)` | 从存档恢复后（批量 RecoverFromMetaList 不触发，由上层统一触发） | 恢复后重新注册事件等 |
| `AfterAdd(entity, ctx)` | 策略动态添加到实体 | 动态添加状态逻辑 |
| `Process(entity, delta, ctx)` | 每帧 | 持续逻辑（移动、计时等） |
| `BeforeRemove(entity, ctx)` | 策略被移除前 | 清理订阅/资源 |
| `BeforeSave(entity, ctx)` | 序列化存档前 | 刷新待写入数据 |
| `BeforeQuit(entity, ctx)` | 实体正常退出 | 保存进度、清理 |
| `BeforeDead(entity, ctx)` | 实体销毁前（批量 RemoveAllEntities 不触发，由上层统一触发） | 死亡特效、掉落等 |

### 编写策略

```csharp
[StrategyIndex("my_game.damage_tick", Priority = 100)]
public class DamageTickStrategy : LifecycleStrategyBase
{
    public override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        var (found, hp) = entity.TryGetData<float>("hp");
        if (!found) return;

        entity.SetData("hp", hp - 5f * (float)delta);

        if (hp <= 0)
            ctx.RequestSaveGame("player_died");
    }
}
```

### 策略池规则

- **无状态强制**：策略类不得声明实例字段或可写属性（注册时反射校验）
- **注册方式**：`[StrategyIndex("xxx.yyy")]` 特性 + 程序集扫描
- **索引命名**：点分命名空间 + 小写蛇形分段（如 `core.player.health`）
- **优先级**：`Priority` 属性决定同实体上多策略的执行序（默认 6205，越小越先执行）
- **引用计数**：同一策略被多实体引用时计数 +1，全部释放后才回收

### 策略中禁止的行为

- 持有 `IDisposable` 字段或非托管资源
- 缓存跨帧可变上下文对象
- 声明实例字段存储运行时数据（应存入实体 Data）

## 实体数据

### TypedData

`readonly partial struct`，通过判别联合实现值类型内联存储。编译时由 Source Generator 为注册类型生成强类型访问器。

```csharp
public readonly partial struct TypedData : IEquatable<TypedData>
{
    internal byte _kind;          // 0=Null, 1=Int32, 2=Single, ...
    internal long _inlineBits;    // 值类型 ≤ 8 字节内联存储
    internal object? _ref;        // 引用类型或大值类型兜底

    public Type DataType { get; }  // 如 typeof(int)
    public object? Data { get; }   // 按需装箱，用于序列化边界
    public bool IsNull { get; }

    // Source Generator 生成的强类型访问器
    public bool TryGetInt32(out int value);
    public bool TryGetSingle(out float value);
    public bool TryGetString(out string? value);
    // ... 每种注册类型一个 TryGetXxx
}
```

- **存储时**：泛型 `SetData<T>` 通过 `TypedDataFactory<T>.Create(value)` 将值内联写入 `_inlineBits`（系统类型）或通过 `TypedDataObjectConverter` 桥接写入 `_ref`（适配层注册类型），零装箱零堆分配
- **读取时**：`TypedDataFactory<T>.TryExtract(td, out var value)` 通过判别值分发；注册类型直接字段读取或 Kind 检查，未注册类型走 `is T` 兜底
- **序列化时**：`TypedData.Data` 属性按需装箱值，`TypedDataConverter` 将类型转字符串并通过 `TypeStringMapping` 双向映射
- **多适配层支持**：GodotAdapter 通过 `[assembly: SndInlineTypes(startKind: 128, ...)]` 注册 14 种引擎类型，运行时通过 `TypedDataLayeredRegistry` 委托链解析 Kind 值。`TypedDataTypeMap.GetKindForType(typeof(Vector3))` 返回确定值 130，避免 `is T` 模式匹配
- **未注册类型**：走 `_ref` 兜底路径（kind=`TypedData.UnregisteredKind`），性能等同 class 方案

### 安全读取

```csharp
// 接口仅提供 TryGetData<T>，先判断 found 再取值
var (found, hp) = entity.TryGetData<int>("hp");
if (found) { /* 使用 hp */ }

// 数值类型跨精度读取（int/float/long/double 统一输出 float）
if (entity.TryGetNumeric("speed", out var speed)) { /* 使用 speed */ }
```

### 数据观察

数据变更通过 `ObserverStrategyBase` 的 `OnDataChanged` 回调响应：

```csharp
[StrategyIndex("my_game.hp_watcher")]
[ObserveData("hp")]
public sealed class HpWatcher : ObserverStrategyBase
{
    public override void OnDataChanged(ISndEntity entity, ISndContext ctx,
        ISndEntity target, string dataKey,
        TypedData oldValue, TypedData newValue)
    {
        if (newValue.TryGetInt32(out var hp) && hp <= 0)
            ctx.RequestSaveGame("entity_died");
    }
}
```

挂载观察者策略后，框架自动管理接线与持久化：

```csharp
// 在 EntityStrategy 的 AfterSpawn 中挂载
entity.MountObserverStrategy(entity.Name, "my_game.hp_watcher");
```

观察者的绑定关系通过 `StrategyMetaData.ObserverIndices` 字段持久化到存档中，读档时自动恢复，无需在 AfterLoad 中手动重新挂载。

## 策略执行顺序

同实体上的多个策略按 `Priority` 升序执行，同优先级按添加顺序：

```
Priority: 10  →  Strategy A  (先执行)
Priority: 50  →  Strategy B
Priority: 100 →  Strategy C
Priority: 6205 (默认) → Strategy D
```

所有钩子（Process / AfterSpawn / etc.）均遵循此顺序。

## 实体元数据

实体的序列化格式：

```json
{
  "name": "player",
  "node": {
    "pairs": {
      "main": "res://scenes/player.tscn"
    }
  },
  "strategy": {
    "lifecycle_indices": ["my_game.health", "my_game.movement"],
    "active_indices": ["my_game.move_to"],
    "observer_indices": [
      { "player": ["my_game.hp_watcher"] }
    ]
  },
  "data": {
    "pairs": {
      "hp": { "type": "Int32", "data": 100 },
      "pos": { "type": "Vector2", "data": { "x": 10, "y": 20 } }
    }
  }
}
```

## 下一个文档

- [会话模型](session-model.md) — 前台/后台会话
- [持久化流程](persistence-flow.md) — 存档/读档
- [策略测试](strategy-testing.md) — StrategyTestScenario

---
[↑ 回到 usage](README.md)
