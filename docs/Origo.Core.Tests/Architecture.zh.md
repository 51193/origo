<!-- docsync-pair: Origo.Core.Tests/Architecture -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 架构守卫 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/README.md](../Origo.Core/README.zh.md)
> [↔ 被测行为: usage/architecture-overview](../usage/architecture-overview.zh.md)

## 被测行为概览

验证 Origo 的架构约束：Core 程序集不引用 Godot（分层隔离）、ISndContext 是纯组合接口（接口隔离原则）、
策略注册时通过反射无状态校验（拒绝实例字段和可写属性）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `CoreArchitectureGuardrailTests.cs` | 分层隔离、接口组合、消费方可通过纯接口完成完整工作流 |
| `AutoInitializerGuardTests.cs` | 策略无状态校验：实例字段被拒绝、静态字段允许、缺少 StrategyIndex 抛异常 |

## CoreArchitectureGuardrailTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CoreAssembly_ShouldNotReferenceGodot` | Core 程序集不引用任何 Godot 程序集 | architecture-overview: 平台无关 |
| `SceneWriteInterfacesAndSpawnFactory_AreInternal` | `ISndSceneHost`/`ISndSceneAccess`/`ISndContextAttachableSceneHost`/`IOwningSessionBindable` 与 `SndEntityFactory` 为 internal | architecture-overview: 单一访问路径 |
| `PrivateFields_FollowUnderscoreCamelCase` | Core 生产程序集私有字段遵循 `_camelCase` 命名 | .editorconfig 命名规则 |
| `ISndContext_ShouldBeCompositionInterface_WithCompanionProperties` | ISndContext 自身不声明任何方法/属性 | Snd Abstraction: ISP |
| `ISndContext_ShouldExposeAllRoleInterfacesAsCompanionProperties` | ISndContext 以 10 个 companion 属性暴露全部角色接口能力，不通过接口继承 | Snd Abstraction: ISndContext 组合 |
| `SndContext_ShouldNotImplementRoleInterfaces` | SndContext 具体类型不实现任何角色接口（纯组合对象） | Snd Abstraction: ISndContext 组合 |
| `SndContext_CompanionProperties_ShareConsistentState` | 各 companion 属性共享同一黑板实例（SystemBlackboard/ProgressBlackboard） | Snd Abstraction: ISndContext 组合 |
| `IStateMachineContext_ShouldInheritSharedRoleInterfaces` | IStateMachineContext 继承 ISndBlackboardAccess + ISndDeferredActions | StateMachine Abstraction |
| `DeferredFlush_ShouldNotBePublicBusinessSurface` | 帧冲刷仅经 `IOrigoFrameDriver.DriveFrame`；`ISndDeferredActions` 与 `OrigoRuntime` 不再暴露可绕过的 public flush | architecture-overview: 单一访问路径 |
| `IEntityLifecycle_ShouldBeInternal` | IEntityLifecycle 接口为 internal——业务代码不得直接触发生命周期钩子 | Runtime: 生命周期编排 |
| `SndEntity_LifecycleMethods_ShouldBeInternal` | SndEntity.Process 等具体生命周期方法为 internal，仅经框架编排调用 | Runtime: 生命周期编排 |
| `Consumer_UsingOnlyPublicInterfaces_CanPerformSaveLoadWorkflow` | 仅通过公共接口完成 save→load 工作流 | architecture-overview: 测试策略 |
| `Consumer_AccessesAllRoleInterfaces_ThroughISndContext` | 通过 ISndContext 可访问全部角色接口的能力（含 ISndFileAccess 读写文件，ISndArchiveFileAccess 存档内文件） | Snd Abstraction |
| `SaveLoad_TriggeredThroughISndSaveOperations` | Save/Load 通过 ISndSaveOperations 接口触发 | persistence-flow |
| `SessionLifecycle_ManagedThroughISessionManager` | 会话生命周期通过 ISessionManager 管理 | session-model |
| `ISessionRun_ProvidesRuntimeAccess` | ISessionRun 提供黑板/SceneHost/StateMachines 访问 | session-model |
| `SessionManager_ProvidesCreateAndDestroyOperations` | ISessionManager 提供 CreateBackgroundSession/DestroySession | session-model |
| `ConsoleCommandHandlerBase_ShouldBePublic_SoExternalProjectsCanExtendIt` | ConsoleCommandHandlerBase 为 public，外部项目可派生自定义命令处理器 | console-bridge |

## AutoInitializerGuardTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SndWorld_RegisterStrategy_WithStatefulInstanceField_Throws` | 有实例字段的策略注册 | InvalidOperationException（含 "invalid instance members"） |
| `SndWorld_RegisterStrategy_WithWritableInstanceProperty_Throws` | 有可写实例属性的策略注册 | InvalidOperationException（含 "invalid instance members"） |
| `SndWorld_RegisterTypeMappings_NullCallback_Throws` | null 回调 | ArgumentNullException |

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndWorld_RegisterStrategy_WithOnlyStaticFields_Succeeds` | 仅静态字段的策略注册成功 | snd-entity-model: 策略池规则 |
| `SndWorld_RegisterStrategy_WithReadonlyInstanceField_Succeeds` | readonly 实例字段的策略注册成功（框架允许 readonly 字段） | snd-entity-model |
| `SndWorld_WriteMetaListNode_NonListEnumerable_UsesToListPath` | 非 List 的 IEnumerable 作为 meta 列表时走 ToList 路径，正确序列化 | snd-entity-model |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `DiscoverAndRegisterStrategies_WithBroadSkipPrefixes_ReturnsZero` | 跳过所有程序集（Origo 前缀） | 注册 0 个策略 |
| `DiscoverAndRegisterStrategies_SkippingTestAssembly_DoesNotRegisterFromSkippedAssembly` | 跳过测试程序集本身 | 注册 0 个策略 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| Core 公共 API 中不应暴露 internal 类型的具体名字 | API 稳定性 | architecture-overview: public 白名单 |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
