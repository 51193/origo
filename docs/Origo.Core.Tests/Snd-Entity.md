# SND 实体 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Snd/Entity](../Origo.Core/Snd/Entity/README.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.md)

## 被测行为概览

验证 SndEntity 聚合根的行为：StubSndEntity 的数据 CRUD、AfterLoad 钩子触发、
AutoInitializer 的策略/数据恢复、批量生命周期（SndEntityLifecycleBatchTests）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `MemorySndEntityTests.cs` | SndEntity 的 SetData/GetData/TryGetData/数据隔离 |
| `SndEntityAfterLoadTests.cs` | AfterLoad 钩子的触发顺序和数据恢复 |
| `SndEntityAndAutoInitializerTests.cs` | AutoInitializer 从 metadata 恢复策略和数据 |
| `SndEntityLifecycleBatchTests.cs` | 批量生命周期：SpawnEntity/RecoverFromMetaList/RemoveAllEntities 整体操作，钩子由上层统一触发；ProcessAll 多实体帧处理 |

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| StubSndEntity SetData/TryGetData 往返 | 数据正确存取，类型保留 | snd-entity-model: TypedData |
| AfterLoad 钩子按预期顺序触发 | Load 恢复后策略 AfterLoad 被调用 | snd-entity-model: 生命周期钩子 |
| AutoInitializer 从 SndMetaData 恢复策略列表 | 策略索引从 metadata 正确加载 | Snd/Entity |
| KillAll 触发 BeforeDead 钩子 | 通过 IEntityLifecycle.FireBeforeDeadHooks 触发 | snd-entity-model: 批量生命周期 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| 空 metadata 恢复 | 无策略索引和数据 | 不抛异常，实体可用 |
| 数据隔离 | 不同实体数据互不影响 | 独立数据空间 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| AutoInitializer 恢复时 metadata 类型不匹配 | 损坏的 metadata 数据 | Snd/Entity |
| AfterLoad 在策略已存在时增量添加 | AfterLoad 后动态 AddStrategy 的行为 | snd-entity-model |

---

[↑ 回到 Origo.Core.Tests](README.md)
