<!-- docsync-pair: Origo.Core.Tests/DataSource -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 数据源 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/DataSource](../Origo.Core/DataSource/README.zh.md)
> [↔ 被测行为: usage/architecture-overview](../usage/architecture-overview.zh.md)

## 被测行为概览

验证 DataSourceNode 树模型及其编解码、转换与摘要能力：节点工厂（Map/Array/Text/Number/Bool/Null）、强类型值访问器（AsString/AsInt/AsLong/AsFloat/AsDouble/AsByte/AsSByte/AsShort/AsUShort/AsUInt/AsULong/AsDecimal/AsChar）、对象/数组访问（索引器、TryGetValue、ContainsKey、Keys、Count、Elements）、Builder 链式 Add、懒展开（访问前不调用 expander、只展开一次、展开失败保持 Lazy 可重试）、JSON 编解码往返（复杂嵌套树/顶层数组/空对象/空数组、嵌套对象懒展开、基本类型非懒展开）、Map 编解码（注释/空行跳过、值中冒号、跳过 null 值、空值、无冒号行报错）、`DataSourceConverterRegistry`（注册/获取、泛型与运行时类型读写、未注册类型报错、null 写入、类型层级回退）、14 种基本类型 + 14 种数组类型 + 领域类型（TypedData/SndMetaData/BlackboardData/StateMachineContainerPayload/StringDictionary）的完整往返、`TypeStringMapping` 类型名注册、`IDisposable` 递归释放与深树防栈溢出、`ComputeSha256Hash` 规范化摘要、以及 `KeyValueFileParser` 的严格/宽松解析行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `DataSourceFactoryTests.cs` | 工厂方法和基本访问器：节点创建、值/对象/数组访问、Builder 链式 Add、Lazy 展开 |
| `DataSourceCodecTests.cs` | JSON 编解码往返（复杂嵌套树、顶层数组、懒展开）和 Map 编解码边缘情况（注释/空行跳过、值中冒号、无冒号行报错） |
| `DataSourceConverterTests.cs` | ConverterRegistry 注册与读写、14 种基本类型 + 14 种数组类型 + 5 种领域类型（TypedData/SndMetaData/BlackboardData/StateMachineContainerPayload/StringDictionary）完整往返、TypeStringMapping 扩展 |
| `DataSourceTests.cs` | 余项：IDisposable 递归释放与深树防栈溢出、新访问器方法、TypeStringMapping 新类型注册、DataSourceConverterRegistry 类型层级回退、ReadOnlyDictionary 往返 |
| `DataSourceNodeSha256Tests.cs` | DataSourceNode `ComputeSha256Hash` 规范化摘要计算 |
| `KeyValueFileParserTests.cs` | `KeyValueFileParser.Parse` 键值文件解析（严格/宽松模式、注释、重复键、null/空内容） |
| `CorruptObserverIndicesStrictReadTests.cs`（位于 Save/） | 回归：`observer_indices` 含非对象元素、**含多个目标键**或**空对象**（损坏存档）时 `LoadFromPayload` 抛 `InvalidOperationException`（含 "observer_indices"），而非静默丢弃绑定 |

## DataSourceTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateObject_ReturnsObjectNode` | CreateObject 返回 Map 节点 | DataSource |
| `CreateArray_ReturnsArrayNode` | CreateArray 返回 Array 节点，Count=0 | DataSource |
| `CreateString_ReturnsStringNode` | CreateString("hello") → Text 节点，AsString()="hello" | DataSource |
| `CreateNumber_IntOverloads_ReturnNumberNode` | int/long/float/double/string 重载均返回 Number 节点 | DataSource |
| `CreateBoolean_ReturnsBooleanNode` | true/false → Bool 节点，AsBool() 正确 | DataSource |
| `CreateNull_ReturnsNullNode` | CreateNull 返回 Null 节点，IsNull=true | DataSource |
| `AsString_OnStringNode_ReturnsValue` | 字符串节点 AsString 返回原值 | DataSource |
| `AsString_OnNumberNode_ReturnsStringRepresentation` | 数字节点 AsString 返回字符串表示 | DataSource |
| `AsInt_ParsesCorrectly` | 数字节点 AsInt 解析为 int | DataSource |
| `AsLong_ParsesCorrectly` | 数字节点 AsLong 解析为 long | DataSource |
| `AsFloat_ParsesCorrectly` | 数字节点 AsFloat 解析为 float | DataSource |
| `AsDouble_ParsesCorrectly` | 数字节点 AsDouble 解析为 double | DataSource |
| `ObjectNode_IndexerByKey_ReturnsChild` | obj["x"] 返回子节点 | DataSource |
| `ObjectNode_TryGetValue_ReturnsTrueForExistingKey` | TryGetValue 存在键返回 true 并输出子节点 | DataSource |
| `ObjectNode_ContainsKey_WorksCorrectly` | ContainsKey 对存在/缺失键分别返回 true/false | DataSource |
| `ObjectNode_Keys_ReturnsInsertionOrder` | Keys 按插入顺序返回 | DataSource |
| `ArrayNode_IndexerByIndex_ReturnsChild` | arr[0]/arr[1] 索引访问返回子节点 | DataSource |
| `ArrayNode_Count_ReflectsElements` | Count 反映元素数量 | DataSource |
| `ArrayNode_Elements_EnumeratesAll` | Elements 枚举全部元素 | DataSource |
| `ObjectNode_Add_ReturnsSameNodeForChaining` | 对象 Add 返回自身以支持链式调用 | DataSource |
| `ArrayNode_Add_ReturnsSameNodeForChaining` | 数组 Add 返回自身以支持链式调用 | DataSource |
| `CreateLazy_DoesNotCallExpanderUntilAccessed` | Lazy 节点在访问前不调用 expander | DataSource |
| `CreateLazy_ExpandsOnlyOnce` | Lazy 节点多次访问只展开一次 | DataSource |
| `JsonCodec_RoundTrip_ComplexTree` | 含字符串/数字/布尔/null/数组/嵌套对象的复杂树 JSON 往返 | DataSource |
| `JsonCodec_RoundTrip_TopLevelArray` | 顶层数组 JSON 往返，保持元素顺序 | DataSource |
| `JsonCodec_Decode_NestedObjectsAreLazy` | 解码后嵌套对象懒展开，访问内层键正确求值 | DataSource |
| `JsonCodec_Decode_PrimitivesAreNotLazy` | 解码后基本类型直接为对应 Kind，不为懒展开 | DataSource |
| `MapCodec_RoundTrip_FlatObject` | 扁平对象 Map 编解码往返 | DataSource |
| `MapCodec_Decode_IgnoresCommentsAndEmptyLines` | Map 解码跳过 `#` 注释和空行 | DataSource |
| `MapCodec_Decode_HandlesColonsInValues` | Map 解码保留值中冒号（如 URL） | DataSource |
| `MapCodec_Encode_SkipsNullValues` | Map 编码跳过 null 值键 | DataSource |
| `MapCodec_Encode_RejectsMultilineValue` | 值含换行符 | InvalidOperationException（编码产出自身严格解码无法读回的文件，须拒绝） |
| `MapCodec_DuplicateKey_WarningIsObservable` | 解码重复键 | 记 Warning 日志（不再经 NullLogger 静默丢弃） |
| `Registry_RegisterAndGet_RoundTrips` | Register→Get→Write→Read 往返 | DataSource |
| `Registry_ReadWrite_ByGenericType` | 按泛型类型 Write/Read 往返 | DataSource |
| `Registry_ReadWrite_ByRuntimeType` | 按运行时 Type Write/Read 往返 | DataSource |
| `PrimitiveConverters_RoundTrip_AllTypes` | string/int/long/float/double/bool 全类型往返 | DataSource |
| `TypedDataConverter_RoundTrip_IntValue` | TypedData(int) 往返保持 DataType=int | DataSource |
| `TypedDataConverter_RoundTrip_StringValue` | TypedData(string) 往返保持 DataType=string | DataSource |
| `TypedDataConverter_RoundTrip_NullData` | TypedData(string, null) 往返保持类型且数据为 null | DataSource |
| `TypedDataConverter_NullDataForNullReferenceType_StillReturnsNullString` | 引用类型（string）data 为 null 时按"存在但为 null"读回，不抛异常 | DataSource |
| `SndMetaDataConverter_RoundTrip_FullStructure` | SndMetaData 全字段（Node/Strategy/Data 子结构）往返 | DataSource |
| `SndMetaDataConverter_RoundTrip_NullSubStructures` | null 子结构往返，DataMetaData 回落为空字典 | DataSource |
| `BlackboardDataConverter_RoundTrip_MixedEntries` | 黑板字典（int/string/bool 混合）往返 | DataSource |
| `StateMachineContainerPayloadConverter_RoundTrip` | 状态机容器 Payload（多机器/栈）往返 | DataSource |
| `StringDictionaryConverter_RoundTrip` | IReadOnlyDictionary<string,string> 往返 | DataSource |
| `Read_String_FromNullNode_Throws` | 读取 Null 节点为 string | InvalidOperationException（null 静默漂移为空串须拒绝；调用方先 IsNull/TryGetValue 检查） |
| `RuntimeRead_String_FromNullNode_Throws` | 运行时类型重载读 Null 节点为 string | InvalidOperationException |
| `ObjectNode_Add_OnScalarNode_Throws` | 在 scalar 节点上按键 Add 子节点 | InvalidOperationException（子节点会被所有 codec 静默丢弃） |
| `ArrayNode_Add_OnScalarNode_Throws` | 在 scalar 节点上追加子节点 | InvalidOperationException |
| `FileMetaAccess_DirectoryExists_RejectsNullOrWhitespacePath` | DirectoryExists 空/空白路径 | ArgumentException（与 FileExists 对齐） |
| `CreateDefaultRegistry_RegistersAllExpectedTypes` | 默认注册全部基本/数组/领域类型转换器 | DataSource |
| `SndMetaDataConverter_JsonIntegration_FullRoundTrip` | SndMetaData → 节点 → JSON → 节点 → SndMetaData | DataSource |
| `ByteConverter_RoundTrip` | byte 0/255/128 往返 | DataSource |
| `SByteConverter_RoundTrip` | sbyte -128/0/127 往返 | DataSource |
| `Int16Converter_RoundTrip` | short -32768/0/32767 往返 | DataSource |
| `UInt16Converter_RoundTrip` | ushort 0/65535 往返 | DataSource |
| `UInt32Converter_RoundTrip` | uint 0/4294967295 往返 | DataSource |
| `UInt64Converter_RoundTrip` | ulong 0/18446744073709551615 往返 | DataSource |
| `DecimalConverter_RoundTrip` | decimal 0/max/负数往返 | DataSource |
| `CharConverter_RoundTrip` | char 'A'/空格/中文往返 | DataSource |
| `ByteArrayConverter_RoundTrip` | byte[] 往返 | DataSource |
| `SByteArrayConverter_RoundTrip` | sbyte[] 往返 | DataSource |
| `Int16ArrayConverter_RoundTrip` | short[] 往返 | DataSource |
| `UInt16ArrayConverter_RoundTrip` | ushort[] 往返 | DataSource |
| `Int32ArrayConverter_RoundTrip` | int[] 往返 | DataSource |
| `UInt32ArrayConverter_RoundTrip` | uint[] 往返 | DataSource |
| `Int64ArrayConverter_RoundTrip` | long[] 往返 | DataSource |
| `UInt64ArrayConverter_RoundTrip` | ulong[] 往返 | DataSource |
| `SingleArrayConverter_RoundTrip` | float[] 往返（含精度容差） | DataSource |
| `DoubleArrayConverter_RoundTrip` | double[] 往返（含精度容差） | DataSource |
| `DecimalArrayConverter_RoundTrip` | decimal[] 往返 | DataSource |
| `BooleanArrayConverter_RoundTrip` | bool[] 往返 | DataSource |
| `CharArrayConverter_RoundTrip` | char[]（含中文）往返 | DataSource |
| `StringArrayConverter_RoundTrip` | string[]（含空串）往返 | DataSource |
| `IntArrayConverter_JsonIntegration_RoundTrip` | int[] → 节点 → JSON → 节点 → int[] | DataSource |
| `ByteArrayConverter_JsonIntegration_RoundTrip` | byte[] → 节点 → JSON → 节点 → byte[] | DataSource |
| `TypedDataConverter_RoundTrip_ByteValue` | TypedData(byte) 往返保持 DataType=byte | DataSource |
| `TypedDataConverter_RoundTrip_DecimalValue` | TypedData(decimal) 往返保持 DataType=decimal | DataSource |
| `TypedDataConverter_RoundTrip_CharValue` | TypedData(char) 往返保持 DataType=char | DataSource |
| `TypedDataConverter_RoundTrip_IntArrayValue` | TypedData(int[]) 往返保持 DataType=int[] | DataSource |
| `TypedDataConverter_RoundTrip_ByteArrayValue` | TypedData(byte[]) 往返保持 DataType=byte[] | DataSource |
| `AsByte_ParsesCorrectly` | 数字节点 AsByte 解析 0/255 | DataSource |
| `AsSByte_ParsesCorrectly` | 数字节点 AsSByte 解析 -128/127 | DataSource |
| `AsShort_ParsesCorrectly` | 数字节点 AsShort 解析 -32768/32767 | DataSource |
| `AsUShort_ParsesCorrectly` | 数字节点 AsUShort 解析 0/65535 | DataSource |
| `AsUInt_ParsesCorrectly` | 数字节点 AsUInt 解析 0/4294967295 | DataSource |
| `AsULong_ParsesCorrectly` | 数字节点 AsULong 解析 0/18446744073709551615 | DataSource |
| `AsDecimal_ParsesCorrectly` | 数字节点 AsDecimal 解析 3.14159/-99.99 | DataSource |
| `AsChar_ParsesCorrectly` | 字符串节点 AsChar 解析 'A'/中文 | DataSource |
| `TypeStringMapping_RegistersAllNewTypes` | TypeStringMapping 注册全部基本类型与数组类型名 | DataSource |
| `TypeStringMapping_RegistersReadOnlyDictionary` | TypeStringMapping 注册 ReadOnlyDictionary 与接口类型名 | DataSource |
| `ConverterRegistry_TypeHierarchyFallback_FindsInterfaceConverterForConcreteType` | ReadOnlyDictionary 具体类型回退到 IReadOnlyDictionary 接口转换器 | DataSource |
| `ConverterRegistry_TypeHierarchyFallback_ExactTypeMatchStillWorks` | 精确类型匹配仍优先生效 | DataSource |
| `ReadOnlyDictionary_BlackboardRoundTrip_SurvivesSerialization` | ReadOnlyDictionary 经黑板序列化/反序列化后值恢复 | DataSource |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `AsInt_OnNonNumericString_Throws` | CreateString("hello") → AsInt() | FormatException |
| `ObjectNode_IndexerByKey_ThrowsOnMissingKey` | obj["missing"] | KeyNotFoundException |
| `Registry_Get_ThrowsForUnregisteredType` | Get<DateTime>() 未注册 | InvalidOperationException |
| `Registry_RuntimeRead_ThrowsForUnregisteredType` | Read(typeof(DateTime), …) 未注册 | InvalidOperationException |
| `TypedDataConverter_NullDataForRegisteredValueType_Throws` | 值类型（int）的 data 字段为 null | InvalidOperationException（消息含 "value type"） |
| `StateMachineContainerPayloadConverter_EntryMissingKey_Throws` | 机器条目缺 key 字段 | InvalidOperationException（消息含 "key"） |
| `StateMachineContainerPayloadConverter_EntryMissingPushIndex_Throws` | 机器条目缺 pushIndex 字段 | InvalidOperationException（消息含 "pushIndex"） |
| `StateMachineContainerPayloadConverter_EntryMissingPopIndex_Throws` | 机器条目缺 popIndex 字段 | InvalidOperationException（消息含 "popIndex"） |
| `StateMachineContainerPayloadConverter_EntryNullOrNonStringKey_Throws` | 机器条目 key 为 null | InvalidOperationException |
| `StateMachineContainerPayloadConverter_EntryNullOrNonStringKey_Throws` | 机器条目 key 为 null | InvalidOperationException |
| `StateMachineContainerPayloadConverter_StackNotArray_Throws` | 机器条目 stack 为对象而非数组 | InvalidOperationException（消息含 "array"；不得静默变为空栈） |
| `StrategyMetaDataConverter_LifecycleIndicesNotArray_Throws` | lifecycle_indices 为对象而非数组 | InvalidOperationException（消息含 "array"） |
| `StrategyMetaDataConverter_ObserverIndicesNotArray_Throws` | observer_indices 为对象而非数组 | InvalidOperationException（消息含 "array"） |
| `StrategyMetaDataConverter_BlankObserverTarget_Throws` | observer_indices 条目的 target 为空 | InvalidOperationException（消息含 "target"；不得静默丢弃绑定） |
| `NodeMetaDataConverter_PairsNotMap_Throws` | node.pairs 为数组而非对象 | InvalidOperationException（消息含 "object"） |
| `DataMetaDataConverter_PairsNotMap_Throws` | data.pairs 为数组而非对象 | InvalidOperationException（消息含 "object"） |
| `StringDictionaryConverter_Read_NonMap_Throws` | 字符串字典根节点为数组 | InvalidOperationException（消息含 "object"） |
| `BlackboardDataConverter_Read_NonMap_Throws` | 黑板字典根节点为数组 | InvalidOperationException（消息含 "object"） |
| `SndMetaDataConverter_Read_NonMap_Throws` | SndMetaData 根节点为数组 | InvalidOperationException（消息含 "object"） |
| `SndMetaDataListConverter_Read_NonArray_Throws` | SndMetaData 列表根节点为对象 | InvalidOperationException（消息含 "array"） |
| `MapCodec_Encode_ThrowsForNonObjectNode` | Map 编码 Array 节点 | InvalidOperationException |
| `LazyNode_WhenExpanderThrows_NodeStaysLazy_AndCanRetrySuccessfully` | 首次展开抛 InvalidOperationException | 首次访问抛异常后节点保持 Lazy，二次访问展开成功（callCount=2） |
| `LazyNode_WhenExpanderThrows_NodeCanStillBeDisposed` | 展开始终抛 InvalidOperationException | 展开失败后仍可 Dispose，后续访问抛 ObjectDisposedException |
| `MapCodec_Decode_LineWithoutColon_Throws` | 含无冒号行的 Map 文本 | FormatException |
| `Dispose_PreventsSubsequentAccess` | Dispose 后访问 Kind/AsString/IsNull | ObjectDisposedException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `AsString_OnNullNode_ReturnsEmpty` | Null 节点 AsString | 返回 string.Empty |
| `ObjectNode_TryGetValue_ReturnsFalseForMissingKey` | TryGetValue 缺失键 | 返回 false |
| `Registry_RuntimeWrite_NullReturnsNullNode` | Write(typeof(int), null) | 返回 Null 节点 |
| `Registry_GenericWrite_NullReturnsNullNodeLikeRuntimeOverload` | Write<string>(null) 与运行时类型重载一致 | 均返回 Null 节点且哈希一致 |
| `StateMachineContainerPayloadConverter_EntryStackMissing_DefaultsToEmpty` | 机器条目缺 stack 字段 | 视为空栈，正常读取（仅身份字段必需） |
| `JsonCodec_RoundTrip_EmptyObject` | 空对象 JSON 往返 | Map 节点，无键 |
| `JsonCodec_RoundTrip_EmptyArray` | 空数组 JSON 往返 | Array 节点，Count=0 |
| `MapCodec_Decode_EmptyValueAfterColon_ReturnsEmptyString` | `emptyval:` 空值行 | 键存在，值为空字符串 |
| `MapCodec_Decode_OnlyCommentsAndEmptyLines_ReturnsEmptyObject` | 仅含注释与空行 | Map 节点，无键 |
| `ByteArrayConverter_RoundTrip_Empty` | 空 byte[] 往返 | 返回空数组 |
| `StringArrayConverter_RoundTrip_Empty` | 空 string[] 往返 | 返回空数组 |
| `Dispose_RecursivelyDisposesChildren` | 父对象 Dispose | 递归释放子节点，访问子节点抛 ObjectDisposedException |
| `Dispose_RecursivelyDisposesArrayChildren` | 父数组 Dispose | 递归释放数组子节点 |
| `Dispose_CanBeCalledMultipleTimes` | 多次 Dispose | 幂等不抛异常，后续访问抛 ObjectDisposedException |
| `Dispose_LazyNodeReleasesExpander` | 未展开的 Lazy 节点 Dispose | expander 不被调用，后续访问抛 ObjectDisposedException |
| `UsingStatement_DisposesAfterScope` | using 作用域退出 | 作用域外访问抛 ObjectDisposedException |
| `Dispose_DeeplyNestedTree_DoesNotStackOverflow` | 2000 层嵌套树 Dispose | 不发生栈溢出 |
| `ComputeSha256Hash_DeeplyNestedTree_DoesNotStackOverflow` | 2000 层嵌套树 ComputeSha256Hash | 不发生栈溢出，返回非空哈希 |

## DataSourceNodeSha256Tests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ScalarString_HashIsDeterministic` | 相同字符串节点哈希一致 | DataSource |
| `ScalarNumber_HashIsDeterministic` | 相同数字节点哈希一致 | DataSource |
| `ScalarBoolean_HashIsDeterministic` | 相同布尔节点哈希一致 | DataSource |
| `NullNode_HashIsDeterministic` | Null 节点哈希一致 | DataSource |
| `ObjectNode_HashDependsOnKeys` | 键名不同则哈希不同 | DataSource |
| `ObjectNode_HashEscapesSpecialCharactersInKeys` | 键含特殊字符（`=`/`,`/`{}`/`[]`/引号/反斜杠）时哈希确定，与值中同字符不碰撞 | DataSource |
| `ObjectNode_HashIndependentOfInsertionOrder` | 对象哈希与键插入顺序无关 | DataSource |
| `ArrayNode_HashOrderDependent` | 数组哈希依赖元素顺序 | DataSource |
| `DeepNested_HashWorks` | 深层嵌套（对象含数组含字符串）计算哈希不抛异常且非空 | DataSource |
| `DifferentValues_DifferentHashes` | 不同字符串值哈希不同 | DataSource |
| `EmptyObjectVsEmptyArray_DifferentHashes` | 空对象与空数组哈希不同 | DataSource |
| `StringWithSpecialChars_HashWorks` | 含引号/反斜杠字符串哈希确定且可重现 | DataSource |
| `StringWithSpecialChars_DoesNotCollideWithUnescapedEquivalent` | 转义前后不同值哈希不碰撞 | DataSource |
| `BooleanTrueAndFalse_HaveDifferentHashes` | true 与 false 哈希不同 | DataSource |
| `NumberIntegerVsFloatWithSameValue_HaveDifferentHashes` | 整数 1 与 float 1.0 规范化后哈希相同（值等价） | DataSource |
| `HashIsHexString` | 哈希为 64 位小写十六进制字符串 | DataSource |
| `SameComplexTree_DifferentInstances_SameHash` | 相同结构的不同实例哈希相同 | DataSource |
| `DecodedDeeplyNestedTree_AllLevelsExpanded_DepthThreeHashDiffers` | 解码树展开到第 3 层后叶值变化，哈希不同（回归：懒子树被纳入哈希） | DataSource |
| `DecodedNestedTree_ArrayDeepChange_ProducesDifferentHash` | 解码树中数组内深层对象值变化，哈希不同 | DataSource |
| `DecodedNestedTree_DeepKeyChange_ProducesDifferentHash` | 解码树深层键名变化，哈希不同 | DataSource |
| `DecodedNestedTree_DeepValueChange_ProducesDifferentHash` | 解码树深层叶值变化，哈希不同 | DataSource |
| `DecodedNestedTree_SameContent_ProducesSameHash` | 相同内容的解码树哈希一致 | DataSource |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `DisposedNode_ComputeSha256Hash_Throws` | 对已 Dispose 节点调用 ComputeSha256Hash | ObjectDisposedException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `EmptyString_HashWorks` | 空字符串节点 | 哈希确定且可重现 |

## KeyValueFileParserTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `KeyValueFileParser_Parse_BasicKeyValue` | 基础 `key: value` 多行解析为字典 | DataSource |
| `KeyValueFileParser_Parse_SkipsCommentsAndBlanks` | 跳过 `#` 注释和空行，保留有效键值 | DataSource |
| `KeyValueFileParser_Parse_ValueContainsColon_PreservesFullValue` | 值中冒号（如 URL）完整保留 | DataSource |

## CorruptObserverIndicesStrictReadTests 测试详情

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `LoadFromPayload_WhenObserverIndicesEntryIsNotAnObject_Throws` | `observer_indices` 数组含非对象元素（损坏存档；写入侧只会产生对象元素） | `LoadFromPayload` 抛 `InvalidOperationException`（消息含 "observer_indices"），不静默丢弃损坏绑定 |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `KeyValueFileParser_Parse_StrictMode_ThrowsOnInvalidLine` | 严格模式遇无冒号行 | FormatException |
| `KeyValueFileParser_Parse_StrictMode_ThrowsOnEmptyKeyOrValue` | 严格模式遇空键（`: value`） | FormatException |
| `KeyValueFileParser_Parse_ThrowsOnNullLogger` | logger 参数为 null | ArgumentNullException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `KeyValueFileParser_Parse_EmptyContent_ReturnsEmpty` | 空字符串内容 | 返回空字典 |
| `KeyValueFileParser_Parse_NullContent_ReturnsEmpty` | null 内容 | 返回空字典 |
| `KeyValueFileParser_Parse_LenientMode_LogsWarningOnInvalidLine` | 宽松模式遇无冒号行 | 不抛异常，结果为空且记录警告 |
| `KeyValueFileParser_Parse_LenientMode_LogsWarningOnEmptyKey` | 宽松模式遇空键 | 不抛异常，结果为空且记录警告 |
| `KeyValueFileParser_Parse_DuplicateKey_LogsWarning` | 重复键 | 后值覆盖前值并记录警告 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本能力的三个测试文件均不定义 `XxxStrategy` 辅助策略类。`DataSourceTests` 通过共享 `TestFactory`（`CreateJsonCodec`/`CreateMapCodec`/`CreateRegistry`）构造编解码器与转换器注册表；`KeyValueFileParserTests` 通过共享 `TestLogger` 捕获警告。两者均为测试项目级共享基础设施，非本能力私有的策略类。 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| Lazy 节点的 `ComputeSha256Hash` 行为 | 未验证对未展开 Lazy 节点求哈希是否触发展开及结果稳定性 | DataSource |
| Map 编解码的嵌套结构 | 仅覆盖扁平对象，嵌套对象/数组在 Map 格式下的行为未覆盖 | DataSource |
| `KeyValueFileParser` 严格模式下的重复键行为 | 仅在宽松模式验证重复键覆盖+警告，严格模式分支未覆盖 | DataSource |
| 转换器与节点的并发读写线程安全性 | 多线程场景未覆盖 | DataSource |
| DataSourceNode 超深嵌套（远超 2000 层）的性能特征 | 极端嵌套深度的性能未量化 | DataSource |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
