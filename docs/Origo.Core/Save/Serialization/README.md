# Serialization (Save)

> [↑ 回到 Save](../README.md)

## 概述

存档序列化层的内部实现。提供黑板（`BlackboardSerializer`）和 SND 场景（`SndSceneSerializer`）的构建/恢复能力，以及聚合这些能力的 `SaveContext`（存档编排上下文）。所有操作均产出 `DataSourceNode`，不直接接触文件 I/O。策略钩子（BeforeSave/AfterLoad）由上层 `SessionRun` 在调用前后统一触发。

## 包含文件

| 文件 | 职责 |
|------|------|
| `BlackboardSerializer.cs` | 黑板 ↔ DataSourceNode（通过 TypedData + ConverterRegistry） |
| `SndSceneSerializer.cs` | SND 场景 ↔ DataSourceNode（通过 SndWorld.WriteMetaListNode / Mappings） |
| `SaveContext.cs` | 存档编排上下文：持有多序列化器 + 构造 SaveGamePayload |

## 模块详解

### BlackboardSerializer

- **Serialize**：`blackboard.SerializeAll()` → `registry.Write<Dict<string,TypedData>>()` → DataSourceNode
- **DeserializeInto**：DataSourceNode → `registry.Read<Dict<string,TypedData>>()` → `blackboard.DeserializeAll(dict)`

`TypedData` 为 struct（值类型），字典中直接存储其内联值，避免额外堆分配。

依赖 `BlackboardDataConverter`（注册在 DataSource.Converters 中）。

### SndSceneSerializer

- **Build**：`sceneAccess.BuildMetaList()` → `_world.WriteMetaListNode(metaList)` → DataSourceNode  
  不触发 BeforeSave——BeforeSave 由 SessionRun.BuildLevelPayload 在调用前批量触发。
- **RecoverInto**：数据必须是 Array 格式 → `SndMappings.ResolveMetaListFromJsonArray` → `sceneAccess.RecoverFromMetaList`  
  不触发 AfterLoad，不执行 ClearAll——AfterLoad 由 SessionRun.LoadFromPayload 在调用后批量触发，ClearAll 由调用方在调用前自行处理。

### SaveContext

Save 模块的核心编排对象，由 `ProgressRun` 持有：

```
SaveContext = IBlackboard(Progress) + IBlackboard(Session) + SndWorld
```

提供统一的构建/恢复入口：

| 方法 | 说明 |
|------|------|
| `BuildSndScene(ISndSceneAccess)` | 构建场景元数据（不触发 BeforeSave） |
| `RecoverSndScene(ISndSceneHost, DataSourceNode)` | 恢复场景（不触发 AfterLoad，不做 ClearAll） |
| `SaveGame(...)` | 收集全部数据构建 `SaveGamePayload` |

## 设计决策

### 为什么序列化产出 DataSourceNode 而非直接出 JSON

分离序列化逻辑和 I/O 逻辑。DataSourceNode 是中立的树形格式，可以被 JSON codec、map codec 或未来的 codec（如 MessagePack）编码。如果直接产出 JSON string，切换存档格式需修改所有序列化器。

### 为什么 SaveContext 持有 Progress 和 Session 两个黑板的引用

流程存档同时包含 Progress（跨关卡共享）和 Session（当前关卡临时状态）。SaveContext 需要两者来完成完整存档。引用而非拷贝确保序列化时拿到的是最新状态（可能在 `BeforeSave` 钩子中被修改）。

### 为什么 SndSceneSerializer 要求输入为 Array 格式

SND 场景是实体数组（`[entity1, entity2, ...]`），不是对象。在入口处校验格式可及早捕获错误（如意外传入了 session.json 而非 snd_scene.json），而非在后续解析中产生歧义。

### 为什么 SndSceneSerializer 不触发策略钩子

BeforeSave 和 AfterLoad 钩子由 `SessionRun` 统一编排：
- **BeforeSave**：`SessionRun.BuildLevelPayload` 先批量触发 `FireBeforeSaveHooks`，再调用 `SaveContext.BuildSndScene`
- **AfterLoad**：`SessionRun.LoadFromPayload` 先调用 `SaveContext.RecoverSndScene`，再批量触发 `FireAfterLoadHooks`

序列化器专注于数据转换，不参与生命周期管理。

---

[↑ 回到 Save](../README.md)
