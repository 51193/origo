# 启动编排 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)
> [↔ 被测模块: Origo.GodotAdapter/Bootstrap](../Origo.GodotAdapter/Bootstrap/README.md)

## 被测行为概览

验证 GodotAdapter 启动编排入口 `GodotSndBootstrap.BindRuntimeAndContext` 的契约：null 管理器守卫与四参数签名（manager/world/logger/context）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GodotSndBootstrapTests.cs` | `GodotSndBootstrap.BindRuntimeAndContext` 的守卫与参数契约 |

## GodotSndBootstrapTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `BindRuntimeAndContext_HasExpectedFourParameterContract` | `BindRuntimeAndContext` 恰有 4 个参数，依次命名为 manager/world/logger/context | Origo.GodotAdapter/Bootstrap |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `BindRuntimeAndContext_WithNullManager_Throws` | manager（及其余参数）为 null | ArgumentNullException |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本测试文件不定义辅助策略，纯反射/守卫契约测试 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 被测生产文件 `OrigoAutoHost.cs`/`OrigoDefaultEntry.cs` 和 `GodotSndEntity.cs` 等依赖 Godot 引擎运行时的文件被 coverlet 排除（`ExcludeByFile`），无法在测试中覆盖 | 启动编排的 Godot 引擎级逻辑未经测试直接验证 | Origo.GodotAdapter/启动编排文档 |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.md)
