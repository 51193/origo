<!-- docsync-pair: Origo.GodotAdapter.Tests/FileSystem -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 文件系统 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
> [↔ 被测模块: Origo.GodotAdapter/FileSystem](../Origo.GodotAdapter/FileSystem/README.zh.md)

## 被测行为概览

验证 `GodotFileSystem` 对 `res://`（只读）和 `user://`（可写）虚拟路径前缀的处理：
路径拼接和父目录解析委托给 `Origo.Core.Utility.PathUtility`。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GodotFileSystemPathTests.cs` | res:// / user:// 路径拼接、父目录解析与边界输入 |

## GodotFileSystemPathTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GodotFileSystem_CombinePath_UsesHelperRules` | `GodotFileSystem.CombinePath` 委托 `PathUtility` 规则拼接 | GodotAdapter FileSystem |
| `GodotFileSystem_GetParentDirectory_UsesHelperRules` | `GodotFileSystem.GetParentDirectory` 委托 `PathUtility` 规则解析 | GodotAdapter FileSystem |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `GodotFileSystem_CombinePath_NullSecondArg_ReturnsFirst` | 第二参数为 null | 返回第一参数 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本测试文件不定义辅助策略；路径逻辑的正确性由 `Origo.Core.Tests/PathUtilityTests` 覆盖 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| `GodotFileSystem` 的实际 I/O 操作（`ReadAllText`/`WriteAllText`/`Exists`/`EnumerateFiles` 等）未覆盖（依赖 Godot 引擎 `FileAccess`/`DirAccess`，相关生产文件被 coverlet 排除） | 真实文件读写与目录枚举行为未在测试中直接验证 | Origo.GodotAdapter/FileSystem |
| `res://` 只读约束（对 `res://` 写入应被拒绝）的行为未覆盖 | 只读语义未验证 | Origo.GodotAdapter/FileSystem |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
