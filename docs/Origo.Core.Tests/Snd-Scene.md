# SND 场景 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Snd/Scene](../Origo.Core/Snd/Scene/README.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.md)

## 被测行为概览

验证 SND 场景宿主层的实现：StubSndSceneHost 的实体容器管理、
FullMemorySndSceneHost 的边界行为、NullNodeFactory 的无操作行为、
SndRuntime 的钩子编排、SndEntityFactory 的公共 API、ProcessAll/Spawn/Kill 批量缩放性能。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `MemorySndSceneHostTests.cs` | SndSceneHost 的 CreateEntity/FindByName/BuildMetaList/RecoverFromMetaList/RemoveAllEntities/RemoveEntity |
| `FullMemorySndSceneHostTests.cs` | FullMemorySndSceneHost 的边界行为：CreateEntity 前置条件、RemoveEntity/RequestKillEntity 错误路径 |
| `NullNodeFactoryTests.cs` | NullNodeFactory 返回 NullNodeHandle，不抛异常 |
| `SndScenePerformanceTests.cs` | 场景操作缩放性能：ProcessAll 实体数量缩放、数据读/写帧模拟、ToArray 快照分配、Spawn/Kill/ClearAll 批量缩放 |

> `SndEntityLifecycleBatchTests.cs` 与 [Snd-Entity.md](Snd-Entity.md) 共享，此处不再重复列出。

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| StubSndSceneHost.CreateEntity 创建实体并加入列表 | CreateEntity 后实体可被 FindByName 找到 | ISndSceneHost |
| BuildMetaList 返回当前实体元数据 | 序列化快照反映当前状态 | ISndSceneHost |
| RecoverFromMetaList 恢复实体列表 | 反序列化后 GetEntities 包含实体 | ISndSceneHost |
| RemoveAllEntities 清空所有实体 | RemoveAllEntities 后 GetEntities 为空 | ISndSceneHost |
| RemoveEntity 移除实体 | RemoveEntity 后实体不可查找 | ISndSceneHost |
| NullNodeFactory.Create 返回 NullNodeHandle | 无渲染模式下工厂返回空节点 | INodeFactory |
| SndRuntime.Spawn 触发 AfterSpawn | CreateEntity + 钩子触发完整完成 | SndRuntime |
| SndRuntime.SpawnMany 批量两阶段 | 全部创建后统一触发钩子 | SndRuntime |
| SndRuntime.KillPendingEntities 全生命周期 | BeforeDead → Release → Teardown → RemoveEntity | SndRuntime |
| SndEntityFactory.Spawn 封装 CreateEntity + AfterSpawn | 公共静态工具正确处理钩子 | SndEntityFactory |
| SndEntityFactory.SpawnMany 批量封装 | 全部创建后统一触发 | SndEntityFactory |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| FindByName 不存在时返回 null | 查询不存在的实体 | 返回 null |
| CreateEntity 不触发 AfterSpawn | 直接调用 CreateEntity | 策略钩子不触发 |
| RemoveEntity 不触发 BeforeDead | 直接调用 RemoveEntity | 策略钩子不触发 |
| Spawn/SpawnMany 处理非 IEntityLifecycle 实体 | StubSndEntity 等无生命周期实体 | 不抛异常，正常返回 |
| ProcessAll 空场景不抛异常 | 无实体的场景 | 不抛异常 |
| ProcessAll 单实体调用策略 Process | 单个实体帧处理 | 策略 Process 被调用，delta 正确传播 |
| ProcessAll 多实体全部被处理 | 多实体帧处理 | 所有实体策略 Process 均被调用 |
| ProcessAll 中 AddStrategy 新策略不执行 | Process 中添加策略 | 当前帧不执行新策略（快照固定） |
| ProcessAll 中 RemoveStrategy 后续策略仍执行 | Process 中移除策略 | 后续策略仍正常执行 |
| FullMemorySndSceneHost CreateEntity 前置条件 | World/Context 未绑定 | InvalidOperationException |
| FullMemorySndSceneHost RemoveEntity 错误路径 | 不存在的名称 | InvalidOperationException |
| FullMemorySndSceneHost RequestKillEntity 双重标记 | 重复 kill | InvalidOperationException |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 并发 CreateEntity/RemoveEntity 的线程安全 | 场景宿主是否承诺线程安全 | ISndSceneHost |

---

[↑ 回到 Origo.Core.Tests](README.md)
