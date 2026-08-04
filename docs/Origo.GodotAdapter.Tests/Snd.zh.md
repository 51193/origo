<!-- docsync-pair: Origo.GodotAdapter.Tests/Snd -->
<!-- docsync-revision: 1 -->
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
| `Snd/TypedDataInitializerTests.cs` | `TypedDataInitializer.EnsureLoaded()` 触发适配层 Kind 注册的幂等性与可用性 |
| `SndEntityNodeExtensionsTests.cs` | `GetNodeFromSnd<T>()` / `GetNativeNode()` 的契约：类型不符抛异常、节点句柄提取 |

## SndEntityCollectionTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateEntity` 系列 | 创建实体加入集合、`FindByName`/`GetEntities` 可见、`OwningSession` 在会话绑定时自动绑定 | Origo.GodotAdapter/Snd |
| `RecoverFromMetaList` 系列 | 批量恢复成功全部入列；`BuildMetaList` 与恢复元数据一一对应 | Origo.GodotAdapter/Snd |
| `RemoveEntity` / `RemoveAllEntities` 系列 | 移除实体经 detach 回调释放引擎节点、列表清空、`GetEntities` 视图同步 | Origo.GodotAdapter/Snd |
| `RequestKillEntity` 系列 | 击杀标记立即生效、重复击杀抛异常、`ProcessAll` 帧处理计数 | Origo.GodotAdapter/Snd |

### 错误路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RecoverFromMetaList` 部分失败 | 第 N 个实体恢复失败时回滚全部已 staged 实体（集合为空、detach 回调逐一触发） | Origo.GodotAdapter/Snd |
| `FindByName` 不存在 | 返回 null；`RemoveEntity` 不存在时抛 `InvalidOperationException` | Origo.GodotAdapter/Snd |

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
