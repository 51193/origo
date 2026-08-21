using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Origo.Core.DataSource;
using Origo.Core.DataSource.Converters;
using Origo.Core.Serialization;
using Origo.Core.Snd.Metadata;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public class DataSourceConverterTests
{
    // ── 11. ConverterRegistry ──

    [Fact]
    public void Registry_RegisterAndGet_RoundTrips()
    {
        var registry = new DataSourceConverterRegistry();
        registry.Register(new Int32DataSourceConverter());

        var converter = registry.Get<int>();
        var node = converter.Write(7);
        var value = converter.Read(node);

        Assert.Equal(7, value);
    }

    [Fact]
    public void Registry_ReadWrite_ByGenericType()
    {
        var registry = TestFactory.CreateRegistry();

        var node = registry.Write("hello");
        var value = registry.Read<string>(node);

        Assert.Equal("hello", value);
    }

    [Fact]
    public void Registry_ReadWrite_ByRuntimeType()
    {
        var registry = TestFactory.CreateRegistry();

#pragma warning disable CA2263 // Intentionally testing runtime-typed overload
        var node = registry.Write(typeof(int), 99);
        var value = registry.Read(typeof(int), node);
#pragma warning restore CA2263

        Assert.Equal(99, value);
    }

    [Fact]
    public void Registry_Get_ThrowsForUnregisteredType()
    {
        var registry = new DataSourceConverterRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Get<DateTime>());
    }

    [Fact]
    public void Registry_RuntimeRead_ThrowsForUnregisteredType()
    {
        var registry = new DataSourceConverterRegistry();

#pragma warning disable CA2263 // Intentionally testing runtime-typed overload
        Assert.Throws<InvalidOperationException>(() => registry.Read(typeof(DateTime), DataSourceNode.CreateNull()));
#pragma warning restore CA2263
    }

    [Fact]
    public void Registry_RuntimeWrite_NullReturnsNullNode()
    {
        var registry = new DataSourceConverterRegistry();

        var node = registry.Write(typeof(int), null);

        Assert.True(node.IsNull);
    }

    [Fact]
    public void Registry_GenericWrite_NullReturnsNullNodeLikeRuntimeOverload()
    {
        var registry = new DataSourceConverterRegistry();
        registry.Register(new StringDataSourceConverter());

        using var genericNull = registry.Write<string?>(null);
        using var runtimeNull = registry.Write(typeof(string), null);

        Assert.True(genericNull.IsNull);
        Assert.Equal(runtimeNull.ComputeSha256Hash(), genericNull.ComputeSha256Hash());
    }

    // ── 12. Primitive converters ──

    [Fact]
    public void PrimitiveConverters_RoundTrip_AllTypes()
    {
        var registry = TestFactory.CreateRegistry();

        Assert.Equal("text", registry.Read<string>(registry.Write("text")));
        Assert.Equal(42, registry.Read<int>(registry.Write(42)));
        Assert.Equal(9876543210L, registry.Read<long>(registry.Write(9876543210L)));
        Assert.Equal(1.5f, registry.Read<float>(registry.Write(1.5f)));
        Assert.Equal(2.718, registry.Read<double>(registry.Write(2.718)), 0.0001);
        Assert.True(registry.Read<bool>(registry.Write(true)));
        Assert.False(registry.Read<bool>(registry.Write(false)));
    }

    // ── 13. TypedData converter ──

    [Fact]
    public void TypedDataConverter_RoundTrip_IntValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = (TypedData)42;

        var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(int), result.DataType);
        Assert.Equal(42, TypedDataObjectConverter.ToObject(result));
    }

    [Fact]
    public void TypedDataConverter_RoundTrip_StringValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new TypedData(TypedData.KindMap.String, 0, "hello");

        var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(string), result.DataType);
        Assert.Equal("hello", TypedDataObjectConverter.ToObject(result));
    }

    [Fact]
    public void TypedDataConverter_RoundTrip_NullData()
    {
        var tm = new TypeStringMapping();
        tm.RegisterType<object>("Object");
        var registry = TestFactory.CreateRegistry(tm);

        var original = new TypedData(TypedData.KindMap.String, 0, null);

        var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(string), result.DataType);
        Assert.Null(TypedDataObjectConverter.ToObject(result));
    }

    [Fact]
    public void TypedDataConverter_NullDataForRegisteredValueType_Throws()
    {
        // A null 'data' value cannot be represented for registered value
        // kinds; silently coercing it to default would lose data, so the
        // reader rejects it (fail-fast).
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var node = DataSourceNode.CreateObject();
        node.Add("type", DataSourceNode.CreateString(BclTypeNames.Int32));
        node.Add("data", DataSourceNode.CreateNull());

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Read<TypedData>(node));
        Assert.Contains("value type", ex.Message);
    }

    [Fact]
    public void TypedDataConverter_NullDataForNullReferenceType_StillReturnsNullString()
    {
        // Reference kinds (string) represent null through the _ref slot and
        // must keep loading as found-but-null, not throw.
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var node = DataSourceNode.CreateObject();
        node.Add("type", DataSourceNode.CreateString(BclTypeNames.String));
        node.Add("data", DataSourceNode.CreateNull());

        var result = registry.Read<TypedData>(node);
        Assert.Equal(TypedData.KindMap.String, result._kind);
        Assert.Null(TypedDataObjectConverter.ToObject(result));
    }

    // ── 14. SndMetaData converter ──

    [Fact]
    public void SndMetaDataConverter_RoundTrip_FullStructure()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new SndMetaData
        {
            Name = "entity1",
            NodeMetaData = new NodeMetaData
            {
                Pairs = new Dictionary<string, string> { ["scene"] = "res://main.tscn" }
            },
            StrategyMetaData = new StrategyMetaData
            {
                LifecycleIndices = ["idle", "walk"]
            },
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData>
                {
                    ["hp"] = (TypedData)100,
                    ["name"] = new TypedData(TypedData.KindMap.String, 0, "hero")
                }
            }
        };

        var node = registry.Write(original);
        var result = registry.Read<SndMetaData>(node);

        Assert.Equal("entity1", result.Name);

        Assert.NotNull(result.NodeMetaData);
        Assert.Equal("res://main.tscn", result.NodeMetaData!.Pairs["scene"]);

        Assert.NotNull(result.StrategyMetaData);
        Assert.Equal(new[] { "idle", "walk" }, result.StrategyMetaData!.LifecycleIndices);

        Assert.NotNull(result.DataMetaData);
        Assert.Equal(typeof(int), result.DataMetaData!.Pairs["hp"].DataType);
        Assert.Equal(100, TypedDataObjectConverter.ToObject(result.DataMetaData.Pairs["hp"]));
        Assert.Equal("hero", TypedDataObjectConverter.ToObject(result.DataMetaData.Pairs["name"]));
    }

    [Fact]
    public void SndMetaDataConverter_RoundTrip_NullSubStructures()
    {
        var registry = TestFactory.CreateRegistry();

        var original = new SndMetaData
        {
            Name = "bare",
            NodeMetaData = null,
            StrategyMetaData = null,
            DataMetaData = null
        };

        var node = registry.Write(original);
        var result = registry.Read<SndMetaData>(node);

        Assert.Equal("bare", result.Name);
        Assert.Null(result.NodeMetaData);
        Assert.Null(result.StrategyMetaData);
        // DataMetaData defaults to new() in SndMetaData, so a null round-trip yields empty
        Assert.NotNull(result.DataMetaData);
        Assert.Empty(result.DataMetaData!.Pairs);
    }

    // ── 15. BlackboardData converter ──

    [Fact]
    public void BlackboardDataConverter_RoundTrip_MixedEntries()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new Dictionary<string, TypedData>
        {
            ["score"] = (TypedData)999,
            ["player"] = new TypedData(TypedData.KindMap.String, 0, "Alice"),
            ["alive"] = (TypedData)true
        } as IReadOnlyDictionary<string, TypedData>;

        var node = registry.Write(original);
        var result = registry.Read<IReadOnlyDictionary<string, TypedData>>(node);

        Assert.Equal(3, result.Count);
        Assert.Equal(999, TypedDataObjectConverter.ToObject(result["score"]));
        Assert.Equal("Alice", TypedDataObjectConverter.ToObject(result["player"]));
        Assert.Equal(true, TypedDataObjectConverter.ToObject(result["alive"]));
    }

    // ── 16. StateMachineContainerPayload converter ──

    [Fact]
    public void StateMachineContainerPayloadConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        var original = new StateMachineContainerPayload
        {
            Machines =
            [
                new()
                {
                    Key = "main",
                    PushIndex = "start",
                    PopIndex = "end",
                    Stack = ["stateA", "stateB"]
                },
                new()
                {
                    Key = "sub",
                    PushIndex = "init",
                    PopIndex = "",
                    Stack = ["only"]
                }
            ]
        };

        var node = registry.Write(original);
        var result = registry.Read<StateMachineContainerPayload>(node);

        Assert.Equal(2, result.Machines.Count);

        Assert.Equal("main", result.Machines[0].Key);
        Assert.Equal("start", result.Machines[0].PushIndex);
        Assert.Equal("end", result.Machines[0].PopIndex);
        Assert.Equal(new[] { "stateA", "stateB" }, result.Machines[0].Stack);

        Assert.Equal("sub", result.Machines[1].Key);
        Assert.Equal(new[] { "only" }, result.Machines[1].Stack);
    }

    [Fact]
    public void StateMachineContainerPayloadConverter_EntryMissingKey_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"machines":[{"pushIndex":"start","popIndex":"end","stack":[]}]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StateMachineContainerPayload>(node));
        Assert.Contains("key", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StateMachineContainerPayloadConverter_EntryMissingPushIndex_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"machines":[{"key":"main","popIndex":"end","stack":[]}]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StateMachineContainerPayload>(node));
        Assert.Contains("pushIndex", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StateMachineContainerPayloadConverter_EntryMissingPopIndex_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"machines":[{"key":"main","pushIndex":"start","stack":[]}]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StateMachineContainerPayload>(node));
        Assert.Contains("popIndex", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StateMachineContainerPayloadConverter_EntryNullOrNonStringKey_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"machines":[{"key":null,"pushIndex":"start","popIndex":"end","stack":[]}]}""");

        Assert.Throws<InvalidOperationException>(
            () => registry.Read<StateMachineContainerPayload>(node));
    }

    [Fact]
    public void StateMachineContainerPayloadConverter_EntryStackMissing_DefaultsToEmpty()
    {
        // 'stack' is written by the framework but a missing stack is a valid
        // empty stack; only the identity fields are mandatory.
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"machines":[{"key":"main","pushIndex":"start","popIndex":"end"}]}""");
        var result = registry.Read<StateMachineContainerPayload>(node);

        var entry = Assert.Single(result.Machines);
        Assert.Equal("main", entry.Key);
        Assert.Empty(entry.Stack);
    }

    // ── 17. StringDictionary converter ──

    [Fact]
    public void StateMachineContainerPayloadConverter_StackNullElement_Throws()
    {
        // A null stack element must not silently drift into an empty string:
        // corrupt save data surfaces here instead of later as an opaque
        // strategy lookup failure.
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"machines":[{"key":"main","pushIndex":"start","popIndex":"end","stack":[null]}]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StateMachineContainerPayload>(node));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StringDictionaryConverter_Read_NullValue_Throws()
    {
        // A null map value must not silently drift into an empty string
        // (consistent with Read<string> rejecting a null node).
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"lang":"en","region":null}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<IReadOnlyDictionary<string, string>>(node));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StringArrayConverter_Read_NullElement_Throws()
    {
        // A null array element must not silently drift into an empty string
        // (consistent with Read<string> rejecting a null node).
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""["hello",null,"world"]""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<string[]>(node));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArrayConverter_Read_NullNode_Throws()
    {
        // A missing array must not silently drift into an empty collection.
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("null");

        Assert.Throws<InvalidOperationException>(() => registry.Read<int[]>(node));
        Assert.Throws<InvalidOperationException>(() => registry.Read<string[]>(node));
    }

    [Fact]
    public void ArrayConverter_Read_ScalarNode_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson(""" "not-an-array" """);

        Assert.Throws<InvalidOperationException>(() => registry.Read<int[]>(node));
        Assert.Throws<InvalidOperationException>(() => registry.Read<string[]>(node));
    }

    [Fact]
    public void ArrayConverter_Read_ObjectNode_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"x":1}""");

        Assert.Throws<InvalidOperationException>(() => registry.Read<int[]>(node));
        Assert.Throws<InvalidOperationException>(() => registry.Read<string[]>(node));
    }

    [Fact]
    public void NodeMetaDataConverter_Read_NullPairValue_Throws()
    {
        // A null node-pair value must not silently drift into an empty
        // resource path (consistent with Read<string> rejecting null nodes).
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"pairs":{"root":"player.tscn","bad":null}}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<NodeMetaData>(node));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StrategyMetaDataConverter_Read_NullIndexElement_Throws()
    {
        // A null strategy-index element must not silently drift into an
        // empty index that only fails later as an opaque pool lookup error.
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"lifecycle_indices":["game.ai",null]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StrategyMetaData>(node));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Wrong node shapes must fail strict reads, not become empty ──────

    [Fact]
    public void StateMachineContainerPayloadConverter_StackNotArray_Throws()
    {
        // A corrupted stack object must not silently become an empty stack:
        // the machine state would be lost without any error.
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"machines":[{"key":"main","pushIndex":"start","popIndex":"end","stack":{}}]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StateMachineContainerPayload>(node));
        Assert.Contains("array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StrategyMetaDataConverter_LifecycleIndicesNotArray_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"lifecycle_indices":{}}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StrategyMetaData>(node));
        Assert.Contains("array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StrategyMetaDataConverter_ObserverIndicesNotArray_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"observer_indices":{}}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StrategyMetaData>(node));
        Assert.Contains("array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StrategyMetaDataConverter_BlankObserverTarget_Throws()
    {
        // A blank target would otherwise be silently dropped and the saved
        // observer binding would disappear during load.
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"observer_indices":[{"":["watch.health"]}]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<StrategyMetaData>(node));
        Assert.Contains("target", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NodeMetaDataConverter_PairsNotMap_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"pairs":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<NodeMetaData>(node));
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DataMetaDataConverter_PairsNotMap_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{"pairs":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<DataMetaData>(node));
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StringDictionaryConverter_Read_NonMap_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""[]""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<IReadOnlyDictionary<string, string>>(node));
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlackboardDataConverter_Read_NonMap_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""[]""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<IReadOnlyDictionary<string, TypedData>>(node));
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SndMetaDataConverter_Read_NonMap_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""[]""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<SndMetaData>(node));
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SndMetaDataListConverter_Read_NonArray_Throws()
    {
        var registry = TestFactory.CreateRegistry();
        var node = TestFactory.NodeFromJson("""{}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<IReadOnlyList<SndMetaData>>(node));
        Assert.Contains("array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StringDictionaryConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        var original = new Dictionary<string, string>
        {
            ["lang"] = "en",
            ["region"] = "US"
        } as IReadOnlyDictionary<string, string>;

        var node = registry.Write(original);
        var result = registry.Read<IReadOnlyDictionary<string, string>>(node);

        Assert.Equal(2, result.Count);
        Assert.Equal("en", result["lang"]);
        Assert.Equal("US", result["region"]);
    }

    [Fact]
    public void Read_String_FromNullNode_Throws()
    {
        // A Null node cannot be read as a string: doing so would silently
        // drift a null value into an empty string. Callers must check
        // IsNull / TryGetValue first (the pattern TypedDataConverter uses).
        var registry = TestFactory.CreateRegistry();

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<string>(DataSourceNode.CreateNull()));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeRead_String_FromNullNode_Throws()
    {
        var registry = TestFactory.CreateRegistry();

#pragma warning disable CA2263 // Intentionally testing runtime-typed overload
        Assert.Throws<InvalidOperationException>(
            () => registry.Read(typeof(string), DataSourceNode.CreateNull()));
#pragma warning restore CA2263
    }

    // ── Additional edge-case tests ──

    [Fact]
    public void JsonCodec_RoundTrip_EmptyObject()
    {
        var codec = TestFactory.CreateJsonCodec();

        var original = DataSourceNode.CreateObject();
        var json = codec.Encode(original);
        var decoded = codec.Decode(json);

        Assert.Equal(DataSourceNodeKind.Map, decoded.Kind);
        Assert.Empty(decoded.Keys);
    }

    [Fact]
    public void JsonCodec_RoundTrip_EmptyArray()
    {
        var codec = TestFactory.CreateJsonCodec();

        var original = DataSourceNode.CreateArray();
        var json = codec.Encode(original);
        var decoded = codec.Decode(json);

        Assert.Equal(DataSourceNodeKind.Array, decoded.Kind);
        Assert.Equal(0, decoded.Count);
    }

    [Fact]
    public void MapCodec_Encode_ThrowsForNonObjectNode()
    {
        var codec = TestFactory.CreateMapCodec();
        var arr = DataSourceNode.CreateArray();

        Assert.Throws<InvalidOperationException>(() => codec.Encode(arr));
    }

    [Fact]
    public void SndMetaDataConverter_JsonIntegration_FullRoundTrip()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);
        var codec = TestFactory.CreateJsonCodec();

        var original = new SndMetaData
        {
            Name = "npc",
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = ["patrol"] },
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData>
                {
                    ["speed"] = (TypedData)5.5
                }
            }
        };

        var node = registry.Write(original);
        var json = codec.Encode(node);
        var decodedNode = codec.Decode(json);
        var result = registry.Read<SndMetaData>(decodedNode);

        Assert.Equal("npc", result.Name);
        Assert.Equal(new[] { "patrol" }, result.StrategyMetaData!.LifecycleIndices);
        Assert.Equal(5.5, (double)TypedDataObjectConverter.ToObject(result.DataMetaData!.Pairs["speed"])!);
    }

    // ── 19. Extended primitive converter round-trips ──

    [Fact]
    public void ByteConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        using var n1 = registry.Write<byte>(0);
        Assert.Equal((byte)0, registry.Read<byte>(n1));

        using var n2 = registry.Write<byte>(255);
        Assert.Equal((byte)255, registry.Read<byte>(n2));

        using var n3 = registry.Write<byte>(128);
        Assert.Equal((byte)128, registry.Read<byte>(n3));
    }

    [Fact]
    public void SByteConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        using var n1 = registry.Write<sbyte>(-128);
        Assert.Equal((sbyte)-128, registry.Read<sbyte>(n1));

        using var n2 = registry.Write<sbyte>(0);
        Assert.Equal((sbyte)0, registry.Read<sbyte>(n2));

        using var n3 = registry.Write<sbyte>(127);
        Assert.Equal((sbyte)127, registry.Read<sbyte>(n3));
    }

    [Fact]
    public void Int16Converter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        using var n1 = registry.Write<short>(-32768);
        Assert.Equal((short)-32768, registry.Read<short>(n1));

        using var n2 = registry.Write<short>(0);
        Assert.Equal((short)0, registry.Read<short>(n2));

        using var n3 = registry.Write<short>(32767);
        Assert.Equal((short)32767, registry.Read<short>(n3));
    }

    [Fact]
    public void UInt16Converter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        using var n1 = registry.Write<ushort>(0);
        Assert.Equal((ushort)0, registry.Read<ushort>(n1));

        using var n2 = registry.Write<ushort>(65535);
        Assert.Equal((ushort)65535, registry.Read<ushort>(n2));
    }

    [Fact]
    public void UInt32Converter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        using var n1 = registry.Write(0u);
        Assert.Equal(0u, registry.Read<uint>(n1));

        using var n2 = registry.Write(4294967295u);
        Assert.Equal(4294967295u, registry.Read<uint>(n2));
    }

    [Fact]
    public void UInt64Converter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        using var n1 = registry.Write(0ul);
        Assert.Equal(0ul, registry.Read<ulong>(n1));

        using var n2 = registry.Write(18446744073709551615ul);
        Assert.Equal(18446744073709551615ul, registry.Read<ulong>(n2));
    }

    [Fact]
    public void DecimalConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        using var n1 = registry.Write(0m);
        Assert.Equal(0m, registry.Read<decimal>(n1));

        using var n2 = registry.Write(79228162514264337593543950335m);
        Assert.Equal(79228162514264337593543950335m, registry.Read<decimal>(n2));

        using var n3 = registry.Write(-3.14159m);
        Assert.Equal(-3.14159m, registry.Read<decimal>(n3));
    }

    [Fact]
    public void CharConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();

        using var n1 = registry.Write('A');
        Assert.Equal('A', registry.Read<char>(n1));

        using var n2 = registry.Write(' ');
        Assert.Equal(' ', registry.Read<char>(n2));

        using var n3 = registry.Write('\u4e2d');
        Assert.Equal('\u4e2d', registry.Read<char>(n3)); // Chinese character
    }

    // ── 20. Array converter round-trips ──

    [Fact]
    public void ByteArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new byte[] { 0, 1, 128, 255 };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<byte[]>(node));
    }

    [Fact]
    public void ByteArrayConverter_RoundTrip_Empty()
    {
        var registry = TestFactory.CreateRegistry();
        using var node = registry.Write(Array.Empty<byte>());
        Assert.Empty(registry.Read<byte[]>(node));
    }

    [Fact]
    public void SByteArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new sbyte[] { -128, -1, 0, 1, 127 };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<sbyte[]>(node));
    }

    [Fact]
    public void Int16ArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new short[] { short.MinValue, -1, 0, 1, short.MaxValue };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<short[]>(node));
    }

    [Fact]
    public void UInt16ArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new ushort[] { 0, 1, 32768, 65535 };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<ushort[]>(node));
    }

    [Fact]
    public void Int32ArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new[] { int.MinValue, -1, 0, 1, int.MaxValue };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<int[]>(node));
    }

    [Fact]
    public void UInt32ArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new uint[] { 0, 1, uint.MaxValue };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<uint[]>(node));
    }

    [Fact]
    public void Int64ArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new[] { long.MinValue, -1L, 0L, 1L, long.MaxValue };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<long[]>(node));
    }

    [Fact]
    public void UInt64ArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new ulong[] { 0, 1, ulong.MaxValue };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<ulong[]>(node));
    }

    [Fact]
    public void SingleArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new[] { 0f, -1.5f, 3.14f, float.MaxValue };
        using var node = registry.Write(original);
        var result = registry.Read<float[]>(node);
        Assert.Equal(original.Length, result.Length);
        for (var i = 0; i < original.Length; i++)
            Assert.Equal(original[i], result[i], 0.001f);
    }

    [Fact]
    public void DoubleArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new[] { 0.0, -1.5, 2.718281828, double.MaxValue };
        using var node = registry.Write(original);
        var result = registry.Read<double[]>(node);
        Assert.Equal(original.Length, result.Length);
        for (var i = 0; i < original.Length; i++)
            Assert.Equal(original[i], result[i], 0.000001);
    }

    [Fact]
    public void DecimalArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new[] { 0m, -3.14m, 99999.99999m };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<decimal[]>(node));
    }

    [Fact]
    public void BooleanArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new[] { true, false, true, true, false };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<bool[]>(node));
    }

    [Fact]
    public void CharArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new[] { 'A', 'B', ' ', '\u4e2d' };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<char[]>(node));
    }

    [Fact]
    public void StringArrayConverter_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var original = new[] { "hello", "world", "", "test" };
        using var node = registry.Write(original);
        Assert.Equal(original, registry.Read<string[]>(node));
    }

    [Fact]
    public void StringArrayConverter_RoundTrip_Empty()
    {
        var registry = TestFactory.CreateRegistry();
        using var node = registry.Write(Array.Empty<string>());
        Assert.Empty(registry.Read<string[]>(node));
    }

    // ── 21. Array converter JSON integration ──

    [Fact]
    public void IntArrayConverter_JsonIntegration_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var codec = TestFactory.CreateJsonCodec();

        var original = new[] { 1, 2, 3, 4, 5 };
        using var node = registry.Write(original);
        var json = codec.Encode(node);
        using var decoded = codec.Decode(json);
        var result = registry.Read<int[]>(decoded);

        Assert.Equal(original, result);
    }

    [Fact]
    public void ByteArrayConverter_JsonIntegration_RoundTrip()
    {
        var registry = TestFactory.CreateRegistry();
        var codec = TestFactory.CreateJsonCodec();

        var original = new byte[] { 0, 127, 255 };
        using var node = registry.Write(original);
        var json = codec.Encode(node);
        using var decoded = codec.Decode(json);
        var result = registry.Read<byte[]>(decoded);

        Assert.Equal(original, result);
    }

    // ── 22. TypedData with new types ──

    [Fact]
    public void TypedDataConverter_RoundTrip_ByteValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = (TypedData)(byte)42;
        using var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(byte), result.DataType);
        Assert.Equal((byte)42, TypedDataObjectConverter.ToObject(result));
    }

    [Fact]
    public void TypedDataConverter_RoundTrip_DecimalValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new TypedData(TypedData.UnregisteredKind, 0, 3.14159m);
        using var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(decimal), result.DataType);
        Assert.Equal(3.14159m, TypedDataObjectConverter.ToObject(result));
    }

    [Fact]
    public void TypedDataConverter_RoundTrip_CharValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = (TypedData)'X';
        using var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(char), result.DataType);
        Assert.Equal('X', TypedDataObjectConverter.ToObject(result));
    }

    [Fact]
    public void TypedDataConverter_RoundTrip_IntArrayValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new TypedData(TypedData.UnregisteredKind, 0, new[] { 1, 2, 3 });
        using var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(int[]), result.DataType);
        Assert.Equal(new[] { 1, 2, 3 }, (int[])TypedDataObjectConverter.ToObject(result)!);
    }

    [Fact]
    public void TypedDataConverter_RoundTrip_ByteArrayValue()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new TypedData(TypedData.UnregisteredKind, 0, new byte[] { 0, 128, 255 });
        using var node = registry.Write(original);
        var result = registry.Read<TypedData>(node);

        Assert.Equal(typeof(byte[]), result.DataType);
        Assert.Equal(new byte[] { 0, 128, 255 }, (byte[])TypedDataObjectConverter.ToObject(result)!);
    }
}
