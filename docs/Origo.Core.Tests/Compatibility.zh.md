<!-- docsync-pair: Origo.Core.Tests/Compatibility -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Shell 兼容契约 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core.Kernel/Ports](../Origo.Core.Kernel/Ports/README.zh.md)
> [↔ 被测行为: architecture/shell-kernel-boundary](../architecture/shell-kernel-boundary.zh.md)
> [↔ 存档格式: Save-Storage](Save-Storage.zh.md)

## 被测行为概览

验证 shell 消费者看到的生命周期顺序、观察者恢复、fail-fast 校验和会话状态转换在 kernel 版本演进时保持不变，并验证 kernel-shell port 只调用既有编排路径、不通过旁路绕过校验与绑定。

测试通过 `OrigoHost` 和 `ISndContext` / `ISessionRun` / `ISndWorldAccess` 等稳定 Contracts 入口驱动；除测试基础设施（内存文件系统、事件收集辅助）外，不依赖 `Origo.Core.Kernel` 类型。golden 存档格式与缺少版本键的存档/未来版本失败语义记录在 [Save-Storage.md](Save-Storage.zh.md)。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `Compatibility/ShellCompatibilityContractTests.cs` | `OrigoHost` shell 入口：生命周期钩子顺序、观察者绑定恢复、Bootstrap 后 fail-fast、后台会话创建/销毁状态转换 |
| `Hosting/OrigoHostTests.cs` | `OrigoHost`/`HostKernelPort` 稳定运行时构造与后台工作流；`AdapterHostKernelPort` 的 runtime/observer/context 绑定顺序；缺失 runtime binder、context binder 或 file system 时的显式失败 |

## ShellCompatibilityContractTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ShellEntry_LifecycleOrdering_IsPreservedThroughSaveLoad` | 通过 `OrigoHost` Spawn/DriveFrame/Save/Load/Kill：AfterSpawn → Process → BeforeSave → AfterLoad → BeforeDead 顺序保持，加载后实体数据与钩子行为一致 | [shell-kernel-boundary](../architecture/shell-kernel-boundary.zh.md) |
| `ShellEntry_ObserverRecovery_IsPreservedThroughSaveLoad` | observer 显式挂载后 OnMounted/OnDataChanged 生效；保存并加载后持久化的 observer binding 恢复挂载，后续数据变更继续通知 | [shell-kernel-boundary](../architecture/shell-kernel-boundary.zh.md) |
| `ShellEntry_SessionStateTransitions_ArePreserved` | 后台会话通过 `ISessionManager` 创建、Spawn、销毁；销毁时 BeforeQuit 触发且会话从 Keys 移除 | [session-model](../usage/session-model.zh.md) |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ShellEntry_FailFastValidation_IsPreservedAfterBootstrap` | Bootstrap 冻结后再次注册策略、非法 save id、null 实体元数据 | 分别抛 `InvalidOperationException` / `ArgumentException` / `ArgumentNullException`，不静默降级 |

## Hosting/OrigoHostTests 与 AdapterHostKernelPortTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `HostKernelPort_ShouldBeInternal_AndLiveInKernelPortsNamespace` | `HostKernelPort` 及端口契约保持 internal，且不进入导出面 | [Ports](../Origo.Core.Kernel/Ports/README.zh.md) |
| `OrigoHost_ShouldCreateStableRuntimeAndRunBackgroundWorkflow` | `OrigoHost` 构造稳定 runtime/context，并驱动帧、策略注册和会话工作流 | [Ports](../Origo.Core.Kernel/Ports/README.zh.md) |
| `AdapterHostKernelPort_ShouldBuildRuntimeAndBindSceneHost` | port 创建 runtime、默认 console 通道，先绑定 runtime/observer topology，再创建并绑定 SND context | [Ports](../Origo.Core.Kernel/Ports/README.zh.md) |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `AdapterHostKernelPort_ShouldRejectSceneHostWithoutRuntimeBinder` | scene host 未实现 `ISndSceneHostRuntimeBinder` | `InvalidOperationException`，不跳过 observer topology 绑定 |
| `AdapterHostKernelPort_ShouldRejectSceneHostWithoutContextBinder` | scene host 未实现 `ISndContextAttachableSceneHost` | `InvalidOperationException`，不跳过 context 绑定 |
| `AdapterHostKernelPort_ShouldRejectMissingFileSystem` | `OrigoHostOptions.FileSystem` 为空 | `InvalidOperationException`，不退回静默空文件系统 |

## 测试辅助策略

| 策略 | 作用 |
|------|------|
| `LifecycleProbeStrategy` | 经 AsyncLocal 收集 AfterSpawn/Process/AfterLoad/BeforeSave/BeforeQuit/BeforeDead 阶段事件，验证生命周期顺序 |
| `ObserverProbeStrategy` | 收集 OnMounted/OnDataChanged/OnUnmounted 事件，验证观察者绑定与恢复 |
| `LateProbeStrategy` | 在 Bootstrap 冻结后注册，用于验证 fail-fast 注册封印 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 尚未在独立 packaged consumer（`PackageReference`）中运行同一兼容场景 | 包恢复与启动路径由 issue #42 覆盖 | [shell-kernel-boundary](../architecture/shell-kernel-boundary.zh.md) |
| 尚未建立跨发布版本的 shell 包 × kernel 包二进制矩阵 | 0.1.0 只有 source/behavior 承诺，二进制矩阵在正式发布后才有输入 | [shell-kernel-boundary](../architecture/shell-kernel-boundary.zh.md) |
| Adapter Godot `Node` 入口的 headless 行为不在本套件重复覆盖 | 由 Godot 集成测试单独验证 | [Origo.GodotAdapter.Integration.Tests](../Origo.GodotAdapter.Integration.Tests/README.zh.md) |

## 设计决策

### 为什么通过 `OrigoHost` 而不是内部 harness 驱动

兼容承诺面向 shell 消费者。测试仅通过 `OrigoHost`、`ISndContext`、`ISessionRun`、`ISndWorldAccess` 等稳定入口验证行为；若 kernel 改动绕过 port 的编排路径，生命周期钩子、观察者恢复或状态转换会直接失败，而不是被测试替身掩盖。

### 为什么 port 测试覆盖缺失 binder/file system 的失败

`AdapterHostKernelPort` 是与 `HostKernelPort` 并列的 kernel-shell 构造入口；测试既验证成功路径的绑定顺序，也验证缺少 binder 或 file system 时显式失败，防止用静默 fallback 绕过 observer topology、context 绑定或输入校验。

### 为什么 golden 存档测试放在 Save-Storage

golden fixture 与格式版本语义属于持久化存储能力；本文件只链接该能力，避免同一测试在两个能力文档中重复维护。详见 [Save-Storage.md](Save-Storage.zh.md)。

---
[↑ 回到 Origo.Core.Tests](README.zh.md)
