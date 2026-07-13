# 框架元信息 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core](../Origo.Core/README.md)

## 被测行为概览

验证 `OrigoMeta` 记录的行为：框架身份信息（名称/版本/横幅）的构造、`ToString` 呈现、
默认横幅常量以及基于值的相等语义。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `OrigoMetaTests.cs` | OrigoMeta 默认横幅、ToString 内容、值相等/不等 |

## OrigoMetaTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `DefaultBanner_IsNonEmpty` | `OrigoMeta.DefaultBanner` 非空字符串 | Origo.Core |
| `ToString_ContainsNameAndVersion` | `ToString()` 包含名称与版本 | Origo.Core |
| `EqualOperator_SameValues_ReturnsTrue` | 相同字段的两个 OrigoMeta 相等，`==` 为 true | Origo.Core |
| `EqualOperator_DifferentValues_ReturnsFalse` | 版本不同的两个 OrigoMeta 不相等，`==` 为 false | Origo.Core |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 空/空白名称或版本构造 OrigoMeta 的行为未覆盖 | 边界输入未验证 | Origo.Core |

---

[↑ 回到 Origo.Core.Tests](README.md)
