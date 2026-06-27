# 架构守卫 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)
> [↔ 被测模块: Origo.GodotAdapter/Bootstrap](../Origo.GodotAdapter/Bootstrap/README.md)

## 被测行为概览

验证 Godot 适配层创建的 SndContext 通过公共接口正确提供全部角色能力（黑板访问、延迟队列、
会话管理、Save/Load 操作），会话生命周期通过 ISessionManager 管理。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `AdapterArchitectureGuardrailTests.cs` | SndContext 公共接口完整性：7 种角色接口全部可用（含 ISndFileAccess）、后台会话创建/销毁/数据读写 |

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndContext_AllRoleInterfaces_AreAccessibleThroughISndContext` | ISndContext 可访问 7 种角色接口（Blackboard/Deferred/Session/Save/Lifecycle/Console/FileAccess） | Abstractions: ISndContext |
| `SndContext_ViaSessionManager_CanCreateAndDestroyBackgroundSessions` | 通过 ISessionManager 创建/销毁后台会话，数据读写正确 | session-model |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.md)
