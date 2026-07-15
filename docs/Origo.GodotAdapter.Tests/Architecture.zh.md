<!-- docsync-pair: Origo.GodotAdapter.Tests/Architecture -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 架构守卫 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
> [↔ 被测模块: Origo.GodotAdapter/Bootstrap](../Origo.GodotAdapter/Bootstrap/README.zh.md)
> [↔ 被测模块: Origo.GodotAdapter/Console](../Origo.GodotAdapter/Console/README.zh.md)
> [↔ 被测行为: usage/session-model](../usage/session-model.zh.md)

## 被测行为概览

验证 Godot 适配层创建的 SndContext 通过公共接口正确提供全部角色能力（黑板访问、延迟队列、
会话管理、Save/Load、生命周期、控制台、文件访问），会话生命周期通过 `ISessionManager` 管理；
并守护适配层 `CommandHandlerBase` 的公共可见性，使外部项目（如 origo.demo）可派生自定义控制台命令处理器。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `AdapterArchitectureGuardrailTests.cs` | SndContext 公共接口完整性（含 ISndFileAccess/ISndArchiveFileAccess）、后台会话创建/销毁/数据读写；`CommandHandlerBase` 公共可见性守卫 |

## AdapterArchitectureGuardrailTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndContext_AllRoleInterfaces_AreAccessibleThroughISndContext` | ISndContext 可 cast 为各角色接口（Blackboard/Deferred/Save/Lifecycle/Console/FileAccess/ArchiveFileAccess）并使用 | Abstractions: ISndContext |
| `SndContext_ViaSessionManager_CanCreateAndDestroyBackgroundSessions` | 通过 ISessionManager 创建后台会话、读写会话黑板、Contains 校验、DestroySession 销毁 | session-model |
| `CommandHandlerBase_ShouldBePublic_SoExternalProjectsCanExtendIt` | `Origo.GodotAdapter.Console.CommandHandlerBase` 为 public（或嵌套 public），外部项目可派生 | Origo.GodotAdapter/Console |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `InMemoryLogger` | `AdapterArchitectureGuardrailTests.cs` | 最小 `ILogger` 替身，吞掉所有日志，仅用于构造 `OrigoRuntime` |
| `InMemorySndSceneHost` | `AdapterArchitectureGuardrailTests.cs` | 内存 `ISndSceneHost` 替身，维护实体列表，支持创建/移除/恢复元数据列表 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 适配层创建 SndContext 时角色接口的真实 Godot 引擎运行时路径未覆盖（测试使用内存替身宿主） | Godot 引擎级行为未在测试中直接验证 | Origo.GodotAdapter/Bootstrap |
| 后台会话与前台会话并存、跨会话切换的架构守卫未覆盖 | 多会话拓扑下的接口契约未验证 | session-model |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
