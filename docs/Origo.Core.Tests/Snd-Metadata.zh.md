<!-- docsync-pair: Origo.Core.Tests/Snd-Metadata -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# SND 元数据 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Snd/Metadata](../Origo.Core/Snd/Metadata/README.zh.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.zh.md)

## 被测行为概览

验证 SND 元数据的核心类型：
- **TypedData**：只读 partial struct，通过 Source Generator 生成类型化工厂与 IEquatable 实现，值类型语义，内联存储
- **SndMetaData**：实体元数据的深拷贝，Node/Strategy/Data 三大模块全部正确复制
- **SndMetaFluentBuilder**：流式构建 SndMetaData 的便利 API
- **TypedData 集成**：TypedData 在实体 SetData/GetData、Blackboard 序列化、DataObserverManager 通知中的端到端行为

测试基础设施由 `TypedDataTestContext` 集合夹具在每次测试前重置 TypedData 静态状态。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `TypedDataTests.cs` | TypedData 构造、类型保留、值/引用类型行为、struct 值语义 |
| `TypedDataGeneratedTests.cs` | Source Generator 输出：隐式/显式转换、TryGet、IEquatable、GetHashCode、DataType、TypedDataFactory、多层 KindResolver、ObjectConverter fallback |
| `SndMetaDataTests.cs` | SndMetaData 默认值、DeepClone 深拷贝、修改不影响原对象 |
| `SndMetaFluentBuilderTests.cs` | SndMetaFluentBuilder 流式 API：名称、Node、Strategy、各类型数据设置、链式调用 |
| `TypedDataIntegrationTests.cs` | TypedData 在实体 CRUD、Blackboard 序列化往返、DataObserverManager 通知中的集成行为 |

> `TypedDataTestContext.cs` 是 xUnit 集合夹具，非测试文件。

## TypedDataTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Constructor_StoresTypeAndValue` | 类型和值正确存储 | snd-entity-model: TypedData |
| `NullValue_IsAllowed` | null 值存储保留类型信息 | snd-entity-model: TypedData |
| `WithIntValue_PreservesExactType` | int 类型保留为 typeof(int) | snd-entity-model: TypedData |
| `WithFloatValue_PreservesExactType` | float 类型保留为 typeof(float) | snd-entity-model: TypedData |
| `WithDoubleValue_PreservesExactType` | double 类型保留 | snd-entity-model: TypedData |
| `WithBoolValue_PreservesExactType` | bool 类型保留 | snd-entity-model: TypedData |
| `WithStringValue_PreservesExactType` | string 类型保留 | snd-entity-model: TypedData |
| `WithStructValue_PreservesExactType` | Guid struct 类型保留 | snd-entity-model: TypedData |
| `WithDateTimeValue_PreservesExactType` | DateTime 类型保留 | snd-entity-model: TypedData |
| `WithBoxedInt_KeepsRuntimeType` | 装箱 int 保持 typeof(int) | snd-entity-model: TypedData |
| `WithArrayType_PreservesExactType` | int[] 类型保留 | snd-entity-model: TypedData |
| `WithReferenceType_PreservesIdentity` | 引用类型保持同一对象引用 | snd-entity-model: TypedData |
| `WithNullValueForReferenceType_PreservesTypeInfo` | null 值保留类型信息 | snd-entity-model: TypedData |
| `RegisterKind_SameTypeTwice_IsIdempotent` | 同一 kind 重复注册相同类型为幂等操作 | snd-entity-model: TypedData |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `RegisterKind_DifferentTypeSameKind_Throws` | 同一 kind 注册不同类型 | InvalidOperationException（消息含 kind 与原类型名），原映射保留 |
| `RegisterKind_NullType_Throws` | RegisterKind 的 type 为 null | ArgumentNullException，不残留映射 |
| `RegisterKind_UnregisteredKindSentinel_Throws` | 使用 UnregisteredKind 哨兵注册 | ArgumentOutOfRangeException，不残留映射 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `RegisterKind_KindZero_IsIgnored` | kind=0 注册 | 被忽略，KindTypeMap[0] 保持 null |
| `TwoInstances_SameTypeAndSameValue_AreEqual` | 相同值的两个 TypedData（struct 值语义） | 值相等 |
| `TwoInstances_DifferentType_HaveDifferentReferences` | 不同类型的两个 TypedData | 值不相等 |

## TypedDataGeneratedTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ImplicitConversion_Int32_RoundTrip` | int 隐式转换往返 | snd-entity-model: TypedData |
| `ImplicitConversion_Single_RoundTrip` | float 隐式转换往返 | snd-entity-model: TypedData |
| `ImplicitConversion_Double_RoundTrip` | double 隐式转换往返 | snd-entity-model: TypedData |
| `ExplicitConversion_String_RoundTrip` | string 显式构造往返 | snd-entity-model: TypedData |
| `ImplicitConversion_Boolean_RoundTrip` | bool 隐式转换往返 | snd-entity-model: TypedData |
| `ImplicitConversion_Byte_RoundTrip` | byte 隐式转换往返 | snd-entity-model: TypedData |
| `ExplicitConversion_Int64_RoundTrip` | long 隐式转换往返 | snd-entity-model: TypedData |
| `ImplicitConversion_Char_RoundTrip` | char 隐式转换往返 | snd-entity-model: TypedData |
| `Equals_SameValueSameType_ReturnsTrue` | 相同类型相同值，Equals/== 返回 true | snd-entity-model: TypedData |
| `Equals_DifferentValueSameType_ReturnsFalse` | 相同类型不同值，Equals/== 返回 false | snd-entity-model: TypedData |
| `Equals_DifferentType_SameInlineBits_ReturnsFalse` | 不同类型相同 bits 值（Kind 不同），Equals 返回 false | snd-entity-model: TypedData |
| `Equals_BothNull_ReturnsTrue` | 两个 default(TypedData) 相等 | snd-entity-model: TypedData |
| `GetHashCode_SameValue_Consistent` | 相同值的 GetHashCode 一致 | snd-entity-model: TypedData |
| `DataType_ReturnsCorrectType` | 各注册类型的 DataType 返回正确 | snd-entity-model: TypedData |
| `DataType_Null_ReturnsObject` | default TypedData 的 DataType 为 typeof(object) | snd-entity-model: TypedData |
| `TypedDataFactory_Create_Int32_Correct` | 泛型工厂 Create<int> 正确 | snd-entity-model: TypedData |
| `TypedDataFactory_Create_String_Correct` | 泛型工厂 Create<string> 正确 | snd-entity-model: TypedData |
| `TypedDataFactory_Create_Float_Correct` | 泛型工厂 Create<float> 正确 | snd-entity-model: TypedData |
| `TypedDataFactory_TryExtract_Int32_Correct` | TryExtract 正确提取已注册类型 | snd-entity-model: TypedData |
| `TypedDataFactory_TryExtract_WrongType_ReturnsFalse` | TryExtract 类型不匹配返回 false | snd-entity-model: TypedData |
| `TypedDataFactory_TryExtract_FromDefault_ReturnsFalse` | TryExtract 从 default 返回 false | snd-entity-model: TypedData |
| `FromObject_RegisteredType_PreservesValue` | 隐式转换存储已注册类型 | snd-entity-model: TypedData |
| `FromObject_UnregisteredType_UsesRefSlot` | 未注册类型存储在 ref 槽 | snd-entity-model: TypedData |
| `FromObject_NullValue_PreservesType` | null 值保留类型信息 | snd-entity-model: TypedData |
| `AllRegisteredTypes_AreCovered` | 全部 13 个已注册类型均可构造非 null TypedData | snd-entity-model: TypedData |
| `TypedDataTypeMap_GetKindForType_Correct` | 已注册类型返回非零 Kind，未注册返回 0 | snd-entity-model: TypedData |
| `RegisterKind_Manual_CanBeRetrieved` | RegisterKind 手动注册后 KindTypeMap 可检索 | snd-entity-model: TypedData |
| `LayeredKindResolver_IsInvoked_ForUnknownType` | 多层 KindResolver 第一层匹配即返回 | snd-entity-model: TypedData |
| `ObjectConverterFallback_ToObject_IsInvoked` | ObjectConverter.ToObject 对注册类型正确工作 | snd-entity-model: TypedData |
| `ObjectConverterFallback_FromObject_IsInvoked` | ObjectConverter.FromObject 对注册类型返回 (0L, refValue) | snd-entity-model: TypedData |
| `TypedDataFactory_Create_Fallback_CallsObjectConverter` | TypedDataFactory.Create 通过 fallback 处理 TimeSpan | snd-entity-model: TypedData |
| `TypedDataFactory_TryExtract_Fallback_Works` | TypedDataFactory.TryExtract 通过 fallback 处理 DateTimeOffset | snd-entity-model: TypedData |
| `FromObject_Dispatch_Fallback` | FromObject 调度通过 fallback 处理 Uri 类型 | snd-entity-model: TypedData |
| `DataType_ForRegisteredKind_ReturnsCorrectType` | 注册 Kind 后 DataType 返回正确类型 | snd-entity-model: TypedData |
| `DataType_ForUnregisteredKind_FallsBackToRefType` | 未注册 Kind 通过 ref 对象的运行时类型推断 | snd-entity-model: TypedData |
| `Data_ForInlineType_NoBoxingAllocation` | 内联类型 ToObject 几乎零内存分配 | snd-entity-model: TypedData |
| `Data_ForRegisteredRefType_UsesRefField` | 注册的引用类型存储在 ref 字段 | snd-entity-model: TypedData |
| `MultiLayer_ResolverChain_FirstNonZeroWins` | 多层 Resolver 链中首个非零结果生效 | snd-entity-model: TypedData |
| `MultiLayer_FromObjectFallback_ChainIterates` | 多层 FromObject fallback 链正确迭代 | snd-entity-model: TypedData |
| `MultiLayer_ToObjectFallback_ChainIterates` | 多层 ToObject fallback 链正确迭代 | snd-entity-model: TypedData |
| `ObjectConverter_ToObject_UnregisteredKind_ReturnsRef` | 未注册 Kind 的 ToObject 返回 ref 对象 | snd-entity-model: TypedData |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `CrossTypeAccess_Int32AsSingle_ReturnsFalse` | int 用 TryGetSingle 访问 | 返回 false |
| `CrossTypeAccess_StringAsInt32_ReturnsFalse` | string 用 TryGetInt32 访问 | 返回 false |
| `NullSentinel_HasKindZero` | default(TypedData) IsNull=true，所有 TryGet 返回 false | Kind=0，所有 TryGet 返回 false |
| `NullSentinel_StillHasKindZero_AfterRegistrations` | 注册新 Kind 后 default 仍为 Kind=0 | 不受注册影响 |
| `TryExtract_StringKindWithNullValue_ReturnsFoundTrueAndNull` | string kind 的值为 null（引用 kind 合法状态） | TryExtract 返回 true 且值为 null，与 TryGetString 语义一致 |

## SndMetaDataTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SndMetaData_DefaultValues` | Name 为空串、NodeMetaData/StrategyMetaData 为 null、DataMetaData 非 null | snd-entity-model: 实体元数据 |
| `SndMetaData_DeepClone_CopiesName` | DeepClone 复制 Name | SndMetaData |
| `SndMetaData_DeepClone_CopiesNodeMetaData` | NodeMetaData 深复制（Pairs 独立） | SndMetaData |
| `SndMetaData_DeepClone_CopiesStrategyMetaData` | StrategyMetaData 深复制（LifecycleIndices 独立） | SndMetaData |
| `SndMetaData_DeepClone_CopiesDataMetaData` | DataMetaData 深复制（Pairs 独立，TypedData 值保留） | SndMetaData |
| `SndMetaData_DeepClone_NullNodeMetaData_RemainsNull` | null NodeMetaData 克隆后仍为 null | SndMetaData |
| `SndMetaData_DeepClone_ModifyCloneDoesNotAffectOriginal` | 修改克隆不影响原对象（Name + LifecycleIndices 独立） | SndMetaData |
| `SndMetaData_WithActiveStrategyIndices_DeepClones` | ActiveIndices 正确深复制 | SndMetaData |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `SndMetaData_DeepClone_EmptyNodePairs_CopiesCorrectly` | 空 NodeMetaData.Pairs 复制后仍为空 | 不抛异常 |
| `SndMetaData_DeepClone_EmptyDataPairs_CopiesCorrectly` | 空 DataMetaData.Pairs 复制后仍为空 | 不抛异常 |

## SndMetaFluentBuilderTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Build_WithName_SetsName` | Build 后 Name 为构造参数 | SndMetaFluentBuilder |
| `SetNode_AddsNodePair` | SetNode 添加 NodeMetaData 键值对 | SndMetaFluentBuilder |
| `AddLifecycleStrategy_StoresIndex` | AddLifecycleStrategy 多次调用累积 LifecycleIndices | SndMetaFluentBuilder |
| `AddActiveStrategy_StoresIndex` | AddActiveStrategy 添加到 ActiveIndices | SndMetaFluentBuilder |
| `SetInt_StoresCorrectTypedData` | SetInt 存储 TypedData with typeof(int) | SndMetaFluentBuilder |
| `SetFloat_StoresCorrectTypedData` | SetFloat 存储 TypedData with typeof(float) | SndMetaFluentBuilder |
| `SetString_StoresCorrectTypedData` | SetString 存储 TypedData with typeof(string) | SndMetaFluentBuilder |
| `SetBool_StoresCorrectTypedData` | SetBool 存储 TypedData with typeof(bool) | SndMetaFluentBuilder |
| `SetDouble_StoresCorrectTypedData` | SetDouble 存储 TypedData with typeof(double) | SndMetaFluentBuilder |
| `SetLong_StoresCorrectTypedData` | SetLong 存储 TypedData with typeof(long) | SndMetaFluentBuilder |
| `ChainedCalls_AllStored` | 链式调用正确存储 Node/Strategy/Data 全部配置 | SndMetaFluentBuilder |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `Build_EmptyName_Throws` | 空串或 null 名称 | ArgumentException / ArgumentNullException |

## TypedDataIntegrationTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Entity_SetData_GetData_RoundTrip_AllRegisteredTypes` | 实体 SetData/GetData 往返支持全部已注册类型 | snd-entity-model: TypedData |
| `Entity_TryGetData_Found_ReturnsTrue` | TryGetData 找到键返回 true 和值 | snd-entity-model: TypedData |
| `Entity_SetData_DifferentTypes_SameKey` | 同一键先后设置不同类型，后设置值覆盖前值 | snd-entity-model: TypedData |
| `Direct_Observer_Subscribe_And_Notify` | DataObserverManager 订阅和通知：oldValue/newValue 正确传递 | snd-entity-model: TypedData |
| `Blackboard_Set_TryGet_RoundTrip_AllTypes` | Blackboard 存储全部已注册类型并正确读回 | snd-entity-model: TypedData |
| `Blackboard_SerializeAll_DeserializeAll_RoundTrip` | Blackboard 序列化往返后数据完整保留 | snd-entity-model: TypedData |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Entity_TryGetData_WrongType_ReturnsFalse` | TryGetData 类型不匹配 | 全部返回 false |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `TypedDataTestContext` | TypedDataTestContext.cs | xUnit 集合夹具，在 [Collection("TypedData")] 内每次测试前调用 TypedData.ResetForTesting() 重置静态状态 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| SndMetaData 非常大量策略索引时的性能 | 极端数据量下的深拷贝性能 | — |
| TypedData 非注册引用类型在实体 SetData/GetData 中的往返 | 当前仅测试已注册类型的往返 | snd-entity-model: TypedData |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
