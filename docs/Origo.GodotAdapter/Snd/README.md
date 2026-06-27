# Snd

> [↑ 回到 Origo.GodotAdapter](../README.md) · [↔ Core: Snd](../../Origo.Core/Snd/README.md)

## 概述

SND 实体体系在 Godot 引擎中的具体实现。将 Core 的抽象 `ISndEntity` / `INodeHandle` / `INodeFactory` / `ISndSceneHost` 与 Godot 的 `Node` / `PackedScene` 生命周期对接。

## 包含文件

| 文件 | 职责 |
|------|------|
| `GodotSndManager.cs` | Godot 场景宿主：管理 GodotSndEntity 集合，实现 ISndSceneHost。实体帧处理由 Core 的 `SndRuntime.ProcessAll` 通过 `IOrigoFrameDriver.DriveFrame` 统一驱动 |
| `GodotSndEntity.cs` | Godot 实体：将 Core SndEntity 绑定到 Godot Node 生命周期，委托所有 ISndEntity 调用 |
| `GodotPackedSceneNodeFactory.cs` | INodeFactory 实现：通过 PackedScene.Instantiate 创建 Godot Node |
| `GodotNodeHandle.cs` | INodeHandle 实现：包装 Godot.Node，提供 Free / SetVisible / UnsafeGetNode |
| `SndEntityNodeExtensions.cs` | 适配层便利扩展：`GetNativeNode()`（从 INodeHandle 提取 Godot Node）、`GetNodeFromSnd<T>()`（从 ISndEntity 遍历 Godot 场景树）。物理位置在项目根 `Origo.GodotAdapter/SndEntityNodeExtensions.cs`（非 Snd/ 子目录），命名空间归属 `Origo.GodotAdapter.Snd` |
| `TypedDataInitializer.cs` | 程序集加载强制入口：访问 `IsLoaded` 属性触发所有 `[ModuleInitializer]` 执行 |

## 模块详解

### GodotSndManager

适配层的核心入口节点（`[GlobalClass]`），直接挂载在 Godot 场景树中：

- **实现 ISndSceneHost**：CreateEntity / LoadFromMetaList / ClearAll（框架内部生命周期操作）/ RequestKillEntity / RemoveEntity / GetEntities / FindByName / ProcessAll。`ClearAll()` 使用 `Free()`（即时释放）而非 `QueueFree()`，因 Core 保证在安全的生命周期时机调用。
- **实现 ISndContextAttachableSceneHost**：支持运行时切换上下文
- **回滚机制**：`RecoverFromMetaList` 中若某实体加载失败，回滚释放所有已创建的实体
- **EntityView**：惰性创建 `IReadOnlyList<ISndEntity>` 包装，缓存引用避免重复分配
- **BuildMetaList()**：调用实体的 `BuildSndMetaData()` 收集元数据
- **ProcessAll(delta)**：实现 Core 层 `SndRuntime.ProcessAll` 调用的统一入口，维护 `ProcessTickCount` 和 `ProcessDeltaSum` 统计

### GodotSndEntity

Core `SndEntity` 的 Godot 包装器（`[GlobalClass]`）：

- **延迟初始化**：`_entity` 在首次访问时通过 `SndWorld.CreateEntity` 创建
- **Lifecycle 分离**：`DetachFromManager()` 仅由 GodotSndManager 调用，设置 released 标志、置空 entity 引用、调用 `Free()` 释放节点
- **BuildSndMetaData()**：公开包装 `BuildMetaData()`，供 GodotSndManager 收集元数据
- **IEntityLifecycle 实现**：各方法包含 `EnsureEntity()` 守卫（先用再创建）
- **StableName**：独立存储实体稳定名（Godot Node 的 Name 可能因重名自动修改后缀）
- **GetNodeFromSnd<TNode>**：Godot 特有扩展——从 SND 节点系统按名查找并强转为 Godot 具体类型

### GodotPackedSceneNodeFactory

- **Create**：`ResourceLoader.Load<PackedScene>(resourceId)` → `Instantiate<Node>()` → `parent.AddChild(node)` → 返回 GodotNodeHandle
- resourceId 通过 `SndMappings.ResolveSceneAlias` 解析（支持别名），因此可以是原始 `res://` 路径或别名
- 已加载的 `PackedScene` 实例会缓存，避免同一资源多次实例化时的重复磁盘 I/O

### GodotNodeHandle

- **Name**：构造时缓存的节点名称，不受 Godot 释放原节点的影响
- **Free()** → 检查 `IsInstanceValid(_node)` 后在有效时调用 `_node.Free()`
- **SetVisible(bool)** → 先检查 `IsInstanceValid(_node)`，再根据节点类型（`CanvasItem` 或 `Node3D`）设置对应的 Visible 属性
- **UnsafeGetNode()** → `internal` — 返回底层 `Godot.Node` 引用，仅供 `SndEntityNodeExtensions.GetNativeNode()` 使用

### SndEntityNodeExtensions

- **GetNativeNode(this INodeHandle)** → 安全地将 `INodeHandle` 转换为原生 `Godot.Node`。仅在 handle 是 `GodotNodeHandle` 时生效，否则返回 null
- **GetNodeFromSnd<TNode>(this ISndEntity, string)** → 遍历 Godot 场景树按名查找节点并强转为指定类型。仅在 entity 是 `GodotSndEntity` 时生效

## 设计决策

### 为什么 GodotSndManager 不拥有 _Process 循环

实体帧处理是 Core 编排职责。若 `GodotSndManager` 自持 `_Process` 循环遍历实体调用 `ProcessSnd(delta)`，会重复 `SndRuntime.ProcessAll` 的逻辑并绕过 Core 的正式处理管线。因此帧处理统一由 Core 的 `SndRuntime.ProcessAll(delta)` 通过 `IOrigoFrameDriver.DriveFrame(delta)` 执行，`ProcessTickCount` 和 `ProcessDeltaSum` 也在 `ProcessAll` 中维护。

### 为什么 GodotSndEntity 使用延迟创建 Core Entity

Core `SndEntity` 需要 `INodeFactory` 注入，而 `INodeFactory` 需要 GodotSndEntity 自身作为父节点。构造顺序问题——GodotSndEntity 先创建，再以自身为参数构造 `INodeFactory`，然后通过 factory 创建 Core Entity。延迟创建解决这个循环依赖。

### 为什么 StableName 独立于 Godot Node.Name

Godot 场景树中如果存在同名节点，Godot 会自动在 Name 后追加 `@2`、`@3` 等后缀。SND 的实体查找依赖稳定名称，不能使用被 Godot 篡改的 Name。`StableName` 在 Spawn/Load 时设置，不受 Godot 对 `Node.Name` 自动改名的影响。

### 为什么 LoadFromMetaList 使用回滚机制

如果加载 100 个实体时第 50 个失败，前 49 个已创建的实体处于不完整状态（可能已触发 AfterLoad 钩子但场景不完整）。回滚全部释放防止残留损坏的实体污染后续操作。

### 为什么 DetachFromManager 必须先移出列表再释放

如果在 `_entities` 遍历中直接释放节点，Godot 的节点树变化可能导致后续迭代跳过实体或重复处理。先移除，后释放，保证列表迭代安全。

---
[↑ 回到 Origo.GodotAdapter](../README.md)
