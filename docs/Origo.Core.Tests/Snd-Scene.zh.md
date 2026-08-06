<!-- docsync-pair: Origo.Core.Tests/Snd-Scene -->
<!-- docsync-revision: 3 -->
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

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本测试文件不定义策略类，纯接口行为测试 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 并发 CreateEntity/RemoveEntity 的线程安全性 | 场景宿主是否承诺线程安全 | ISndSceneHost |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
