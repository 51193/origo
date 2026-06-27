# Scene

> [↑ 回到 Snd](../README.md)

## 概述

SND 场景宿主实现层。提供 `ISndSceneHost` 的两种实现：完整内存宿主（用于后台会话）、轻量存根宿主（用于测试和设备无关的离线构建）。`SndRuntime` 作为面向上层的门面，组合 `SndWorld` 和 `ISndSceneHost`，并统一编排全部策略生命周期钩子。SceneHost 仅提供实体容器能力。

## 包含文件

| 文件 | 职责 |
|------|------|
| `SndRuntime.cs` | SND 运行时门面：组合 World + SceneHost，统一编排 Spawn/Kill/Save/Quit 策略生命周期钩子，提供 ProcessAll 帧更新 |
| `SndEntityFactory.cs` | 公共工具类：`Spawn(host, meta)` 和 `SpawnMany(host, metas)` 静态方法，委托给 `SndRuntime.SpawnCore/SpawnManyCore` 统一实现 |
| `FullMemorySndSceneHost.cs` | 完整内存场景宿主，创建真实 SndEntity，具备完整策略生命周期 |
| `StubSndSceneHost.cs` | 轻量存根场景宿主，使用简单 StubSndEntity（无策略/节点），用于单元测试和 LevelBuilder 离线构建 |
| `ISndContextAttachableSceneHost.cs` | 接口：允许运行时绑定 ISndContext |
| `NullNodeFactory.cs` | 内存级节点工厂，创建无操作句柄 |

## 模块详解

### SndRuntime — 生命周期编排

`SndRuntime` 不仅组合 `SndWorld` + `ISndSceneHost`，还负责全部策略生命周期钩子的统一编排：

```
SndRuntime = SndWorld (策略池 + 配置) + ISndSceneHost (实体宿主) + 钩子编排
```
> 所有策略生命周期钩子在此统一编排。SceneHost 仅提供容器能力。

**批处理方法**：

| 方法 | 说明 |
|------|------|
| `Spawn(meta)` | 重名校验后调用 `SceneHost.CreateEntity(meta)` 创建实体，再调用 `FireAfterSpawnHooks()` 触发钩子 |
| `SpawnMany(metaList)` | 重名校验后批量 `CreateEntity()` 创建所有实体，再统一 `FireAfterSpawnHooks()` 触发钩子 |
| `KillPendingEntities()` | 收集 IsPendingKill → 全部 `FireBeforeDeadHooks()` → 全部 `ReleaseStrategiesOnly()` + `TeardownOnly()` → 全部 `SceneHost.RemoveEntity(name)` |
| `ProcessAll(delta)` | 透传 `SceneHost.ProcessAll(delta)` |
| `ClearAll()` | 全部 `FireBeforeQuitHooks()` + `ReleaseStrategiesOnly()` + `TeardownOnly()` → `SceneHost.RemoveAllEntities()` |
| `BuildMetaList()` | 透传 `SceneHost.BuildMetaList()`（无 BeforeSave） |

Spawn/SpawnMany 的钩子触发由 SndRuntime 统一在创建完成后调用 `FireAfterSpawnHooks()` 完成。SceneHost.CreateEntity 仅负责创建和恢复，不触发钩子。

### FullMemorySndSceneHost

后台会话的默认场景宿主。关键特性：
- 通过 `SndWorld.CreateEntity` 创建完整 `SndEntity`（非简单内存实体）
- 需延迟绑定 `SndWorld` 和 `ISndContext`（配合 `OrigoRuntime` 两阶段构造）
- **实体容器管理**：只负责创建、查找、移除实体，不触发任何策略钩子
- **CreateEntity**：创建实体，恢复数据/策略/节点（通过 `RecoverForLifecycle`），不触发 AfterSpawn 钩子。多个实体的 AfterSpawn 由 SndRuntime 在批量创建后统一触发。
- **RecoverFromMetaList**：仅恢复实体数据/策略/节点（不触发 AfterLoad 钩子），用于存档加载场景。先通过 `entity.Name = metaData.Name` 设置实体名，再将实体注册到内部集合，最后调用 `RecoverForLifecycle(meta)` 恢复数据/策略/节点（不触发钩子）。因此钩子执行前，`FindByName` 可查找所有已注册实体。
- **RemoveEntity**：从集合移除实体并释放引擎资源（节点/数据），不释放策略引用，不触发钩子
- **RemoveAllEntities**：仅清空内部集合
- **ProcessAll**：基于快照迭代所有实体

### StubSndSceneHost

轻量实现，直接使用内嵌的 `StubSndEntity` 类。这个实体不支持节点访问、策略和订阅，仅支持基础键值数据存取。用于单元测试和 `LevelBuilder` 离线构建。

> `StubSndSceneHost` 的命名表达其"存根"语义——无策略/无节点的轻量占位实现，非完整内存宿主。

### NullNodeFactory / NullNodeHandle

用于 `FullMemorySndSceneHost`。`Create()` 返回不绑定任何引擎节点的句柄，所有操作（`Free`、`SetVisible`）为空操作。Core 层后台会话不需要实际渲染节点。

## 设计决策

### 为什么场景宿主不触发策略钩子

所有策略生命周期钩子的触发由 `SndRuntime` 统一编排。场景宿主仅负责实体容器管理。这种职责分离确保：

- Godot 适配层（`GodotSndManager`）不参与策略生命周期管理
- 批量操作可以在"全部创建/恢复"阶段和"全部触发钩子"阶段之间进行
- 钩子触发期间，所有实体已完全恢复并注册到查找集合，实现加载顺序无关的跨实体互操作

### 为什么需要两个场景宿主

`FullMemorySndSceneHost` 提供完整策略生命周期但需要 `SndWorld` 和 `ISndContext` 的上游依赖；`StubSndSceneHost` 零依赖、完全自治但不能运行策略。前者用于后台会话，后者用于测试和离线构建（测试中通常只测数据流转而无需策略执行）。

### 为什么 SndEntityFactory 委托给 SndRuntime

spawn 逻辑的唯一权威实现是 `SndRuntime` 的内部静态方法 `SpawnCore/SpawnManyCore`，`SndEntityFactory.Spawn/SpawnMany` 委托调用这些方法。单一来源保证修复 bug 或调整行为只需改一处，避免两套 spawn 逻辑产生分歧。

### 为什么 FullMemorySndSceneHost 延迟绑定 World/Context

`OrigoRuntime` 的两阶段构造（先创建宿主，后注入运行时依赖）需要宿主支持延迟绑定。在构造函数中提供这些依赖会形成循环：`SndWorld` 的创建需要 `OrigoRuntime`，而宿主的创建又在 `SndWorld` 之前。

### 为什么实体在钩子触发前先登记到查找集合

策略钩子可能需要在创建期间引用兄弟实体（例如通过 `FindByName` 查找依赖实体、调用 `ObserveData` 建立跨实体观察关系）。先登记后触发钩子保证了所有实体在整个生命周期内始终可被检索。批处理模式下，所有实体先全部登记，再统一触发钩子，进一步加强了这一保证。

---

[↑ 回到 Snd](../README.md)
