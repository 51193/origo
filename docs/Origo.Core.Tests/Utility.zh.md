<!-- docsync-pair: Origo.Core.Tests/Utility -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Utility 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Utility](../Origo.Core/Utility/README.zh.md)

## 验证能力

`PathUtility` 静态路径操作与 `ValueInference` 字符串到类型值推断的行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `Utility/PathUtilityTests.cs` | 路径拼接、遍历攻击检测、父目录提取、glob 后缀解析 |
| `Utility/ValueInferenceTests.cs` | int → long → float → bool → string 推断顺序与类型精确性 |

## 测试详情

### PathUtility 正确路径

| 测试方法 | 验证的行为 |
|---------|-----------|
| `NormalizeDirectoryPath_StripsTrailingSlashes` | 尾部斜杠去除 |
| `ExtractGlobSuffix_ReturnsSuffix` | `"*.json"` → `".json"` |
| `ExtractGlobSuffix_ReturnsNull_WhenNoGlob` | 无通配符模式返回 null |
| `Combine_EmptyBase_ReturnsRelative` | 基础路径为空字符串时直接返回相对路径（透传） |
| `Combine_NullBase_Throws` | 基础路径为 null | ArgumentNullException（fail-fast） |
| `Combine_NullOrEmptyRelative_ReturnsBase` | 相对路径为空时返回基础路径 |
| `Combine_JoinsPaths` | 正常路径拼接（去冗余斜杠） |
| `GetParentDirectory_ReturnsParent` | 父目录提取 |
| `GetParentDirectory_NullOrEmpty_ReturnsEmpty` | null/空输入返回 string.Empty |
| `GetParentDirectory_SingleSegment_ReturnsEmpty` | 单段路径无父级返回 string.Empty |
| `NormalizeDirectoryPath_SchemePath_TrimsTrailingSlash` | `user://dir/` 尾部斜杠去除 |
| `NormalizeDirectoryPath_SchemeRoot_IsPreserved` | `user://`/`res://` scheme 根保留双斜杠 |
| `Combine_SchemeRootBase_KeepsDoubleSlash` | `user://` 根拼接子路径保留双斜杠 |
| `Combine_EmptyBase_RejectsTraversal` | 空基础路径 + 遍历序列（`../`、`..\\`） | ArgumentException（验证：空 base 分支同样应用遍历守卫） |
| `Combine_SchemeRootBase_RejectsTraversal` | scheme 根 + 遍历序列 | ArgumentException |
| `GetParentDirectory_SchemeFile_ReturnsSchemeRoot` | scheme 下文件返回其 scheme 根（`user://foo.map` → `user://`） |
| `GetParentDirectory_BackslashPath_ReturnsParent` | Windows 反斜杠路径父目录提取（`C:\base\sub` → `C:\base`） |

### PathUtility 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Combine_RejectsPathTraversal` | `..` 路径遍历序列 | `ArgumentException` |
| `GetParentDirectory_AtRoot_Throws` | 根路径无父目录 | `InvalidOperationException` |
| `GetParentDirectory_SchemeRoot_Throws` | `user://`/`res://` scheme 根无父目录 | `InvalidOperationException` |

### ValueInference 推断顺序

| 测试方法 | 验证的行为 |
|---------|-----------|
| `Infer_ReturnsFirstMatchingTypedValue` | 按 int → long → float → bool → string 顺序返回第一个可解析类型；`"42"`→int、`"3000000000"`→long、`"3.14"`→float、`"true"`→bool、其余→string（含空串与 `"12abc"` 原样返回） |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
