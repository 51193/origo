<!-- docsync-pair: Origo.GodotAdapter.Integration.Tests/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Origo.GodotAdapter.Integration.Tests

> [↑ 回到 Origo.manual](../README.zh.md)

## 概述

**Origo.GodotAdapter.Integration.Tests** 是在真实 Godot 运行时中执行的集成测试项目。
与单元测试项目 `Origo.GodotAdapter.Tests` 不同，本项目使用 `Godot.NET.Sdk` 并运行于
`godot --headless` 模式下，能够验证引擎依赖代码的实际行为。

## 测试运行器

集成测试使用自定义轻量运行器，而非 xUnit：

- **`IntegrationTestRunner`**：`[GlobalClass]` AutoLoad Node，在 `_Ready()` 中
  通过反射发现所有 `[IntegrationTest]` 标记的方法并执行。
  支持两种测试模式：
  - **即时测试**（`[IntegrationTest]`）：在 `_Ready()` 中立即执行，适用于不需要树操作的测试
  - **延迟测试**（`[DeferredTest]` + `IDeferredTestFixture`）：在 `_Ready()` 中排队，
    在后续 `_Process()` 帧中执行，适用于需要 `AddChild` 到 SceneTree 的测试。
    `Setup()` 方法在第一帧调用（添加节点到树），测试体在后续帧执行。
- **`[IntegrationTest]`**：自定义 attribute，标记即时测试方法
- **`[DeferredTest]`**：自定义 attribute，标记延迟测试方法
- **断言**：`IntegrationTestRunner.Assert(condition, message)` / `AssertEqual` /
  `AssertNotNull` / `AssertNull` / `AssertThrows<TException>` /
  `AssertContains` / `AssertEmpty` / `AssertNotEmpty`
- **输出**：测试结果以 `INTEGRATION_TEST_RESULTS:` 和 `INTEGRATION_TEST_SUMMARY:` 前缀
  输出到 stdout，便于 CI 解析

## 能力一览

| 测试类 | 文件 | 测试数 | 覆盖的引擎依赖 |
|--------|------|--------|---------------|
| GodotRuntimeSmokeTests | `Tests/GodotRuntimeSmokeTests.cs` | 5 | Godot 运行时冒烟（GD.Print、FileAccess/DirAccess 静态类、Vector2 类型、SceneTree） |
| GodotFileSystemIntegrationTests | `Tests/GodotFileSystemIntegrationTests.cs` | 5 | `GodotFileSystem`（`res://`/`user://` 读写、目录创建、文件枚举、删除） |
| GodotFileOperationsIntegrationTests | `Tests/GodotFileOperationsIntegrationTests.cs` | 7 | `GodotFileOperations`（ReadAllText/WriteAllText/Copy/Delete 守卫和正确性） |
| GodotDirectoryOperationsIntegrationTests | `Tests/GodotDirectoryOperationsIntegrationTests.cs` | 10 | `GodotDirectoryOperations`（Create/Exists/EnumerateFiles/Recursive/EnumerateDirectories/DeleteRecursive、隐藏文件枚举/删除） |
| GodotNodeHandleIntegrationTests | `Tests/GodotNodeHandleIntegrationTests.cs` | 8 | `GodotNodeHandle`（Name 缓存、Free、SetVisible for CanvasItem/Node3D、UnsafeGetNode） |
| GodotSndManagerInitializationTests | `Tests/GodotSndManagerInitializationTests.cs` | 6 | `GodotSndManager.BindRuntimeDependencies` / `BindContext`（null 守卫、正常链接绑定流程）；未绑定时的实体操作抛 NotReady 契约错误而非 NRE |
| GodotSndEntityIntegrationTests | `Tests/GodotSndEntityIntegrationTests.cs` | 9 | `GodotSndEntity`（构造 null 守卫、SetData/GetData/TryGetData、类型安全、释放后 fail-fast） |
| GodotSndManagerIntegrationTests | `Tests/GodotSndManagerIntegrationTests.cs` | 7 | `GodotSndManager`（BindRuntimeDeps 双重绑定守卫、BindContext 顺序守卫、null 守卫、ProcessAll 空列表） |
| GodotSndManagerCreationIntegrationTests | `Tests/GodotSndManagerCreationIntegrationTests.cs` | 5 | `GodotSndManager`（CreateEntity/RemoveEntity/BuildMetaList/RequestKillEntity/GetEntities） |
| GodotPackedSceneNodeFactoryIntegrationTests | `Tests/GodotPackedSceneNodeFactoryIntegrationTests.cs` | 4 | `GodotPackedSceneNodeFactory`（有效/无效场景加载、子节点添加、缓存复用） |
| OrigoAutoHostBootstrapIntegrationTests | `Tests/OrigoAutoHostBootstrapIntegrationTests.cs` | 2 | `OrigoAutoHost` 完整 `_Ready()` 启动（Runtime/SndManager/ConsoleChannels） |
| AdapterCommandHandlerIntegrationTests | `Tests/AdapterCommandHandlerIntegrationTests.cs` | 5 | `TreeDebugCommandHandler`、`PressButtonCommandHandler`、`CameraViewCommandHandler` |
| OrigoDefaultEntryBootstrapIntegrationTests | `Tests/OrigoDefaultEntryBootstrapIntegrationTests.cs` | 1 | `OrigoDefaultEntry` 属性完整默认值 |
| BootstrapIntegrationTests | `Tests/BootstrapIntegrationTests.cs` | 2 | `OrigoAutoHost` / `OrigoDefaultEntry` 属性默认值与实例化 |
| SndEntityNodeExtensionsIntegrationTests | `Tests/SndEntityNodeExtensionsIntegrationTests.cs` | 3 | `SndEntityNodeExtensions`（GetNativeNode/GetNodeFromSnd 类型守卫） |
| TypedDataInitializerIntegrationTests | `Tests/TypedDataInitializerIntegrationTests.cs` | 1 | `TypedDataInitializer`（EnsureLoaded 触发 adapter kind 注册） |
| ObserverSaveReloadIntegrationTests | `Tests/ObserverSaveReloadIntegrationTests.cs` | 3 | 观察者绑定跨存档/读档恢复 + 会话销毁触发 OnUnmounted |
| UserDataCleanupIntegrationTests | `Tests/UserDataCleanupIntegrationTests.cs` | 5 | 测试进程启动前 user:// 清理：残留写中标记/前缀产物清除、非测试内容与 Godot 系统内容保留、幂等 |
| GodotSndManagerExitTreeIntegrationTests | `Tests/GodotSndManagerExitTreeIntegrationTests.cs` | 2 | `GodotSndManager._ExitTree` 越界清理：直接移除管理器节点后 Core 侧策略池引用无泄漏 |

## 运行

### CI

```bash
bash scripts/godot-test.sh
```

该脚本自动：
1. 从 `Origo.GodotAdapter.csproj` 解析 `Godot.NET.Sdk` 版本
2. 下载匹配版本的 Godot mono 二进制（缓存于 `.godot_binary/`）
3. 运行 `godot --headless --path Origo.GodotAdapter.Integration.Tests`
4. 解析退出码并展示结果

### 本地

```bash
# 一键运行（自动下载 Godot 二进制）
bash scripts/godot-test.sh

# 手动模式（已有 Godot 安装）
godot --headless --path Origo.GodotAdapter.Integration.Tests
```

## 文件结构

```
Origo.GodotAdapter.Integration.Tests/
├── project.godot                          # Godot 4 工程配置
├── Origo.GodotAdapter.Integration.Tests.csproj
├── Runner/
│   ├── IntegrationTestRunner.cs           # AutoLoad 测试运行器
│   ├── IntegrationTestAttribute.cs        # [IntegrationTest] attribute
│   ├── DeferredTestAttribute.cs           # [DeferredTest] attribute（帧推进测试）
│   ├── IDeferredTestFixture.cs            # 延迟测试夹具接口
│   └── TestResult.cs                      # 结果 DTO
├── Tests/
│   ├── GodotRuntimeSmokeTests.cs          # 运行时冒烟测试
│   ├── GodotFileSystemIntegrationTests.cs # 文件系统集成测试
│   ├── GodotFileOperationsIntegrationTests.cs # 文件操作守卫测试
│   ├── GodotDirectoryOperationsIntegrationTests.cs # 目录操作测试
│   ├── GodotNodeHandleIntegrationTests.cs # Node 句柄测试
│   ├── GodotSndManagerInitializationTests.cs # BindRuntimeDependencies/BindContext 初始化测试
│   ├── GodotSndEntityIntegrationTests.cs # SND Entity 测试
│   ├── GodotSndManagerIntegrationTests.cs # SND Manager 测试
│   ├── GodotSndManagerCreationIntegrationTests.cs # Entity 创建/移除测试
│   ├── GodotPackedSceneNodeFactoryIntegrationTests.cs # PackedScene 加载测试
│   ├── OrigoAutoHostBootstrapIntegrationTests.cs # 完整启动测试
│   ├── AdapterCommandHandlerIntegrationTests.cs # 命令处理器测试
│   ├── SndEntityNodeExtensionsIntegrationTests.cs # 扩展方法测试
│   ├── TypedDataInitializerIntegrationTests.cs # 类型数据初始化测试
│   ├── BootstrapIntegrationTests.cs       # 引导默认值/实例化测试
│   ├── OrigoDefaultEntryBootstrapIntegrationTests.cs # 默认入口属性测试
│   ├── ObserverSaveReloadIntegrationTests.cs # 观察者绑定跨存档恢复测试
│   └── UserDataCleanupIntegrationTests.cs # 测试进程 user:// 清理测试
├── TestSupport/
│   ├── StubConsoleOutput.cs
│   ├── StubNodeFactory.cs
│   └── IntegrationTestHarness.cs
└── TestScenes/
    └── minimal.tscn                       # 最小根场景
```

## 版本管理

- **`Godot.NET.Sdk` NuGet 版本**：由 dependabot 自动跟踪（NuGet ecosystem）
- **Godot 引擎二进制**：CI 脚本从 `.csproj` 解析 SDK 版本后自动下载匹配二进制，
  无需额外版本跟踪文件

## 与单元测试的互补

| 维度 | 单元测试 | 集成测试 |
|------|---------|---------|
| 运行时 | 纯 .NET | Godot `--headless` |
| 速度 | 快（毫秒级） | 较慢（需启动 Godot 引擎） |
| 覆盖 | Core 逻辑、序列化、路径处理 | 真实文件 I/O、Node 生命周期、引擎 API |
| CI 角色 | 主阻塞 gate（含覆盖率门禁） | 补充阻塞 gate |

---

[↑ 回到 Origo.manual](../README.zh.md)
