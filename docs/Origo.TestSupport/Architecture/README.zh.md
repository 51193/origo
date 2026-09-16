<!-- docsync-pair: Origo.TestSupport/Architecture/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Architecture

> [↑ 回到 Origo.TestSupport](../README.zh.md)

## 概述

测试架构守卫辅助设施。

## 包含文件

| 文件 | 职责 |
|------|------|
| `PrivateFieldNamingConvention.cs` | 反射校验生产程序集私有字段遵循 `_camelCase` 命名 |
| `Metadata/TypedDataTestSupport.cs` | internal 测试复位助手：清空 TypedData kind 注册表并重放 Home 注册；生产代码无测试钩子 |
| `Runtime/SndContextTestFrameDriver.cs` | internal 测试侧帧冲刷扩展：仅排干运行时延迟队列，不处理实体、不泵控制台；生产代码无测试钩子 |

## 设计决策

### 为什么命名规则用反射测试而非 dotnet format

`.editorconfig` 的 private-field naming rule 在 `dotnet format --verify-no-changes` 中是 fix-only，无法作为失败门禁。架构测试通过反射扫描生产程序集私有字段，使命名违规进入正常测试门禁。

### 为什么 TypedData 复位放在测试程序集

全局 kind 注册表是进程级静态状态，测试间需要复位；但 AGENTS §1.2 禁止生产代码为测试便利暴露钩子。`Origo.TestSupport` 经 `InternalsVisibleTo` 访问 internal 注册表并集中复位，生产 `TypedData` 不包含任何测试专用 API。

### 为什么测试帧冲刷经 TestSupport 而非生产 API

帧边界是 Core 的单一访问路径（`IOrigoFrameDriver.DriveFrame`），生产 API 不暴露只冲刷延迟队列的半步骤。SndContext 工作流测试需要验证"入队→冲刷→生效"而不连带实体处理和命令执行，因此 `SndContextTestFrameDriver.FlushFrame` 经 `InternalsVisibleTo` 调用 `OrigoRuntime.FlushEndOfFrameDeferred`；生产 `ISndDeferredActions` 保持封闭。

---
[↑ 回到 Origo.TestSupport](../README.zh.md)
