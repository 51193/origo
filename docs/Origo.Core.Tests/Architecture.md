# 架构守卫 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/README.md](../Origo.Core/README.md)
> [↔ 被测行为: usage/architecture-overview](../usage/architecture-overview.md)

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
| `ISndContext_ShouldBeCompositionInterface_WithNoOwnMethodDeclarations` | ISndContext 自身不声明任何方法/属性 | Snd Abstraction: ISP |
| `ISndContext_ShouldInheritAllRoleInterfaces` | ISndContext 继承全部 11 个角色接口（含 ISndFileAccess 和 ISndArchiveFileAccess） | Snd Abstraction: ISndContext 组合 |
| `IStateMachineContext_ShouldInheritSharedRoleInterfaces` | IStateMachineContext 继承 ISndBlackboardAccess + ISndDeferredActions | StateMachine Abstraction |
| `Consumer_UsingOnlyPublicInterfaces_CanPerformSaveLoadWorkflow` | 仅通过公共接口完成 save→load 工作流 | architecture-overview: 测试策略 |
| `Consumer_AccessesAllRoleInterfaces_ThroughISndContext` | 通过 ISndContext 可访问全部 11 个角色接口的能力（含 ISndFileAccess 读写文件，ISndArchiveFileAccess 存档内文件） | Snd Abstraction |
| `SaveLoad_TriggeredThroughISndSaveOperations` | Save/Load 通过 ISndSaveOperations 接口触发 | persistence-flow |
| `SessionLifecycle_ManagedThroughISessionManager` | 会话生命周期通过 ISessionManager 管理 | session-model |
| `ISessionRun_ProvidesRuntimeAccess` | ISessionRun 提供黑板/SceneHost/StateMachines 访问 | session-model |
| `SessionManager_ProvidesCreateAndDestroyOperations` | ISessionManager 提供 CreateBackgroundSession/DestroySession | session-model |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Consumer_AccessesAllRoleInterfaces_ThroughISndContext` 中 CloneTemplate 未加载模板时 | 模板未加载 | 返回 null |

## AutoInitializerGuardTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `DiscoverAndRegisterStrategies_WithoutAttribute_Throws` | 程序集中无 StrategyIndex 策略 | InvalidOperationException |
| `SndWorld_RegisterStrategy_WithStatefulInstanceField_Throws` | 有实例字段的策略注册 | InvalidOperationException（含 "invalid instance members"） |
| `SndWorld_RegisterStrategy_WithWritableInstanceProperty_Throws` | 有可写实例属性的策略注册 | InvalidOperationException（含 "invalid instance members"） |

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndWorld_RegisterStrategy_WithOnlyStaticFields_Succeeds` | 仅静态字段的策略注册成功 | snd-entity-model: 策略池规则 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `DiscoverAndRegisterStrategies_WithBroadSkipPrefixes_ReturnsZero` | 跳过所有程序集（Origo 前缀） | 注册 0 个策略 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| Core 公共 API 中不应暴露 internal 类型的具体名字 | API 稳定性 | architecture-overview: public 白名单 |

---

[↑ 回到 Origo.Core.Tests](README.md)
