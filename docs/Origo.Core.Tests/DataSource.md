# 数据源 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/DataSource](../Origo.Core/DataSource/README.md)
> [↔ 被测行为: usage/architecture-overview](../usage/architecture-overview.md)

## 被测行为概览

验证 DataSourceNode 树模型：节点创建（Map/Array/Text/Number/Bool/Null）、
值访问（AsInt/AsLong/AsString 等）、懒展开（Lazy 延迟求值 + 失败可重试）、
JSON 编解码往返（嵌套对象/顶层数组、懒展开保护）、Map 编解码（注释/冒号值/空值跳过）、
ConverterRegistry（14 种基本类型 + 14 种数组类型 + 领域类型）完整往返、
TypedData 转换器、SndMetaData 转换器、IDisposable 迭代释放。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `DataSourceTests.cs` | 完整 DataSource 体系：工厂/访问/编解码/转换器/Dispose/Lazy |
| `DataSourceNodeSha256Tests.cs` | DataSourceNode SHA-256 摘要计算 |
| `KeyValueFileParserTests.cs` | Key/Value 文件解析器 |

## DataSourceTests 测试详情

DataSourceTests 是该能力最大的测试文件（1491 行）。

### 正确路径（代表性摘录）

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `CreateObject_ReturnsObjectNode` | CreateObject 返回 Object 节点 | DataSource |
| `CreateString_ReturnsStringNode` | CreateString("hello") → AsString()="hello" | DataSource |
| `CreateNumber_IntOverloads_ReturnNumberNode` | int/long/float/double/string → Number 节点 | DataSource |
| `CreateBoolean_ReturnsBooleanNode` | true/false → Boolean 节点 | DataSource |
| `ObjectNode_IndexerByKey_ReturnsChild` | obj["x"] 返回子节点 | DataSource |
| `ObjectNode_TryGetValue_ReturnsTrueForExistingKey` | TryGetValue 存在返回 true | DataSource |
| `ArrayNode_IndexerByIndex_ReturnsChild` | arr[0] 索引访问 | DataSource |
| `JsonCodec_RoundTrip_ComplexTree` | 复杂嵌套树 JSON 编解码往返 | DataSource |
| `JsonCodec_Decode_NestedObjectsAreLazy` | 解码后嵌套对象是懒展开 | DataSource |
| `JsonCodec_Decode_PrimitivesAreNotLazy` | 基本类型不解码为懒展开 | DataSource |
| `MapCodec_RoundTrip_FlatObject` | Map 编解码往返 | DataSource |
| `MapCodec_Decode_IgnoresCommentsAndEmptyLines` | Map 跳过 # 注释和空行 | DataSource |
| `MapCodec_Decode_HandlesColonsInValues` | Map 处理值中的冒号（URL） | DataSource |
| `MapCodec_Encode_SkipsNullValues` | Map 编码跳过 null 值 | DataSource |
| `Registry_RegisterAndGet_RoundTrips` | Converter 注册→获取→Write→Read 往返 | DataSource |
| `PrimitiveConverters_RoundTrip_AllTypes` | string/int/long/float/double/bool 往返 | DataSource |
| `TypedDataConverter_RoundTrip_IntValue` | TypedData<int> 往返，struct 语义下同一转换器无损序列化 | DataSource |
| `TypedDataConverter_RoundTrip_NullData` | TypedData<string, null> 往返，struct 内 null 数据自然支持 | DataSource |
| `SndMetaDataConverter_RoundTrip_FullStructure` | SndMetaData 全字段往返 | DataSource |
| `SndMetaDataConverter_RoundTrip_NullSubStructures` | null 子结构往返 | DataSource |
| `BlackboardDataConverter_RoundTrip_MixedEntries` | 黑板数据字典往返 | DataSource |
| `StateMachineContainerPayloadConverter_RoundTrip` | 状态机容器 Payload 往返 | DataSource |
| `CreateDefaultRegistry_RegistersAllExpectedTypes` | 默认注册全部 28+ 类型 | DataSource |
| `ConverterRegistry_TypeHierarchyFallback` | ReadOnlyDictionary→IReadOnlyDictionary 接口回退 | DataSource |
| `ReadOnlyDictionary_BlackboardRoundTrip_SurvivesSerialization` | ReadOnlyDictionary 经黑板序列化后恢复 | DataSource |
| `SndMetaDataConverter_JsonIntegration_FullRoundTrip` | SndMetaData → JSON → SndMetaData | DataSource |
| `CreateLazy_DoesNotCallExpanderUntilAccessed` | Lazy 访问前不调用 expander | DataSource |
| `CreateLazy_ExpandsOnlyOnce` | Lazy 只展开一次 | DataSource |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ObjectNode_IndexerByKey_ThrowsOnMissingKey` | obj["missing"] | KeyNotFoundException |
| `ObjectNode_TryGetValue_ReturnsFalseForMissingKey` | TryGetValue("nope") | 返回 false |
| `AsInt_OnNonNumericString_Throws` | CreateString("hello")→AsInt() | FormatException |
| `MapCodec_Encode_ThrowsForNonObjectNode` | Map 编码非 Object 节点 | InvalidOperationException |
| `Registry_Get_ThrowsForUnregisteredType` | 获取未注册类型转换器 | InvalidOperationException |
| `Registry_RuntimeRead_ThrowsForUnregisteredType` | 运行时读未注册类型 | InvalidOperationException |
| `LazyNode_WhenExpanderThrows_NodeStaysLazy` | 展开抛异常后保持 Lazy 状态 | 可重试 |
| `LazyNode_WhenExpanderThrows_NodeCanStillBeDisposed` | 展开失败后仍可 Dispose | 不抛异常 |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `AsString_OnNullNode_ReturnsEmpty` | Null 节点 AsString | "" |
| `JsonCodec_RoundTrip_EmptyObject` | 空对象编解码 | 空对象 |
| `JsonCodec_RoundTrip_EmptyArray` | 空数组编解码 | 空数组 |
| `MapCodec_Decode_LineWithoutColon_SkipsLine` | 无冒号行 | 跳过 |
| `MapCodec_Decode_EmptyValueAfterColon_ReturnsEmptyString` | 空值行 | 空字符串 |
| `MapCodec_Decode_OnlyCommentsAndEmptyLines_ReturnsEmptyObject` | 仅注释和空行 | 空对象 |
| `Registry_RuntimeWrite_NullReturnsNullNode` | null 值 Write | Null 节点 |
| `ByteConverter_RoundTrip` | byte 0/255/128 往返 | 边界值 |
| `SByteConverter_RoundTrip` | sbyte -128/0/127 往返 | 边界值 |
| `Int16Converter_RoundTrip` | short -32768/0/32767 往返 | 边界值 |
| `UInt32Converter_RoundTrip` | uint 0/4294967295 往返 | 边界值 |
| `UInt64Converter_RoundTrip` | ulong 0/18446744073709551615 往返 | 边界值 |
| `DecimalConverter_RoundTrip` | decimal 0/max/负数往返 | 边界值 |
| `CharConverter_RoundTrip` | char 'A'/空格/中文 往返 | 边界值 |
| `Dispose_PreventsSubsequentAccess` | Dispose 后访问抛异常 | ObjectDisposedException |
| `Dispose_RecursivelyDisposesChildren` | 父节点 Dispose 递归释放子节点 | — |
| `Dispose_CanBeCalledMultipleTimes` | 多次 Dispose 幂等 | — |
| `Dispose_LazyNodeReleasesExpander` | Dispose 后 accessor 释放 | — |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| DataSourceNode 超深嵌套（100+ 层）的性能 | 极端嵌套深度 | — |

---

[↑ 回到 Origo.Core.Tests](README.md)
