# 类型序列化 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Serialization](../Origo.Core/Serialization/README.md)

## 被测行为概览

验证 TypeStringMapping 的 CLR 类型 ↔ 稳定字符串标识双向映射：
全部 BCL 基本类型和数组类型预注册、自定义类型注册后双向查询、
冲突检测（同名冲突/同类型冲突）、null/空白键校验。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `TypeStringMappingTests.cs` | 基础冲突检测 |
| `TypeStringMappingExtendedTests.cs` | BCL 预注册验证、自定义注册往返、null 键校验 |
| `JsonAndMappingsTests.cs` | JSON 与类型映射集成 |

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RegisterType_DuplicateSameType_NoThrow` | 重复注册同一映射不抛异常 | Serialization |
| `RegisterCustomType_RoundTrips` | 注册 Guid → 双向查询正确 | Serialization |
| `BclTypes_AllPreregistered` | Int32/String/Boolean/Single/Double 等可获取 | Serialization |
| `RegisterManyCustomTypes_AllResolvable` | 连续注册 DateTime/Uri/Version/TimeSpan → 全部可双向查询 | Serialization |
| `ReadOnlyDictionaryTypes_Preregistered` | ReadOnlyDictionary 类型预注册 | Serialization |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `RegisterType_ConflictingNameToType_Throws` | "Int32" 已映射到 int，再映射到 long | InvalidOperationException |
| `RegisterType_ConflictingTypeToName_Throws` | int 已映射到 "Int32"，再映射到 "MyInt" | InvalidOperationException |
| `RegisterType_WhitespaceName_Throws` | "" 或 "  " 名称 | ArgumentException |
| `GetTypeByName_UnregisteredType_Throws` | 未注册名称 | InvalidOperationException |
| `GetNameByType_UnregisteredType_Throws` | 未注册类型 | InvalidOperationException |
| `RegisterType_NullName_Throws` | null 名称 | ArgumentNullException |
| `GetTypeByName_NullName_Throws` | null 名称 | ArgumentNullException |
| `GetNameByType_NullType_Throws` | null 类型 | ArgumentNullException |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 泛型类型的名称稳定性（如 `List<int>` vs `List<string>`） | 泛型类型的标识符策略 | Serialization |

---

[↑ 回到 Origo.Core.Tests](README.md)
