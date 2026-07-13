# 控制台 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)
> [↔ 被测模块: Origo.GodotAdapter/Console](../Origo.GodotAdapter/Console/README.md)
> [↔ 被测行为: usage/console-commands](../usage/console-commands.md)

## 被测行为概览

验证 Godot 适配层扩展的控制台命令与基类：`press_button`（按实体名与节点路径查找 Godot Button 并发射 Pressed 信号）、
`camera_view`（打印实体在当前相机下的屏幕坐标与深度）、适配层 `CommandHandlerBase` 的参数数量校验、null 守卫与执行编排，
以及 `camera_view` 依赖的 `ProjectionHelper` 世界坐标→屏幕坐标投影数学。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `CommandHandlerBaseTests.cs` | 适配层 CommandHandlerBase 基类：构造守卫、null 守卫、参数数量上下界校验、执行成功 |
| `PressButtonCommandHandlerTests.cs` | press_button 命令：属性契约、参数不足、实体未找到、实体非 Godot 实体 |
| `CameraViewCommandHandlerTests.cs` | camera_view 命令：属性契约（Name/HelpText/参数界） |
| `ProjectionHelperTests.cs` | ProjectionHelper.ProjectWorldToScreen：中心/四边界/相机后方/视锥外/深度递增/对称性/相机非原点 |

## CommandHandlerBaseTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `TryExecute_ExactArgs_Succeeds` | 参数数量恰好满足时执行成功，输出 "ok"，error 为 null | console-commands |
| `TryExecute_UnlimitedMax_AcceptsManyArgs` | MaxPositionalArgs=-1（无上限）时接受任意多参数并成功 | console-commands |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Constructor_NullRuntime_Throws` | 构造时 runtime 为 null | ArgumentNullException |
| `TryExecute_NullInvocation_Throws` | invocation 为 null | ArgumentNullException |
| `TryExecute_NullOutputChannel_Throws` | outputChannel 为 null | ArgumentNullException |
| `TryExecute_TooFewArgs_ReturnsErrorWithHelpText` | 参数数量少于 MinPositionalArgs | 返回 false，error 含 "参数数量不合法" 与 HelpText |
| `TryExecute_TooManyArgs_ReturnsErrorWithHelpText` | 参数数量超过 MaxPositionalArgs | 返回 false，error 含 "参数数量不合法" |

## PressButtonCommandHandlerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Properties_HaveExpectedValues` | Name="press_button"，HelpText 含 `<entity>`/`<path>`，Min/Max 均为 2 | console-commands |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `TryExecute_TooFewArgs_ReturnsError` | 仅提供 1 个参数（需 2 个） | 返回 false，error 含 "参数数量不合法" |
| `TryExecute_EntityNotFound_ReturnsError` | 实体名不存在 | 返回 false，error 含 "Entity 'NonExistent' not found" |
| `TryExecute_EntityNotGodot_ReturnsError` | 实体存在但非 Godot 实体 | 返回 false，error 含 "is not a Godot entity" |

## CameraViewCommandHandlerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Properties_HaveExpectedValues` | Name="camera_view"，HelpText 含 "屏幕坐标"/"深度"，Min/Max 均为 0 | console-commands |

## ProjectionHelperTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ProjectWorldToScreen_Center_ReturnsScreenCenter` | 相机正前方点投影到屏幕中心 (400,300)，深度为距离 | console-commands: camera_view |
| `ProjectWorldToScreen_RightEdge_ReturnsRightBoundary` | 90° FOV 下右侧点投影到右边界 X=800 | console-commands |
| `ProjectWorldToScreen_LeftEdge_ReturnsLeftBoundary` | 左侧点投影到左边界 X=0 | console-commands |
| `ProjectWorldToScreen_TopEdge_ReturnsTopBoundary` | 上方点投影到上边界 Y=0 | console-commands |
| `ProjectWorldToScreen_BottomEdge_ReturnsBottomBoundary` | 下方点投影到下边界 Y=600 | console-commands |
| `ProjectWorldToScreen_DepthIncreasesWithDistance` | 更远的点返回更大的深度值 | console-commands |
| `ProjectWorldToScreen_SymmetricPositions_HaveSymmetricScreenX` | 关于中心对称的点具有对称的屏幕 X 偏移 | console-commands |
| `ProjectWorldToScreen_CameraNotAtOrigin_ProjectsCorrectly` | 相机不在原点时仍正确投影 | console-commands |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `ProjectWorldToScreen_BehindCamera_ReturnsNull` | 点位于相机后方 | 返回 null |
| `ProjectWorldToScreen_OutsideFrustum_ReturnsNull` | 点在视锥体外 | 返回 null |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `TestHandler` | `CommandHandlerBaseTests.cs` | 派生 `CommandHandlerBase` 的测试桩，可配置 Min/Max 参数界，`ExecuteCore` 输出 "ok"，用于驱动基类参数校验逻辑 |

> 共享辅助 `TestRuntimeHelper`/`TestSndSceneHost`/`InMemorySndEntity` 见 [TestSupport.md](TestSupport.md)。

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| `press_button` 在真实 Godot 实体上成功发射 Pressed 信号的正确路径未覆盖（依赖 Godot 引擎运行时，相关生产文件被 coverlet 排除） | 命令的成功路径未在测试中直接验证 | Origo.GodotAdapter/Console |
| `camera_view` 命令在真实相机/实体上的执行正确路径未覆盖（依赖 Godot 引擎运行时，命令处理器文件被 coverlet 排除；仅 ProjectionHelper 投影数学被单元测试覆盖） | 命令的成功路径未在测试中直接验证 | Origo.GodotAdapter/Console |
| `CommandHandlerBase` 的命名参数（NamedArgs）解析与校验未覆盖 | 命名参数路径未验证 | console-commands |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.md)
