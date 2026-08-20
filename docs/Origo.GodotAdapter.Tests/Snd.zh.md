<!-- docsync-pair: Origo.GodotAdapter.Tests/Snd -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# SND 实体 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
> [↔ 被测模块: Origo.GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.zh.md)

## 被测行为概览

验证适配层 SND 实体体系中**不依赖 Godot 运行时**的部分：纯 C# 实体集合
`SndEntityCollection<T>` 的增删/查找/批量恢复回滚/击杀标记/帧处理编排，TypedData
适配层注册的强制加载入口，以及 `GetNodeFromSnd<T>` / `GetNativeNode` 扩展的契约。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `Snd/SndEntityCollectionTests.cs` | 实体集合全能力：创建/查找/移除/击杀标记、`RecoverFromMetaList` 批量恢复与部分失败回滚、`RemoveAllEntities`、帧处理 `ProcessAll`、元数据列表构建、`OwningSession` 绑定 |
| `Snd/TypedDataAssemblyLoadTests.cs` | 通过引用公开 GodotAdapter 类型强制程序集加载，验证生成的 `[ModuleInitializer]` 完成适配层 Kind 注册 |
| `SndEntityNodeExtensionsTests.cs` | `GetNodeFromSnd<T>()` / `GetNativeNode()` 的契约：非 Godot 实体/句柄返回 null、节点句柄提取 |

## SndEntityCollectionTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateEntity` 系列 | 创建实体加入集合、`FindByName`/`GetEntities` 可见、`OwningSession` 在会话绑定时自动绑定 | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList` 系列 | 批量恢复成功全部入列；`BuildMetaList` 与恢复元数据一一对应 | Origo.GodotAdapter/Snd |
| `RemoveEntity` / `RemoveAllEntities` 系列 | 移除实体经 detach 回调释放引擎节点、列表清空、`GetEntities` 视图同步 | Origo.GodotAdapter/Snd |
| `RequestKillEntity` 系列 | 击杀标记立即生效、重复击杀抛异常、`ProcessAll` 帧处理计数 | Origo.GodotAdapter/Snd |
| `CreateEntity_AddsAndRecovers` | 创建实体加入集合且 `RecoverForLifecycle` 被调用（StableName 设置、计数正确） | Origo.GodotAdapter/Snd |
| `CreateEntity_OwningSession_BindsEntity` | `OwningSession` 已绑定时创建的实体自动绑定同一会话 | Origo.GodotAdapter/Snd |
| `FindByName_ReturnsEntity` | 按名查找返回实体；不存在返回 null | Origo.GodotAdapter/Snd |
| `GetEntities_ReturnsAllAndIsEnumerable` | `GetEntities` 返回全部实体，集合可直接枚举 | Origo.GodotAdapter/Snd |
| `GetEntities_ReturnsSnapshot_NotTheMutableBackingList` | `GetEntities` 返回快照副本（非可变后备列表、不可下转型绕过集合管理，后续变更不影响已取得的视图） | Origo.GodotAdapter/Snd |
| `ProcessAll_ProcessesEveryEntity` | 帧处理对每个实体各调用一次 `ProcessSnd` | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList_RecoversAll` | 批量恢复全部入列、可按名查找 | Origo.GodotAdapter/Snd |
| `RemoveEntity_DetachesAndRemoves` | 移除实体触发 detach 回调并从集合删除 | Origo.GodotAdapter/Snd |
| `RemoveAllEntities_ClearsCollection` | 清空集合且 detach 回调逐一触发（b、a 逆序） | Origo.GodotAdapter/Snd |
| `RequestKillEntity_MarksPending` | 击杀标记立即生效（`IsPendingKill`=true） | Origo.GodotAdapter/Snd |
| `BuildMetaList_ReturnsAllMetadata` | 按序构建全部实体元数据（a、b） | Origo.GodotAdapter/Snd |

### 错误路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RecoverFromMetaList` 部分失败 | 第 N 个实体恢复失败时回滚全部已 staged 实体（集合为空、detach 回调逐一触发） | Origo.GodotAdapter/Snd |
| `FindByName` 不存在 | 返回 null；`RemoveEntity` 不存在时抛 `InvalidOperationException` | Origo.GodotAdapter/Snd |
| `CreateEntity_NullMeta_Throws` | meta 为 null 时抛 `ArgumentNullException` | Origo.GodotAdapter/Snd |
| `CreateEntity_RecoverFailure_RollsBackAndPropagates` | 创建时恢复失败：异常上抛、集合回滚为空、detach 回调触发 | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList_Failure_RollsBackStaged` | 第 N 个实体恢复失败时回滚全部已 staged 实体（集合为空、detach 回调触发） | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList_Failure_ReportsFailingMeta` | 恢复失败经失败回调报告失败的 meta 与异常 | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList_Null_Throws` | metaList 为 null 时抛 `ArgumentNullException` | Origo.GodotAdapter/Snd |
| `RemoveEntity_Unknown_Throws` | 移除不存在的实体抛 `InvalidOperationException` | Origo.GodotAdapter/Snd |
| `RequestKillEntity_AlreadyPending_Throws` | 对已标记击杀的实体重复击杀抛 `InvalidOperationException` | Origo.GodotAdapter/Snd |
| `ProcessAll_ContainerModifiedDuringProcess_Throws` | 帧处理期间集合被修改（实体在 ProcessSnd 中新增） | 抛 `InvalidOperationException`（消息含 "modified during ProcessAll"；与 FullMemorySndSceneHost 一致） |
| `RequestKillEntity_Unknown_Throws` | 击杀不存在的实体抛 `InvalidOperationException` | Origo.GodotAdapter/Snd |

## TypedDataAssemblyLoadTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GodotAdapterAssemblyLoad_RegistersTypedDataKinds` | 引用公开 GodotAdapter 类型触发程序集加载和生成的 `[ModuleInitializer]`，Vector2 解析为 Kind 128 | Origo.GodotAdapter/Snd |

## SndEntityNodeExtensionsTests 测试详情

### 错误路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GetNativeNode_NonGodotHandle_ReturnsNull` | `GetNativeNode()` 在节点句柄非 Godot 节点句柄时返回 null（契约违约静默降级，不崩溃） | Origo.GodotAdapter/Snd |
| `GetNodeFromSnd_NonGodotEntity_ReturnsNull` | `GetNodeFromSnd<T>()` 在实体非 Godot 实体时返回 null | Origo.GodotAdapter/Snd |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `SndEntityCollectionTests` 内嵌假实体 | `SndEntityCollectionTests.cs` | 实现 `ISndEntityFacade` 的纯 C# 假实体（无 Godot 依赖），记录 `RecoverForLifecycle`/`DetachFromManager` 调用 |
| `InMemoryLogger` | `SndEntityCollectionTests.cs` | 最小 `ILogger` 替身 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 真实 `GodotSndEntity` / `GodotSndManager` 的引擎侧行为（节点树操作、`DetachAndFree` 回调）不在本层覆盖 | 由 `Origo.GodotAdapter.Integration.Tests` 在 Godot `--headless` 运行时中兜底 | Origo.GodotAdapter.Integration.Tests/README |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
