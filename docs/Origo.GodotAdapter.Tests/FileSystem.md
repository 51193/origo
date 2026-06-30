# 文件系统 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)
> [↔ 被测模块: Origo.GodotAdapter/FileSystem](../Origo.GodotAdapter/FileSystem/README.md)

## 被测行为概览

验证 `GodotPathResolver` 与 `GodotFileSystem` 对 `res://`（只读）和 `user://`（可写）虚拟路径前缀的处理：
路径拼接、父目录解析、路径遍历保护（禁止 `..` 逃逸），以及 null/空字符串等边界输入。
`GodotFileSystem.CombinePath`/`GetParentDirectory` 委托给 `GodotPathResolver` 的同一套规则。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GodotFileSystemPathTests.cs` | res:// / user:// 路径拼接、父目录解析、遍历保护与边界输入 |

## GodotFileSystemPathTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `GodotPathResolver_Combine_JoinsPaths` | `Combine("user://origo_saves", "current/system.json")` → `user://origo_saves/current/system.json` | GodotAdapter FileSystem |
| `GodotPathResolver_GetParentDirectory_HandlesTrailingSlash` | `GetParentDirectory("res://origo/maps/")` → `res://origo`（容忍尾部斜杠） | GodotAdapter FileSystem |
| `GodotFileSystem_CombinePath_UsesHelperRules` | `GodotFileSystem.CombinePath` 委托 `GodotPathResolver` 规则拼接 | GodotAdapter FileSystem |
| `GodotFileSystem_GetParentDirectory_UsesHelperRules` | `GodotFileSystem.GetParentDirectory` 委托 `GodotPathResolver` 规则解析 | GodotAdapter FileSystem |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `GodotPathResolver_Combine_WithTrailingDotDotNoSlash_Throws` | 相对路径以 `foo/..` 结尾 | ArgumentException |
| `GodotPathResolver_Combine_WithTraversal_Throws` | 含遍历段（`../escape`、`foo/../bar`、`foo\..\bar`） | ArgumentException（[Theory] 覆盖三种输入） |
| `GodotPathResolver_GetParentDirectory_RootPath_ThrowsInvalidOperation` | 对根路径（`/`、`res://`、`user://`）取父目录 | InvalidOperationException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `GodotPathResolver_Combine_NullBasePath_ReturnsRelativePath` | basePath 为 null | 返回 relativePath |
| `GodotPathResolver_Combine_EmptyBasePath_ReturnsRelativePath` | basePath 为空字符串 | 返回 relativePath |
| `GodotPathResolver_Combine_NullRelativePath_ReturnsBasePath` | relativePath 为 null | 返回 basePath |
| `GodotPathResolver_Combine_EmptyRelativePath_ReturnsBasePath` | relativePath 为空字符串 | 返回 basePath |
| `GodotPathResolver_Combine_BothEmpty_ReturnsEmpty` | 两者均为空字符串 | 返回空字符串 |
| `GodotPathResolver_Combine_BothNull_ReturnsNull` | 两者均为 null | 返回 null |
| `GodotPathResolver_GetParentDirectory_EmptyString_ReturnsEmpty` | 空字符串路径 | 返回空字符串 |
| `GodotPathResolver_GetParentDirectory_Null_ReturnsEmpty` | null 路径 | 返回空字符串 |
| `GodotPathResolver_GetParentDirectory_NoSlash_ReturnsEmpty` | 无斜杠的扁平路径 | 返回空字符串 |
| `GodotFileSystem_CombinePath_NullSecondArg_ReturnsFirst` | 第二参数为 null | 返回第一参数 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本测试文件不定义辅助策略；遍历用例数据由 `TheoryData<string>` 静态成员 `GodotPathResolver_Combine_WithTraversal_Data` 提供 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| `GodotFileSystem` 的实际 I/O 操作（`ReadAllText`/`WriteAllText`/`Exists`/`EnumerateFiles` 等）未覆盖（依赖 Godot 引擎 `FileAccess`/`DirAccess`，相关生产文件被 coverlet 排除） | 真实文件读写与目录枚举行为未在测试中直接验证 | Origo.GodotAdapter/FileSystem |
| `res://` 只读约束（对 `res://` 写入应被拒绝）的行为未覆盖 | 只读语义未验证 | Origo.GodotAdapter/FileSystem |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.md)
