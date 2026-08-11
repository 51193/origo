<!-- docsync-pair: Origo.Core.Tests/Snd-Scene -->
<!-- docsync-revision: 9 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# SND 场景 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Snd/Scene](../Origo.Core/Snd/Scene/README.zh.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.zh.md)

## 被测行为概览

验证 SND 场景宿主层的实现：StubSndSceneHost 的实体容器操作（CreateEntity/FindByName/RecoverFromMetaList/RemoveAllEntities/BuildMetaList）、
FullMemorySndSceneHost 的绑定前置条件和错误路径、NullNodeFactory 的无渲染行为。

SndEntityFactory 的 spawn 编排和 ProcessAll 帧处理由 SndEntityLifecycleBatchTests 覆盖，参见 [Snd-Entity.md](Snd-Entity.zh.md)。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `MemorySndSceneHostTests.cs` | StubSndSceneHost 的实体增删查改和列表序列化基本行为 |
| `FullMemorySndSceneHostTests.cs` | FullMemorySndSceneHost 的绑定前置条件、CreateEntity/RemoveEntity/RequestKillEntity 错误路径 |
| `NullNodeFactoryTests.cs` | NullNodeFactory 返回 NullNodeHandle，Free/SetVisible 为无操作 |
| `SndEntityFactoryRollbackTests.cs` | 回归：Spawn/SpawnMany 的 AfterSpawn 钩子抛异常时回滚（实体移除、观察者拆线、策略/节点释放、原始异常传播）；detach 失效宿主（Godot wrapper 语义）上回滚顺序正确不遮蔽原始异常；回滚步骤自身抛异常时原始异常仍传播且剩余回滚步骤全部执行 |

## MemorySndSceneHostTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Spawn_AddsEntityAndMeta` | CreateEntity 将实体加入 GetEntities 列表和 BuildMetaList | ISndSceneHost |
| `FindByName_ReturnsEntity` | FindByName 找到已创建实体，不存在返回 null | ISndSceneHost |
| `LoadFromMetaList_DoesNotClearExisting` | RecoverFromMetaList 不清空已有实体（调用者负责清理） | ISndSceneHost |
| `ClearAll_RemovesEntitiesAndMeta` | RemoveAllEntities 后 GetEntities 和 BuildMetaList 均为空 | ISndSceneHost |
| `SerializeMetaList_ReturnsCorrectData` | BuildMetaList 返回当前全部实体元数据 | ISndSceneHost |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Spawn_ThrowsOnNull` | null metadata 参数 | ArgumentNullException |
| `LoadFromMetaList_ThrowsOnNull` | null metaList 参数 | ArgumentNullException |
| `RemoveEntity_Missing_Throws` | 移除不存在的实体名 | InvalidOperationException |

## FullMemorySndSceneHostTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateEntity_ReturnsEntityAndAddsToCollection` | CreateEntity 返回实体，FindByName 可找到，GetEntities 包含 | ISndSceneHost |
| `RemoveEntity_ExistingName_RemovesAndNotFoundAfter` | RemoveEntity 后 FindByName 返回 null | ISndSceneHost |
| `RequestKillEntity_SetsPendingKillTrue` | RequestKillEntity 将 IsPendingKill 设为 true | ISndSceneHost |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `CreateEntity_NullMeta_ThrowsArgumentNull` | null metadata (paramName="metaData") | ArgumentNullException |
| `CreateEntity_BeforeBindWorld_ThrowsInvalidOperation` | BindWorld 未调用 | InvalidOperationException，消息包含 "SndWorld" |
| `CreateEntity_BeforeBindContext_ThrowsInvalidOperation` | BindContext 未调用 | InvalidOperationException，消息包含 "ISndContext" |
| `RemoveEntity_NonexistentName_ThrowsInvalidOperation` | 不存在的实体名称 | InvalidOperationException，消息包含实体名 |
| `RequestKillEntity_DoubleRequest_ThrowsInvalidOperation` | 重复调用 RequestKillEntity | InvalidOperationException，消息包含 "already pending kill" |

## NullNodeFactoryTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `NullNodeFactory_CreatesNullNodeHandle` | Create 返回非 null 句柄，Free/SetVisible 为无操作 | INodeFactory |

## SndEntityFactoryRollbackTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Spawn_AfterSpawnHookThrows_RollsBackEntityAndStrategyReferences` | Spawn 的 AfterSpawn 钩子抛出 InvalidOperationException | 半初始化实体从宿主移除（FindByName 返回 null），已获取的策略引用归还（LogPoolLeaks 无 refCount 告警），原始异常传播 |
| `SpawnMany_AfterSpawnHookThrows_RollsBackUnfiredEntitiesOnly` | SpawnMany 中第二个实体的 AfterSpawn 钩子抛出 InvalidOperationException | 钩子已触发的 E1 保留；钩子未触发的 E2、E3 全部回滚，回滚策略引用归还 |
| `Spawn_AfterSpawnHookThrows_OnDetachInvalidatingHost_PropagatesOriginalException` | detach 使实体包装失效的宿主（Godot wrapper 语义）上 AfterSpawn 抛异常 | 先拆卸再移除，原始异常不被 ObjectDisposedException 遮蔽 |
| `Spawn_AfterSpawnHookThrows_WhenRollbackStepAlsoThrows_PropagatesOriginalAndCompletesRollback` | AfterSpawn 抛异常且回滚时 `TeardownObserverBindings`（OnUnmounted 钩子）也抛 | 原始 AfterSpawn 异常传播；`ReleaseStrategiesOnly`/`TeardownOnly`/`RemoveEntity` 全部仍执行，宿主移除为尽力而为 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `ThrowingAfterSpawnStrategy` | SndEntityFactoryRollbackTests.cs | AfterSpawn 钩子抛出 InvalidOperationException（"Intentional AfterSpawn failure"），验证 Spawn/SpawnMany 回滚 |
| `NormalStrategy` | SndEntityFactoryRollbackTests.cs | 正常生命周期策略，SpawnMany 中钩子正常触发的实体（E1）保留 |
| `NormalTwoStrategy` | SndEntityFactoryRollbackTests.cs | 正常生命周期策略，钩子尚未触发的回滚候选（E3），验证未触发钩子的实体被回滚 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 并发 CreateEntity/RemoveEntity 的线程安全性 | 场景宿主是否承诺线程安全 | ISndSceneHost |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
