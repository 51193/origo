<!-- docsync-pair: Origo.Core.Tests/TypeStringMapping -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 类型序列化 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Serialization](../Origo.Core/Serialization/README.zh.md)

## 被测行为概览

验证 TypeStringMapping 的 CLR 类型 ↔ 稳定字符串标识双向映射：
全部 BCL 基本类型和数组类型预注册、自定义类型注册后双向查询、
冲突检测（同名冲突/同类型冲突）、null/空白键校验。
另覆盖 SndMappings 场景别名/模板加载与解析、JSON 与 TypedData/SndMetaData 的编解码集成。

`SchedulingAndTypeMappingTests.cs` 同时承载 ActionScheduler 与 TypeStringMapping 两种能力的测试：本文档仅记录其 TypeStringMapping 相关方法（`TypeStringMapping_HasDefaultTypes_AndSupportsCustomRegistration`）；其 ActionScheduler 方法记录于 [Runtime-Core.md](Runtime-Core.zh.md)，本文档不重复收录。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `TypeStringMappingTests.cs` | 基础冲突检测 |
| `TypeStringMappingExtendedTests.cs` | BCL 预注册验证、自定义注册往返、ReadOnlyDictionary 预注册、冲突检测、null/空白键校验 |
| `JsonAndMappingsTests.cs` | JSON 与类型映射集成：SndMetaData/TypedData 往返、SndMappings 场景别名与模板解析、Blackboard SerializeAll、JSON 数组根解码 |
| `SchedulingAndTypeMappingTests.cs` | TypeStringMapping 默认类型与自定义注册（ActionScheduler 方法见 [Runtime-Core.md](Runtime-Core.zh.md)） |

## TypeStringMappingTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `RegisterType_ThrowsOnConflictingMapping` | DateTime→"MyDateTime" 后再以 long→"MyDateTime" 或 DateTime→"OtherName" 注册 | InvalidOperationException |

## TypeStringMappingExtendedTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `TypeStringMapping_RegisterType_DuplicateSameType_NoThrow` | 重复注册同一映射不抛异常 | Serialization |
| `TypeStringMapping_BclTypes_AllPreregistered` | Int32/String/Boolean/Single/Double/Int64/Int16/Byte/ArrayString 等可获取 | Serialization |
| `TypeStringMapping_RegisterCustomType_RoundTrips` | 注册 Guid → 双向查询正确 | Serialization |
| `TypeStringMapping_ReadOnlyDictionaryTypes_Preregistered` | ReadOnlyDictionary / IReadOnlyDictionary 类型双向预注册 | Serialization |
| `TypeStringMapping_RegisterManyCustomTypes_AllResolvable` | 连续注册 DateTime/Uri/Version/TimeSpan → 全部可双向查询 | Serialization |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `TypeStringMapping_RegisterType_ConflictingNameToType_Throws` | "Int32" 已映射到 int，再映射到 long | InvalidOperationException |
| `TypeStringMapping_RegisterType_ConflictingTypeToName_Throws` | int 已映射到 "Int32"，再映射到 "MyInt" | InvalidOperationException |
| `TypeStringMapping_RegisterType_WhitespaceName_Throws` | "" 或 "  " 名称 | ArgumentException |
| `TypeStringMapping_GetTypeByName_UnregisteredType_Throws` | 未注册名称 | InvalidOperationException |
| `TypeStringMapping_GetNameByType_UnregisteredType_Throws` | 未注册类型 | InvalidOperationException |
| `TypeStringMapping_RegisterType_NullName_Throws` | null 名称 | ArgumentNullException |
| `TypeStringMapping_GetTypeByName_NullName_Throws` | null 名称 | ArgumentNullException |
| `TypeStringMapping_GetNameByType_NullType_Throws` | null 类型 | ArgumentNullException |

## JsonAndMappingsTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndMetaData_RoundTripPreservesTypedData` | SndMetaData 经 registry.Write → JSON 编码 → 解码 → Read 往返保持名称/节点/策略/TypedData | Serialization |
| `SndMappings_LoadSceneAliases_DuplicateKey_LastWins` | 场景别名 map 中重复 key 时后者覆盖前者 | Serialization |
| `SndMappings_LoadSceneAliasesAndTemplates_ResolveExpectedValues` | 加载场景别名与模板后正确解析，模板解析后缓存不再重复读文件 | Serialization |
| `SndMappings_ResolveMetaListFromJsonArray_SupportsTemplateAndInlineMix` | JSON 数组混合 templateKey 引用与内联定义解析为 meta 列表 | Serialization |
| `Blackboard_SerializeAll_ReturnsDetachedCopy` | SerializeAll 返回脱离副本，修改副本不影响黑板原值 | Serialization |
| `JsonCodec_DecodeJsonArrayRoot_ReadsElements` | 顶层 JSON 数组解码为 Array 节点并按索引读取元素 | Serialization |
| `SndWorld_ResolveTemplate_MutationDoesNotPolluteTemplateCache` | 修改 ResolveTemplate 返回的副本不污染模板缓存，再次解析仍为原始内容 | Serialization |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SndMappings_ResolveTemplate_BeforeLoadTemplates_Throws` | 未 LoadTemplates 即 ResolveTemplate | InvalidOperationException |
| `SndMappings_ResolveTemplate_AfterLoadTemplatesWithEmptyMap_Throws` | 加载空模板 map 后 ResolveTemplate | InvalidOperationException（含 "empty"） |
| `SndMappings_ResolveTemplate_InvalidJson_Throws` | 模板指向的 JSON 文件语法无效 | 抛出异常 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TypedDataJson_DataPropertyBeforeType_DeserializesCorrectly` | JSON 中 data 字段排在 type 字段之前 | 仍正确反序列化为 TypedData<int> |

## SchedulingAndTypeMappingTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `TypeStringMapping_HasDefaultTypes_AndSupportsCustomRegistration` | 默认类型（Int32、ArrayString）可获取，注册 Guid 后可双向查询 | Serialization |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本能力的测试不定义辅助策略，纯映射/序列化行为测试 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 泛型类型的名称稳定性（如 `List<int>` vs `List<string>`） | 泛型类型的标识符策略 | Serialization |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
