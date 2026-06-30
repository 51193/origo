# DiffUtility 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Utility](../Origo.Core/Utility/README.md)

## 验证能力

`DiffUtility.Diff<T>()` 的行为：泛型集合差异比较（added / removed），基于 `HashSet<T>` 实现，自动去重。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `Utility/DiffUtilityTests.cs` | Diff 集合差异比较的正确/错误/边界路径 |

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

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| Diff 对非 IEquatable<T> 的自定义引用类型的去重/比较 | 引用相等 vs 值相等语义 | DiffUtility |

---

[↑ 回到 Origo.Core.Tests](README.md)
