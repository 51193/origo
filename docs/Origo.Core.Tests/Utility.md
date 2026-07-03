# Utility 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Utility](../Origo.Core/Utility/README.md)

## 验证能力

`DiffUtility.Diff<T>()` 和 `PathUtility` 静态路径操作的行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `Utility/DiffUtilityTests.cs` | Diff 集合差异比较的正确/错误/边界路径 |
| `Utility/PathUtilityTests.cs` | 路径拼接、遍历攻击检测、父目录提取、glob 后缀解析 |

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Diff_AddedItems_Detected` | 添加项正确检出 | `DiffUtility` public API |
| `Diff_RemovedItems_Detected` | 删除项正确检出 | `DiffUtility` public API |
| `Diff_AddedAndRemoved` | 同时有添加和删除的混合情况 | `DiffUtility` public API |
| `Diff_Duplicates_TreatedAsSingle` | 重复元素去重后参与比较 | `DiffUtility` public API |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Diff_EmptyBoth_ReturnsEmpty` | 两个集合均为空 | added、removed 均为空列表 |
| `Diff_EmptyOld_NewHasItems_ReturnsAdded` | 旧集合为空 | 全部新项计为 added |
| `Diff_EmptyNew_OldHasItems_ReturnsRemoved` | 新集合为空 | 全部旧项计为 removed |
| `Diff_NoChange_ReturnsEmpty` | 两集合内容相同 | added、removed 均为空列表 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Diff_NullOld_Throws` | oldItems 为 null | `ArgumentNullException` |
| `Diff_NullNew_Throws` | newItems 为 null | `ArgumentNullException` |

### PathUtility 正确路径

| 测试方法 | 验证的行为 |
|---------|-----------|
| `NormalizeDirectoryPath_StripsTrailingSlashes` | 尾部斜杠去除 |
| `ExtractGlobSuffix_ReturnsSuffix` | `"*.json"` → `".json"` |
| `ExtractGlobSuffix_ReturnsNull_WhenNoGlob` | 无通配符模式返回 null |
| `Combine_NullOrEmptyBase_ReturnsRelative` | 基础路径为空时返回相对路径 |
| `Combine_NullOrEmptyRelative_ReturnsBase` | 相对路径为空时返回基础路径 |
| `Combine_JoinsPaths` | 正常路径拼接（去冗余斜杠） |
| `GetParentDirectory_ReturnsParent` | 父目录提取 |
| `GetParentDirectory_NullOrEmpty_ReturnsEmpty` | null/空输入返回 string.Empty |
| `GetParentDirectory_SingleSegment_ReturnsEmpty` | 单段路径无父级返回 string.Empty |

### PathUtility 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Combine_RejectsPathTraversal` | `..` 路径遍历序列 | `ArgumentException` |
| `GetParentDirectory_AtRoot_Throws` | 根路径无父目录 | `InvalidOperationException` |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| Diff 对非 IEquatable<T> 的自定义引用类型的去重/比较 | 引用相等 vs 值相等语义 | DiffUtility |

---

[↑ 回到 Origo.Core.Tests](README.md)
