<!-- docsync-pair: Origo.GodotAdapter/Snd/README -->
<!-- docsync-revision: 19 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Snd

> [↑ 回到 Origo.GodotAdapter](../README.zh.md) · [↔ Core: Snd](../../Origo.Core/Snd/README.zh.md)

## 概述

SND 实体体系在 Godot 引擎中的具体实现。将 Core 的抽象 `ISndEntity` / `INodeHandle` / `INodeFactory` / `ISndSceneHost` 与 Godot 的 `Node` / `PackedScene` 生命周期对接。

## 包含文件

| 文件 | 职责 |
|------|------|
| `GodotSndManager.cs` | Godot 场景宿主：管理 GodotSndEntity 集合，实现 ISndSceneHost。实体帧处理由 Core 的 `SessionManager.ProcessAllSessions` 经 `SceneHost.ProcessAll` 通过 `IOrigoFrameDriver.DriveFrame` 统一驱动 |
| `GodotSndEntity.cs` | Godot 实体：将 Core SndEntity 绑定到 Godot Node 生命周期，委托所有 ISndEntity 调用 |
| `SndEntityCollection.cs` | internal — 纯 C# 实体集合：实体增删、批量恢复回滚、击杀标记、帧处理编排，无 Godot 依赖，由测试直接覆盖 |
| `GodotPackedSceneNodeFactory.cs` | INodeFactory 实现：通过 PackedScene.Instantiate 创建 Godot Node |
| `GodotNodeHandle.cs` | INodeHandle 实现：包装 Godot.Node，提供 Free / SetVisible / UnsafeGetNode |
| `SndEntityNodeExtensions.cs` | 适配层便利扩展：`GetNativeNode()`（从 INodeHandle 提取 Godot Node）、`GetNodeFromSnd<T>()`（经 SND 节点注册表按逻辑名解析并强转）。物理位置在项目根 `Origo.GodotAdapter/SndEntityNodeExtensions.cs`（非 Snd/ 子目录），命名空间归属 `Origo.GodotAdapter` |
| `TypedDataInitializer.cs` | internal — 程序集加载强制入口：调用 `EnsureLoaded()` 触发所有 `[ModuleInitializer]` 执行（测试项目经 InternalsVisibleTo 访问） |

> 项目根目录的 `AssemblyAttributes.cs` 声明 `[assembly: SndInlineTypes(startKind: 128, ...)]`，注册 14 种 Godot 引擎类型（Vector2/Vector2I/Vector3/Vector3I/Vector4/Quaternion/Basis/Transform2D/Transform3D/Color/Rect2/Rect2I/Aabb/Plane）到 TypedData 的适配层 Kind 区间（128–141）。

## 模块详解

### GodotSndManager

适配层的核心入口节点（`[GlobalClass]`），直接挂载在 Godot 场景树中：

- **实现 ISndSceneHost**：CreateEntity / RecoverFromMetaList / RemoveAllEntities（框架内部生命周期操作）/ RequestKillEntity / RemoveEntity / ProcessAll 均为**显式接口实现**——业务代码无法在 `GodotSndManager` 具体类型上直接调用这些写操作，只能经 `ISndSceneHost`/`ISndSceneAccess` 接口（Core 内部持有）驱动；公开读操作为 `GetEntities` / `FindByName`。`RemoveAllEntities()` 使用 `Free()`（即时释放）而非 `QueueFree()`，因 Core 保证在安全的生命周期时机调用。
- **实现 ISndContextAttachableSceneHost**：`BindContext` 为**显式接口实现**——上下文绑定是框架启动编排（`SessionRun` 构造 / Bootstrap 流程）驱动的写路径，业务代码无法在具体类型上重绑上下文
- **启动编排封闭**：`BindRuntimeDependencies` 为 `internal`——运行时依赖绑定（World + Logger）同属框架启动编排（由 `OrigoAutoHost` 在 Bootstrap 流程中驱动），业务代码无法在具体类型上重绑运行时依赖
- **实现 IObserverTopologyHost**（internal）：暴露本场景宿主专用的 `ObserverTopology`，供 Core 的观察者挂载/卸载编排使用
- **实现 IOwningSessionBindable**（internal）：`SetOwningSession` 将会话绑定到宿主，供 Core 会话创建流程使用
- **集合逻辑委托**：实体增删、批量恢复回滚、击杀标记、帧处理等编排逻辑集中在纯 C# 的 `SndEntityCollection<T>`（internal，无 Godot 依赖），由测试直接覆盖；GodotSndManager 仅桥接集合与 Godot 节点树（`AddChild`/`RemoveChild`/`Free` 经 `DetachAndFree` 回调注入）
- **`_ExitTree` 越界兜底**：当本管理器节点未经框架的会话 teardown 直接离开场景树（场景切换、业务代码直接 `RemoveChild`/`Free`）时，`_ExitTree` 释放 Core 侧状态：对每个实体执行观察者绑定拆线（退订 + `OnUnmounted` + 归还池引用）与策略释放，随后清空实体集合（引擎已负责节点树的物理释放）。钩子异常仅记 Warning 不中断清理（兜底路径为越界使用而存在）；框架正常路径先清空集合再释放节点，`_ExitTree` 幂等空转
- **回滚机制**：`RecoverFromMetaList` 中若某实体加载失败，集合回滚释放所有已创建的实体（经 `SndEntityCollection` 的 staged 列表）
- **GetEntities()**：返回**快照**（非实时视图）——与 Core 宿主契约一致：迭代期间宿主被修改不会抛 "collection was modified"，且快照不可下转型为可变后备列表（杜绝绕过集合管理的手工修改）
- **BuildMetaList()**：经集合调用实体的 `BuildSndMetaData()` 收集元数据
- **ProcessAll(delta)**：实现 `ISndSceneHost.ProcessAll` 的统一入口，由 Core 的 `SessionManager.ProcessAllSessions` 调用，驱动集合内每个实体的帧处理

### GodotSndEntity

Core `SndEntity` 的 Godot 包装器（`[GlobalClass]`）：

> **`[GlobalClass]` 限制**：`GodotSndEntity` 的唯一构造函数是 internal（五参数依赖注入），无无参构造函数——因此它不能在编辑器中手动创建或从 `.tscn` 实例化。实体必须经 `GodotSndManager` 创建（`CreateEntity`），这是刻意设计：`GodotSndEntity` 的依赖（`SndWorld`、`ISndContext`、日志、观察者拓扑）只能由框架注入，`[GlobalClass]` 仅用于让 Godot 编辑器识别该类型（导出属性/类型注册）。

- **延迟初始化**：`_entity` 在首次访问时通过 `SndWorld.CreateEntity` 创建
- **Lifecycle 分离**：`DetachFromManager()` 设置 released 标志并置空 entity 引用；引擎级释放（`RemoveChild`/`Free`）由 GodotSndManager 的 `DetachAndFree` 回调执行（`SndEntityCollection` 的"引擎工作委托注入"契约）
- **BuildSndMetaData()**：公开包装 `BuildMetaData()`，供 GodotSndManager 收集元数据
- **IEntityLifecycle 实现**：各方法包含 `EnsureEntity()` 守卫（先用再创建）
- **StableName**：独立存储实体稳定名（Godot Node 的 Name 可能因重名自动修改后缀）
- **GetNodeFromSnd<TNode>**：Godot 特有扩展——从实体的 SND 节点注册表按逻辑名查找并强转为 Godot 具体类型

### GodotPackedSceneNodeFactory

- **Create**：`ResourceLoader.Load<PackedScene>(resourceId)` → `Instantiate<Node>()` → `parent.AddChild(node)` → 返回 GodotNodeHandle
- resourceId 在 Core 侧（`SndWorld` 创建实体时传入 `SndMappings.ResolveSceneAlias` 委托）已解析为最终路径（支持别名），因此工厂收到的始终是原始 `res://` 路径或已解析路径
- 已加载的 `PackedScene` 实例会缓存，避免同一资源多次实例化时的重复磁盘 I/O

### GodotNodeHandle

- **Name**：构造时缓存的节点名称，不受 Godot 释放原节点的影响
- **Free()** → 检查 `IsInstanceValid(_node)` 后在有效时调用 `_node.Free()`
- **SetVisible(bool)** → 先检查 `IsInstanceValid(_node)`，再根据节点类型（`CanvasItem` 或 `Node3D`）设置对应的 Visible 属性；对其他无 `Visible` 属性的节点类型抛 `InvalidOperationException`（fail-fast，避免静默无操作）
- **UnsafeGetNode()** → `internal` — 返回底层 `Godot.Node` 引用，仅供 `SndEntityNodeExtensions.GetNativeNode()` 使用

### SndEntityNodeExtensions

- **GetNativeNode(this INodeHandle)** → 安全地将 `INodeHandle` 转换为原生 `Godot.Node`。仅在 handle 是 `GodotNodeHandle` 时生效，否则返回 null
- **GetNodeFromSnd<TNode>(this ISndEntity, string)** → 经实体的 SND 节点注册表按逻辑名解析节点并强转为指定类型；未注册名抛 `InvalidOperationException`，句柄非 Godot 或类型不符返回 null。仅在 entity 是 `GodotSndEntity` 时生效

## 设计决策

### 为什么 GodotSndManager 不拥有 _Process 循环

实体帧处理是 Core 编排职责。若 `GodotSndManager` 自持 `_Process` 循环遍历实体调用 `ProcessSnd(delta)`，会重复 Core 的帧处理逻辑并绕过正式处理管线。因此帧处理统一由 Core 的 `SessionManager.ProcessAllSessions(delta)` 经 `SceneHost.ProcessAll(delta)`、通过 `IOrigoFrameDriver.DriveFrame(delta)` 执行。`ProcessSnd` 为 `internal`——生命周期编排只能经 Core 的 `ISessionRun` 与批量钩子管线触发，外部代码不得经 `GodotSndEntity` 具体类型直接调用。

### 为什么 GodotSndEntity 使用延迟创建 Core Entity

Core `SndEntity` 需要 `INodeFactory` 注入，而 `INodeFactory` 需要 GodotSndEntity 自身作为父节点。构造顺序问题——GodotSndEntity 先创建，再以自身为参数构造 `INodeFactory`，然后通过 factory 创建 Core Entity。延迟创建解决这个循环依赖。

### 为什么 StableName 独立于 Godot Node.Name

Godot 场景树中如果存在同名节点，Godot 会自动在 Name 后追加 `@2`、`@3` 等后缀。SND 的实体查找依赖稳定名称，不能使用被 Godot 篡改的 Name。`StableName` 在 Spawn/Load 时设置，不受 Godot 对 `Node.Name` 自动改名的影响。

### 为什么 RecoverFromMetaList 使用回滚机制

如果加载 100 个实体时第 50 个失败，前 49 个已创建的实体处于不完整状态（可能已触发 AfterLoad 钩子但场景不完整）。回滚全部释放防止残留损坏的实体污染后续操作。

### 为什么 DetachFromManager 必须先移出列表再释放

如果在 `_entities` 遍历中直接释放节点，Godot 的节点树变化可能导致后续迭代跳过实体或重复处理。先移除，后释放，保证列表迭代安全。

### 适配层实体桥接：为什么 GodotSndEntity 必须手写转发

`GodotSndEntity`（约 238 行）的代码可分解为三类：

| 类别 | 行数 | 占比 | 说明 |
|------|------|------|------|
| 纯转发样板 | ~120 | 60% | `ISndEntity`（~20 个方法）、`IEntityLifecycle`（10 个方法）、`ISndEntityRawSubscription`（2 个方法）全部形如 `Entity.Foo(...)`，每个方法 1~6 行 |
| 引擎特有逻辑 | ~60 | 30% | `StableName` ↔ `Node.Name` 同步、`DetachFromManager()` 状态清理、`GetNodeFromSnd<TNode>()` Godot 节点转义、`EnsureEntity()` 延迟创建 |
| 基础设施 | ~20 | 10% | 字段声明、构造函数、Guard 方法、using |

#### 为什么不能提取基类或自动生成

**C# 单继承是根本约束。** `GodotSndEntity` 必须继承 `Godot.Node`（`[GlobalClass]` 要求）才能挂载到 Godot 场景树。若在 Core 中提供抽象基类 `SndEntityBridge`，Godot 适配器无法同时继承基类和 `Node`。Unity 同理（必须继承 `MonoBehaviour`）。

其他技术路线也缺乏投入产出比：

- **接口默认方法（DIMs）**：要求修改 `ISndEntity` 等核心接口的契约设计，添加抽象属性作为间接访问点，与当前 ISP 拆分方向相悖，且 `IEntityLifecycle` 显式接口实现语义与 DIMs 冲突。
- **源生成器自动生成转发**：为节省 ~130 行一次性样板代码，需要新增一个 Roslyn 增量源生成器（约 200-300 行），并承担其维护负担。而实体桥接在整个引擎适配工作量中仅占约 5%，真正成本在场景宿主、文件系统、序列化等组件。
- **独立委托对象**（`SndEntityProxy`）：将转发代码从适配器移到委托类，适配器仍需实现 `ISndEntity` 将调用转发到委托对象，没有减少任何样板。

综上，C# 单继承约束下，当前手写转发方案已是最优解，不值得为此投入代码变更。

#### 未来适配层作者的参考

当一个新引擎适配器需要实现自己的实体桥接时，以下是可以直接复制的纯转发样板（方法签名和实现完全相同，只需替换类型名）：

```
ISndEntity:
  SetData / GetData / TryGetData
  GetNode / GetNodeNames
  AddStrategy / RemoveStrategy
  AddActiveStrategy / RemoveActiveStrategy / InvokeStrategy
  MountObserverStrategy / UnmountObserverStrategy（两组重载）

IEntityLifecycle（显式接口实现）:
  RecoverForLifecycle / FireAfterSpawnHooks / FireAfterLoadHooks
  FireBeforeSaveHooks / FireBeforeQuitHooks / FireBeforeDeadHooks
  ReleaseStrategiesOnly / TeardownOnly / TeardownObserverBindings / BuildMetaData

ISndEntityRawSubscription（显式接口实现）:
  SubscribeDataRaw / UnsubscribeDataRaw
```

以下必须在每个适配器中重写（引擎特有部分）：

| 逻辑 | Godot 实现 | Unity 需替换为 |
|------|-----------|---------------|
| 引擎基类 | `: Node` | `: MonoBehaviour` |
| 实体清理 | `Free()` | `Destroy(gameObject)` |
| 名称同步 | `StableName` / `Node.Name` | 等价概念（`gameObject.name`） |
| 节点访问 | `GetNodeFromSnd<TNode>()` 返回 `Godot.Node` | 返回 `UnityEngine.GameObject` / `Component` |
| 节点工厂 | `GodotPackedSceneNodeFactory`，以自身为父节点 | `UnityPrefabNodeFactory`，实例化到自身 Transform 下 |
| 延迟创建 | `_world.CreateEntity(nodeFactory, ...)` | 相同调用，但传入 Unity 的 `INodeFactory` |

**预估工作量**：整个实体桥接部分约 2 小时（130 行机械转发复制 + 60 行引擎 API 替换），占适配层总工作量的不到 5%。真正的适配成本在 `ISndSceneHost`（场景实体管理）、`IFileSystem`（引擎文件 API）、`ILogger`（引擎日志 API）、`INodeFactory/INodeHandle`（节点生命周期）、Bootstrap 等组件。

---

[↑ 回到 Origo.GodotAdapter](../README.zh.md)
