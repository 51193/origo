# SND 元数据 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.md)

## 被测行为概览

验证 SND 元数据的核心类型：
- **TypedData**：只读 partial struct，通过 Source Generator 生成类型化工厂与 IEquatable 实现，值类型语义，内联存储
- **SndMetaData**：实体元数据的深拷贝，Node/Strategy/Data 三大模块全部正确复制
- **TypedDataPerformanceTests**：struct vs class 性能基准、零分配验证、SG 工厂 vs 隐式构造对比

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `TypedDataTests.cs` | TypedData 构造、类型保留、值/引用类型行为、struct 值语义 |
| `TypedDataGeneratedTests.cs` | Source Generator 输出验证：生成的工厂方法、IEquatable 实现、显式转换、多层 KindResolver 链、ObjectConverter fallback |
| `TypedDataPerformanceTests.cs` | 零分配基准测试：struct 内联存储与装箱消除、实体帧模拟、SG 工厂 Create vs 隐式转换性能对比 |
| `TypedDataDispatchPerformanceTests.cs` | 分发性能基准：Kind 检查 vs `is T` 模式匹配、工厂 TryExtract vs `is T`、ToObject switch vs Data 属性、混合类型分发 |
| `SndMetaDataTests.cs` | SndMetaData 默认值、DeepClone 深拷贝、修改不影响原对象 |

## TypedDataTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Constructor_StoresTypeAndValue` | 工厂/显式转换正确存储 Type 和 Data | snd-entity-model: TypedData |
| `NullValue_IsAllowed` | 工厂/显式转换允许 null 值 | — |
| `WithIntValue_PreservesExactType` | int 类型保留为 typeof(int) | snd-entity-model: TypedData |
| `WithFloatValue_PreservesExactType` | float 类型保留为 typeof(float) | snd-entity-model: TypedData |
| `WithDoubleValue_PreservesExactType` | double 类型保留 | snd-entity-model: TypedData |
| `WithBoolValue_PreservesExactType` | bool 类型保留 | snd-entity-model: TypedData |
| `WithStringValue_PreservesExactType` | string 类型保留 | snd-entity-model: TypedData |
| `WithStructValue_PreservesExactType` | Guid 类型保留 | snd-entity-model: TypedData |
| `WithDateTimeValue_PreservesExactType` | DateTime 类型保留 | snd-entity-model: TypedData |
| `WithBoxedInt_KeepsRuntimeType` | 装箱 int 保持 typeof(int) | snd-entity-model: TypedData |
| `WithArrayType_PreservesExactType` | int[] 类型保留 | snd-entity-model: TypedData |
| `WithReferenceType_PreservesIdentity` | 引用类型保持同一对象引用 | snd-entity-model: TypedData |
| `WithNullValueForReferenceType_PreservesTypeInfo` | null 值保留 List<int> 类型信息 | snd-entity-model: TypedData |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `TwoInstances_SameTypeAndSameValue_AreEqual` | 相同值的两个 TypedData 值相等（struct 值语义） | 值相等，无独立引用 |
| `TwoInstances_DifferentType_AreNotEqual` | 不同类型的两个 TypedData 值不等 | 类型不同的 struct 互不相等 |

## SndMetaDataTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `DefaultValues` | Name 为空串、NodeMetaData/StrategyMetaData 为 null、DataMetaData 非 null | snd-entity-model: 实体元数据 |
| `DeepClone_CopiesName` | DeepClone 复制 Name | SndMetaData |
| `DeepClone_CopiesNodeMetaData` | NodeMetaData 深复制（Pairs 独立） | SndMetaData |
| `DeepClone_CopiesStrategyMetaData` | StrategyMetaData 深复制（LifecycleIndices 独立） | SndMetaData |
| `DeepClone_CopiesDataMetaData` | DataMetaData 深复制（Pairs 独立，TypedData 值保留） | SndMetaData |
| `DeepClone_NullNodeMetaData_RemainsNull` | null NodeMetaData 克隆后仍为 null | SndMetaData |
| `DeepClone_ModifyCloneDoesNotAffectOriginal` | 修改克隆不影响原对象（Name + LifecycleIndices） | SndMetaData |
| `WithActiveStrategyIndices_DeepClones` | ActiveIndices 正确深复制 | SndMetaData |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `DeepClone_EmptyNodePairs_CopiesCorrectly` | 空 NodeMetaData.Pairs 复制后仍为空 | 不抛异常 |
| `DeepClone_EmptyDataPairs_CopiesCorrectly` | 空 DataMetaData.Pairs 复制后仍为空 | 不抛异常 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| TypedData IEquatable<TypedData> 的测试 | TypedData 通过 Source Generator 实现 IEquatable<TypedData>，需验证值相等语义 | TypedData |
| SndMetaData 非常大量策略索引时的性能 | 极端数据量 | — |

---

[↑ 回到 Origo.Core.Tests](README.md)
