<!-- docsync-pair: Origo.Core.Tests/META-TEST -->
<!-- docsync-revision: 12 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 测试文档维护元指令

> [↑ 回到 Origo 手册](../README.zh.md)

## 测试文档定位

`docs/` 中的测试文档是 Origo 框架的测试行为镜像。目标是：**快速了解某个能力被哪些测试覆盖、覆盖了哪些正确/错误/边界路径、还有什么缺口**，而无需通读源码。

## 编写原则

### 能力分组优先于目录镜像

- 测试文档按 **被测试的能力** 分组，一个文档描述一种能力的测试
- 若一个源码目录包含多种独立能力，拆分为多个文档（如 `Save/` 拆为 `Save-Storage.md`、`Save-Serialization.md`、`Save-Meta.md`）
- 若多个测试文件共同验证同一种能力，合并到一个文档中分段描述

### 自底向上

1. **测试方法级**：每个测试方法归类为"正确路径/错误路径/边界路径"，在能力文档中以表格列出
2. **能力文档级**：汇总该能力的所有测试文件 + 测试方法表 + 辅助策略 + 覆盖缺口 + 设计决策
3. **模块根**：测试项目 README（`Origo.Core.Tests/README.md`）——列出所有能力文档索引、测试辅助设施、测试策略概述
4. **顶级**：`docs/README.md` 中的测试导航入口

### 链接规范

- **每个文档必须包含向上一层（模块 README）的链接**，格式：`[↑ 回到 Origo.Core.Tests](README.zh.md)`
- **每个能力文档必须包含横向链接**到被测模块的文档，格式：`` `[↔ 被测模块: Origo.Core/Xxx](../Origo.Core/Xxx/README.zh.md)` ``
- **每个能力文档引用 usage/ 中的行为描述时，必须链接到对应文档**
- **禁止孤立文档**：所有测试文档通过链接严格连通到模块 README 和顶级 README

### 内容约定

| 层级 | 内容 |
|------|------|
| 能力文档 | 被测行为概览（引用 usage/ 或模块文档）→ 测试文件清单 → 各文件测试详情（正确/错误/边界表格）→ 辅助策略列表 → 已知覆盖缺口 → 设计决策 |
| 模块 README | 测试策略概述 → 测试辅助设施说明 → 所有能力文档索引（含文件数和测试数） |
| 顶级导航 | 测试导航入口（从 `docs/README.md` 出发的路径） |

### 测试详情表格规范

**正确路径表格**：

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `MethodName` | 简洁描述（一句话） | `usage/xxx.md` 或 `Abstractions/README.md` |

**错误路径表格**：

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `MethodName` | 错误输入描述 | 抛出的异常类型/错误消息关键词 |

**边界路径表格**：

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `MethodName` | 边界条件描述 | 不抛异常 / 返回默认值 / 等 |

### 覆盖缺口规范

- **每个能力文档必须包含"已知覆盖缺口"章节**
- 缺口必须引用文档依据（指明该行为在 usage/ 或模块文档中的位置）
- 缺口格式：表格，含"缺口描述"、"影响"、"文档依据"三列
- 缺口可用于后续测试扩展的优先级排序依据

### 辅助策略规范

- 测试文件内定义的 `private sealed class XxxStrategy` 等辅助策略类必须列入"测试辅助策略"表格
- 需说明每个辅助策略的作用和使用方式（不描述具体实现，只描述它模拟了什么行为）

### 写作风格

- 每个文档开头标注父链接（↑）
- 每个文档末尾标注父链接（便于返回导航）
- 表格清晰列出测试方法和验证行为
- **禁止演进标记**：不得出现"新增"、"旧版"、"v0.x 起"等标记版本演进历史的字样。测试的存在本身即代表当前需要验证的行为。
- **不确定的行为描述必须询问维护者**，不得依据代码实现编造

### InternalsVisibleTo 白名单原则

Origo 将大量编排逻辑（`OrigoRuntime`、`SndWorld`、`SessionRun`、`ProgressRun`、`SndStrategyPool`、`SndStrategyManager` 等）设为 `internal`。测试通过 `InternalsVisibleTo` 访问这些类型，但必须遵守以下白名单原则：

**允许使用 InternalsVisibleTo 的情况（白名单）**：

1. **框架守卫契约**：内部类型在注册/构造时的防御性校验，没有公共 API 可触发
   - 示例：`SndWorld.RegisterStrategy()` 拒绝有实例字段的策略（`AutoInitializerGuardTests`）
   - 示例：`SaveCoordinator` 构造函数 null 参数校验（`SaveCoordinatorTests`）

2. **内部编排的正确性契约**：策略池的引用计数、类型分支安全、回滚行为等
   - 示例：`SndStrategyPool` 的 `GetStrategy`/`ReleaseStrategy` 引用计数正确性
   - 示例：`StackStateMachine` 构造时 `SndStrategyPool` 获取失败的回滚行为
   - 示例：实体分阶段生命周期编排（AfterLoad/AfterSpawn/BeforeSave/BeforeQuit/BeforeDead 的触发时机、LIFO/优先级顺序、跨实体可见性、以及"已创建但钩子未触发""BeforeQuit 已触发但实体仍在集合中"等中间态）通过 `IEntityLifecycle` 分阶段方法 + `FullMemorySndSceneHost` 直接验证（`SndEntityLifecycleBatchTests`）。这些中间态与排序**无法**通过 `ISessionRun` 公共 API 观察，故属白名单。

3. **场景宿主自身契约**：`FullMemorySndSceneHost`/`MemorySndSceneHost`/`StubSndSceneHost` 的 `CreateEntity`/`RemoveEntity`/`RemoveAllEntities`/`ProcessAll`/`RequestKillEntity` 方法契约本身，以及 `SndEntityFactory.Spawn`/`SpawnMany`。这些是被测宿主/工厂的直接 API（见 [Snd-Scene.md](Snd-Scene.zh.md)、[Snd-Entity.md](Snd-Entity.zh.md)）。

4. **性能基准的精确测量**：基准测试（`[Trait("Category","Benchmark")]`）为避免会话层额外开销混入测量，直接操作 `SndStrategyPool`/`FullMemorySndSceneHost`/`IEntityLifecycle`（`SndStrategyPerformanceTests`）。

5. **测试基础设施构造**：`OrigoRuntime`、`SndContextParameters` 等根对象，测试需要构造它们来搭建测试环境

6. **静态方法的直接调用**：`OrigoAutoInitializer.DiscoverAndRegisterStrategies()` 等引导期工具方法

7. **载荷反序列化校验与无公共等价的低层操作**：以下情形没有能忠实复现同一契约的公共路径，故保留内部 API：
   - `DefaultSaveStorageService` 的**隔离契约验证**（`SavePathPolicyContractTests`、`SaveStorageContractTests`）：自定义 `ISavePathPolicy` 注入下逐方法路径断言、以及 `EnumerateSavesWithMetaData`/`SnapshotCurrentToSave`/`WriteSavePayloadToCurrent`/`ReadSavePayloadFromCurrent` 等无公共等价的低层方法——公共 `RequestSaveGame`/`RequestLoadGame` 会连带进度文件与幂等逻辑，无法隔离验证存储服务自身；有公共等价的可观察行为（如 `EnumerateSaveIds` → `ctx.Save.ListSaves()`）必须走公共路径。
   - `LevelBuilder` 的提交委托契约（`LevelBuilder_Commit_UsesStorageService`）：内部类型，无公共等价。
   - `ProgressRun.LoadFromPayload` 对**手工构造的畸形/缺失字段载荷**的校验（畸形/缺失拓扑、null 的 `ProgressStateMachinesNode`）——公共 `RequestLoadGame` 走磁盘，存档写入器会在读档校验前就拒绝这类畸形载荷，无法忠实复现（`ProgressRunSessionLoadingEdgeTests`、`LifecycleRunsTests`）。
   - `ProgressRun.PersistProgress`（仅持久化 progress、不含会话数据）——公共 `RequestSaveGame` 会连带持久化会话，无"仅 progress"的公共等价（`DisposeSemanticsTests`）。
   - `ProgressRun.LoadAndMountForeground(levelId)` 以**任意关卡**作为初始前台挂载的测试基础设施——生产中初始挂载只经入口/存档，无任意关卡初始挂载的公共 API。
   - `ProgressRun.BuildSavePayload`/`LoadFromPayload` 的**内存往返编解码契约**（`PayloadCodec_InMemoryRoundTrip_PreservesState`）——隔离验证序列化编解码本身、不经磁盘；公共 `RequestSaveGame`/`RequestLoadGame` 会把编解码与存储管线耦合，无法隔离验证 codec。

8. **全局状态的测试复位**：`TypedData.ResetForTesting()`（internal）仅用于在测试间复位 TypedData 的 kind 注册表，使各测试以干净状态启动。它是**专为测试存在的复位钩子**，在生产路径中不可达；不得在测试用例之外的代码路径调用。

9. **无公共触发路径的内部故障态注入**：当被测行为的故障态（faulted task、永不完成的 task、端口被占用前已启动的 listener、已损坏的客户端 writer 等）**没有任何公共 API 可以触发**时，允许经反射注入/读取私有字段构造故障态。此类注入比 `InternalsVisibleTo` 更脆弱（字段重命名即运行时失败），必须遵守：
   - 仅用于**该故障态本身无公共触发路径**的场景；可经公共路径触发的场景必须走公共路径（如 `Start_AfterDispose`、`Start_PortInUse` 均走公共调用）
   - 反射字段访问集中在该测试文件内，不扩散到生产代码或其他测试
   - 先例：`Origo.ConsoleBridge.Tests/ConsoleBridgeServerErrorPathTests.cs`（`_acceptTask`/`_listener`/`_writer`/`_started` 字段注入，`ConsoleBridge` 程序集未配置 `InternalsVisibleTo`）；等待性断言必须使用轮询等待（如 `SpinUntil` 等待 `_writer` 非空），禁止固定 `Thread.Sleep` 时序

10. **内部属性作为宿主契约验证入口**：当测试意图是验证**场景宿主自身契约**（白名单第 3 条）但宿主实例来自会话内部时，允许经 `((SessionRun)bg).SceneHost` 等内部属性取得宿主后直接操作宿主方法。这与直接构造宿主（如 `MemorySndSceneHostTests`）属同一白名单类别，区别仅是宿主实例的取得路径。先例：`BackgroundSessionTests.FullMemorySndSceneHost_LoadFromMetaList_ClearsAndLoads`。

**测试项目命名空间偏离记录**：`Origo.GodotAdapter.Integration.Tests` 的 `Runner/`（轻量测试运行器，`[GlobalClass]` Godot 节点）与 `TestSupport/`（集成测试夹具）基础设施类使用 `Origo.GodotAdapter.Integration.Tests.Runner` / `.TestSupport` 子命名空间——这是刻意偏离扁平命名空间约定的设计：这些类型是引擎运行时组件（AutoLoad 节点、Godot 场景对象），而非测试用例；测试用例类本身仍使用扁平命名空间。不适用于其他测试项目。

**禁止使用 InternalsVisibleTo 的情况（应通过公共接口验证）**：

1. **会话生命周期的编排方法**：`SessionRun.PersistLevelState()`、`SessionRun.SerializeToPayload()`、`ProgressRun.LoadFromPayload()`/`BuildSavePayload()`/`SwitchForeground()`、`SessionManager.PersistSession()` 等内部方法的行为应通过 `ctx.Save.RequestSaveGame()`/`RequestLoadGame()`/`RequestSwitchForegroundLevel()` + `ISaveStorageService` 公共流程验证（syncProcess 状态通过 `ProcessAllSessions` 是否处理该会话来间接验证）。仅上述白名单第 7 条所列、无公共等价的低层校验情形除外。

2. **场景宿主内部方法作为行为触发器**：当测试意图是验证实体/策略行为（而非场景宿主自身契约）时，`FullMemorySndSceneHost.ProcessAll()`/`CreateEntity()`/`RemoveEntity()`/`RemoveAllEntities()` 不得作为触发捷径——应通过 `ISessionRun.Spawn`、`ISessionManager.ProcessAllSessions(includeForeground: true)`、`ISessionRun.RequestKillEntity` + `ISessionManager.KillPendingAllSessions()` 公共流程。（验证场景宿主自身契约的测试例外，见上白名单第 3 条。）

3. **实体生成后手工补钩子**：`((IEntityLifecycle)e).FireAfterSpawnHooks()` 等不得用于模拟 spawn——应使用 `ISessionRun.Spawn`（内部已触发 AfterSpawn 钩子）。（验证分阶段编排中间态/排序的批量测试例外，见上白名单第 2 条。**单元级裸实体**（经 `SndWorld.CreateEntity` 直接构造、无宿主/会话包装，用于隔离测试策略挂载行为）因无 `ISessionRun` 公共路径可用，允许直接调用 `IEntityLifecycle` 的分阶段方法（`RecoverForLifecycle`/`FireAfterSpawnHooks`/`FireAfterLoadHooks`/`BuildMetaData`）完成"实体就绪"——`SndEntityFactory.Spawn` 需场景宿主，`ISessionRun.Spawn` 需会话。集成场景（有宿主/会话）仍必须走公共 API。`SaveAndSwitchForegroundIntegrationTests` 属于白名单第 2 条的**宿主契约边界**：其测试意图是验证"钩子期间 FindByName 跨实体可见性"这一宿主中间态契约，故允许在完整会话环境中手工触发钩子，但同套件中验证会话编排的测试（`SaveAndSwitchForegroundTests` 等）必须走 `ISessionRun.Spawn` 公共 API。）

4. **会话挂载键的内部属性**：`SessionRun.MountKey` 应通过 `ISessionManager.Contains()` / `ISessionManager.TryGet()` 验证

5. **SessionManager 的 Clear/LoadSessionFromPayload**：应通过 `DestroySession()` / `ctx.Save.RequestLoadGame()` 验证

**判断标准**：如果我改了内部实现但行为契约不变，这个测试应该仍然通过。如果不能通过公共接口验证到同等的行为语义，则可以使用 `InternalsVisibleTo`。请牢记：`InternalsVisibleTo` 是"白名单"——如无必要，不得使用。

### 测试命名空间约定

所有测试文件统一使用**扁平命名空间**（`Origo.Core.Tests`），不按子目录拆分为 `Origo.Core.Tests.Snd.Strategy` 等子命名空间。所有测试辅助类型（test doubles、helper strategies、factory methods）无需跨命名空间 using 即可互相访问。

**设计决策**：

- **为什么**：xUnit test discovery 不受命名空间影响，扁平命名空间消除跨目录的 using 指令维护成本。测试项目不是 API 库，命名空间层次不会暴露给下游消费者。
- **为什么不拆**：分割子命名空间后，`Snd/Strategy/` 目录的测试文件需要 `using Origo.Core.Tests.TestSupport;` 才能引用 `TestFactory` 等公用设施，反而增加维护开销。

**实现**：此约定通过 `.editorconfig` 规则强制执行——`[Origo.Core.Tests/**/*.cs]` 路径上的 `IDE0130` 诊断已设为 `none`（详见仓库根 `.editorconfig`），其他测试项目同理。

**偏离此约定前**：请与维护者确认设计意图。如需为特定目录启用子命名空间，必须在对应测试能力文档中记录原因。

### 静态可变状态隔离原则

框架要求策略必须无状态（无实例字段、无可写实例属性），这是通过 `SndStrategyPool.Register()` 的反射检查强制执行的。测试中的 spy 策略因此只能使用 `static` 字段收集事件。

但纯 `static` 字段会导致测试间数据污染。解决方案是使用 `AsyncLocal<T>` 包装静态字段：

```csharp
// ✅ 正确：AsyncLocal 隔离，兼容策略池的静态要求
private static readonly AsyncLocal<ICollection<string>?> _events = new();
public static void Bind(ICollection<string> events) => _events.Value = events;

// ❌ 错误：纯静态，测试间污染
private static ICollection<string>? EventSink { get; set; }
```

此原则确保 spy 策略在满足框架约束的同时，各测试拥有独立的事件收集器。

当测试类使用多个基于 `AsyncLocal<T>` 的 spy 策略时，应在测试类上实现 `IDisposable`
并在 `Dispose()` 中统一清理共享状态。xUnit 在每个测试方法之后（无论通过或失败）都会调用 `Dispose()`，
确保即使断言失败也能执行清理：

```csharp
public class MyTests : IDisposable
{
    public void Dispose()
    {
        SpyStrategyA.Events.Clear();
        SpyStrategyB.MountedCalls.Clear();
        GC.SuppressFinalize(this);
    }
}
```

相比为每个测试单独包裹 `try/finally`，这是更推荐的模式——集中清理并保证始终执行。

## 同步规则

### 需同步更新的情况

1. **新增测试文件** → 找到对应能力文档，在"测试文件清单"中添加条目，在详情表格中添加测试方法
2. **新增测试方法** → 更新对应能力文档的正确/错误/边界表格
3. **删除测试方法** → 从表格中移除对应行
4. **测试覆盖了之前记录的缺口** → 将缺口条目从"已知覆盖缺口"移至正确的测试表格中
5. **新增能力测试目录** → 创建新的能力文档
6. **辅助策略增删** → 更新"测试辅助策略"表格

### 无需同步的情况

- 测试辅助策略内部重构（不影响其对外模拟的行为）
- 测试数据/夹具的值调整（不改变验证的行为语义）
- 测试方法名称重命名（需同步更新表格中的方法名，但不改变验证内容）

### 同步检查清单

在测试代码 PR 合并后，检查：
- [ ] 新增的能力是否对应有文档？
- [ ] 新增的测试方法是否已录入正确/错误/边界表格？
- [ ] 删除的测试方法是否已从表格移除？
- [ ] 已知覆盖缺口是否已更新（新增缺口 or 移除已覆盖缺口）？
- [ ] 辅助策略表格是否与测试文件一致？
- [ ] 所有文档链接是否有效？
- [ ] 文档中引用的"文档出处"链接是否仍指向正确位置？

## 文档生成

本测试文档由分析测试代码后手工编写（非自动生成）。质量依赖对测试意图的正确理解和维护者的设计知识。如发现偏差，向手册维护者报告。

---

[↑ 回到 Origo.manual](../README.zh.md)
