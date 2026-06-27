# 运行时核心 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Runtime](../Origo.Core/Runtime/README.md)
> [↔ 被测行为: usage/architecture-overview](../usage/architecture-overview.md)

## 被测行为概览

验证 OrigoRuntime 的基础构造和控制台注入、延迟动作队列的帧末刷新、实体 Kill 标记。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `OrigoRuntimeBasicTests.cs` | OrigoRuntime 构造、SndWorld 创建、控制台注入/未注入、ResetConsoleState、FlushEndOfFrameDeferred、IOrigoFrameDriver.DriveFrame |
| `ContextBoundaryTests.cs` | SndContext 边界：上下文能力和访问控制 |
| `EntityKillTests.cs` | 实体 Kill/KillAll：标记为待销毁、帧末执行 |
| `SchedulingAndTypeMappingTests.cs` | 调度器与类型映射的集成 |

## OrigoRuntimeBasicTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `OrigoRuntime_Constructor_CreatesSndWorld` | 构造后 SndWorld 和 Logger 可用 | Runtime: OrigoRuntime |
| `OrigoRuntime_ConsoleInputBuffer_NullWithoutInjection` | 未注入控制台时 Console 相关属性为 null | Runtime: Console |
| `OrigoRuntime_WithConsole_CreatesConsole` | 注入控制台输入/输出后 Console 可用 | Runtime: Console |
| `OrigoRuntime_ResetConsoleState_ClearsInputQueue` | 重置只清理输入队列 | Runtime: Console |
| `OrigoRuntime_FlushEndOfFrameDeferred_ExecutesDeferredActions` | Business 和 System 延迟动作全部执行 | Scheduling |
| `OrigoRuntime_DriveFrame_DelegatesToFlushAndConsole` | IOrigoFrameDriver.DriveFrame(delta) 执行业务延迟队列并处理控制台 pending | Abstractions/Runtime: IOrigoFrameDriver |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| OrigoRuntime 在构造后立即 Dispose 的行为 | 资源释放的正确性 | Runtime |
| SystemBlackboard 在多个 ProgressRun 间的隔离 | 系统黑板是否跨 Progress 共享 | Runtime: 四层运行时 |

---

[↑ 回到 Origo.Core.Tests](README.md)
