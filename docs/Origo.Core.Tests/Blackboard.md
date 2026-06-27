# 黑板 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Blackboard](../Origo.Core/Blackboard/README.md)
> [↔ 抽象: Origo.Core/Abstractions/Blackboard](../Origo.Core/Abstractions/Blackboard/README.md)

## 被测行为概览

验证 `IBlackboard` 接口的默认内存实现的全部行为：SetValue/Get 返回类型安全的元组、
键校验（null/空白键拒绝）、类型不匹配检测、Clear/GetKeys/SerializeAll/DeserializeAll 全生命周期。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `BlackboardTests.cs` | 黑板 CRUD + 序列化 + 键校验 + 类型安全 |

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Blackboard_SetValue_And_TryGet_Int` | SetValue(100) → TryGet<int> 返回 (true, 100) | Blackboard Abstraction |
| `Blackboard_SetValue_And_TryGet_String` | SetValue("player") → TryGet<string> 返回 (true, "player") | Blackboard Abstraction |
| `Blackboard_Clear_RemovesAll` | Clear 后 GetKeys 为空 | Blackboard Abstraction |
| `Blackboard_GetKeys_ReturnsAllKeys` | GetKeys 返回全部键名 | Blackboard Abstraction |
| `Blackboard_SerializeAll_And_DeserializeAll_RoundTrip` | 序列化后反序列化数据一致 | Blackboard Abstraction |
| `Blackboard_SetValue_OverwriteExisting` | 覆盖写入返回最新值 | Blackboard Abstraction |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Blackboard_TryGet_MissingKey_ReturnsFalse` | 不存在的键 | found=false |
| `Blackboard_TryGet_WrongType_ReturnsFalse` | int 键用 string 类型读 | found=false |
| `Blackboard_SetValue_ThrowsOnNullKey` | null 键 | ArgumentException |
| `Blackboard_TryGet_ThrowsOnNullKey` | null 键 | ArgumentException |
| `Blackboard_SetValue_ThrowsOnWhitespaceKey` | 空白键 | ArgumentException |

| `Blackboard_DeserializeAll_Null_Throws` | null 数据传入 | 抛出 Exception |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 大量键值（10k+）时的性能 | 黑板是大规模键值存储时 | — |

---

[↑ 回到 Origo.Core.Tests](README.md)
