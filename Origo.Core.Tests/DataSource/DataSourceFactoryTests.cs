using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Origo.Core.DataSource;
using Origo.Core.DataSource.Converters;
using Origo.Core.Snd.Metadata;
using Origo.Core.StateMachine;
using Xunit;

namespace Origo.Core.Tests;

public class DataSourceFactoryTests
{
    // ── 1. Factory methods ──

    [Fact]
    public void CreateObject_ReturnsObjectNode()
    {
        var node = DataSourceNode.CreateObject();

        Assert.Equal(DataSourceNodeKind.Map, node.Kind);
    }

    [Fact]
    public void CreateArray_ReturnsArrayNode()
    {
        var node = DataSourceNode.CreateArray();

        Assert.Equal(DataSourceNodeKind.Array, node.Kind);
        Assert.Equal(0, node.Count);
    }

    [Fact]
    public void CreateString_ReturnsStringNode()
    {
        var node = DataSourceNode.CreateString("hello");

        Assert.Equal(DataSourceNodeKind.Text, node.Kind);
        Assert.Equal("hello", node.AsString());
    }

    [Fact]
    public void CreateNumber_IntOverloads_ReturnNumberNode()
    {
        var fromInt = DataSourceNode.CreateNumber(42);
        var fromLong = DataSourceNode.CreateNumber(123456789L);
        var fromFloat = DataSourceNode.CreateNumber(3.14f);
        var fromDouble = DataSourceNode.CreateNumber(2.718281828);
        var fromString = DataSourceNode.CreateNumber("99");

        Assert.All([fromInt, fromLong, fromFloat, fromDouble, fromString],
            n => Assert.Equal(DataSourceNodeKind.Number, n.Kind));
    }

    [Fact]
    public void CreateBoolean_ReturnsBooleanNode()
    {
        var t = DataSourceNode.CreateBoolean(true);
        var f = DataSourceNode.CreateBoolean(false);

        Assert.Equal(DataSourceNodeKind.Bool, t.Kind);
        Assert.True(t.As<bool>());
        Assert.Equal(DataSourceNodeKind.Bool, f.Kind);
        Assert.False(f.As<bool>());
    }

    [Fact]
    public void CreateNull_ReturnsNullNode()
    {
        var node = DataSourceNode.CreateNull();

        Assert.Equal(DataSourceNodeKind.Null, node.Kind);
        Assert.True(node.IsNull);
    }

    // ── 2. Value access ──

    [Fact]
    public void AsString_OnStringNode_ReturnsValue() =>
        Assert.Equal("abc", DataSourceNode.CreateString("abc").AsString());

    [Fact]
    public void AsString_OnNumberNode_ReturnsStringRepresentation() =>
        Assert.Equal("42", DataSourceNode.CreateNumber(42).AsString());

    [Fact]
    public void AsString_OnNullNode_ReturnsEmpty() =>
        Assert.Equal(string.Empty, DataSourceNode.CreateNull().AsString());

    [Fact]
    public void AsInt_ParsesCorrectly() => Assert.Equal(42, DataSourceNode.CreateNumber(42).As<int>());

    [Fact]
    public void AsInt_OnNonNumericString_Throws()
    {
        var node = DataSourceNode.CreateString("hello");
        Assert.Throws<FormatException>(() => node.As<int>());
    }

    [Fact]
    public void AsLong_ParsesCorrectly() =>
        Assert.Equal(9876543210L, DataSourceNode.CreateNumber(9876543210L).As<long>());

    [Fact]
    public void AsFloat_ParsesCorrectly() => Assert.Equal(3.14f, DataSourceNode.CreateNumber(3.14f).As<float>(), 0.001f);

    [Fact]
    public void AsDouble_ParsesCorrectly() =>
        Assert.Equal(2.718281828, DataSourceNode.CreateNumber(2.718281828).As<double>(), 0.000001);

    // ── 3. Object access ──

    [Fact]
    public void ObjectNode_IndexerByKey_ReturnsChild()
    {
        var obj = DataSourceNode.CreateObject()
            .Add("x", DataSourceNode.CreateString("val"));

        Assert.Equal("val", obj["x"].AsString());
    }

    [Fact]
    public void ObjectNode_IndexerByKey_ThrowsOnMissingKey()
    {
        var obj = DataSourceNode.CreateObject();

        Assert.Throws<KeyNotFoundException>(() => obj["missing"]);
    }

    [Fact]
    public void ObjectNode_TryGetValue_ReturnsTrueForExistingKey()
    {
        var obj = DataSourceNode.CreateObject()
            .Add("k", DataSourceNode.CreateNumber(1));

        Assert.True(obj.TryGetValue("k", out var child));
        Assert.NotNull(child);
        Assert.Equal(1, child!.As<int>());
    }

    [Fact]
    public void ObjectNode_TryGetValue_ReturnsFalseForMissingKey()
    {
        var obj = DataSourceNode.CreateObject();

        Assert.False(obj.TryGetValue("nope", out _));
    }

    [Fact]
    public void ObjectNode_ContainsKey_WorksCorrectly()
    {
        var obj = DataSourceNode.CreateObject()
            .Add("a", DataSourceNode.CreateNull());

        Assert.True(obj.ContainsKey("a"));
        Assert.False(obj.ContainsKey("b"));
    }

    [Fact]
    public void ObjectNode_Keys_ReturnsInsertionOrder()
    {
        var obj = DataSourceNode.CreateObject()
            .Add("z", DataSourceNode.CreateNull())
            .Add("a", DataSourceNode.CreateNull())
            .Add("m", DataSourceNode.CreateNull());

        Assert.Equal(new[] { "z", "a", "m" }, obj.Keys.ToArray());
    }

    // ── 4. Array access ──

    [Fact]
    public void ArrayNode_IndexerByIndex_ReturnsChild()
    {
        var arr = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateString("first"))
            .Add(DataSourceNode.CreateString("second"));

        Assert.Equal("first", arr[0].AsString());
        Assert.Equal("second", arr[1].AsString());
    }

    [Fact]
    public void ArrayNode_Count_ReflectsElements()
    {
        var arr = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateNull())
            .Add(DataSourceNode.CreateNull())
            .Add(DataSourceNode.CreateNull());

        Assert.Equal(3, arr.Count);
    }

    [Fact]
    public void ArrayNode_Elements_EnumeratesAll()
    {
        var arr = DataSourceNode.CreateArray()
            .Add(DataSourceNode.CreateNumber(1))
            .Add(DataSourceNode.CreateNumber(2));

        var values = arr.Elements.Select(e => e.As<int>()).ToArray();
        Assert.Equal(new[] { 1, 2 }, values);
    }

    // ── 5. Builder chaining ──

    [Fact]
    public void ObjectNode_Add_ReturnsSameNodeForChaining()
    {
        var obj = DataSourceNode.CreateObject();
        var returned = obj.Add("k", DataSourceNode.CreateNull());

        Assert.Same(obj, returned);
    }

    [Fact]
    public void ArrayNode_Add_ReturnsSameNodeForChaining()
    {
        var arr = DataSourceNode.CreateArray();
        var returned = arr.Add(DataSourceNode.CreateNull());

        Assert.Same(arr, returned);
    }

    // ── 6. Lazy expansion ──

    [Fact]
    public void CreateLazy_DoesNotCallExpanderUntilAccessed()
    {
        var callCount = 0;
        var lazy = DataSourceNode.CreateLazy("{\"v\":1}", _ =>
        {
            callCount++;
            return DataSourceNode.CreateObject()
                .Add("v", DataSourceNode.CreateNumber(1));
        });

        Assert.Equal(0, callCount);

        _ = lazy.Kind;

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void CreateLazy_ExpandsOnlyOnce()
    {
        var callCount = 0;
        var lazy = DataSourceNode.CreateLazy("raw", _ =>
        {
            callCount++;
            return DataSourceNode.CreateString("expanded");
        });

        _ = lazy.AsString();
        _ = lazy.AsString();

        Assert.Equal(1, callCount);
    }

    // ── 18. DataSourceFactory.CreateDefaultRegistry ──

    [Fact]
    public void CreateDefaultRegistry_RegistersAllExpectedTypes()
    {
        var tm = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(tm);

        // Primitives
        Assert.NotNull(registry.Get<string>());
        Assert.NotNull(registry.Get<byte>());
        Assert.NotNull(registry.Get<sbyte>());
        Assert.NotNull(registry.Get<short>());
        Assert.NotNull(registry.Get<ushort>());
        Assert.NotNull(registry.Get<int>());
        Assert.NotNull(registry.Get<uint>());
        Assert.NotNull(registry.Get<long>());
        Assert.NotNull(registry.Get<ulong>());
        Assert.NotNull(registry.Get<float>());
        Assert.NotNull(registry.Get<double>());
        Assert.NotNull(registry.Get<decimal>());
        Assert.NotNull(registry.Get<char>());
        Assert.NotNull(registry.Get<bool>());

        // Primitive arrays
        Assert.NotNull(registry.Get<byte[]>());
        Assert.NotNull(registry.Get<sbyte[]>());
        Assert.NotNull(registry.Get<short[]>());
        Assert.NotNull(registry.Get<ushort[]>());
        Assert.NotNull(registry.Get<int[]>());
        Assert.NotNull(registry.Get<uint[]>());
        Assert.NotNull(registry.Get<long[]>());
        Assert.NotNull(registry.Get<ulong[]>());
        Assert.NotNull(registry.Get<float[]>());
        Assert.NotNull(registry.Get<double[]>());
        Assert.NotNull(registry.Get<decimal[]>());
        Assert.NotNull(registry.Get<bool[]>());
        Assert.NotNull(registry.Get<char[]>());
        Assert.NotNull(registry.Get<string[]>());

        // Domain types
        Assert.NotNull(registry.Get<TypedData>());
        Assert.NotNull(registry.Get<NodeMetaData>());
        Assert.NotNull(registry.Get<StrategyMetaData>());
        Assert.NotNull(registry.Get<DataMetaData>());
        Assert.NotNull(registry.Get<SndMetaData>());
        Assert.NotNull(registry.Get<IReadOnlyList<SndMetaData>>());
        Assert.NotNull(registry.Get<IReadOnlyDictionary<string, TypedData>>());
        Assert.NotNull(registry.Get<IReadOnlyDictionary<string, string>>());
        Assert.NotNull(registry.Get<StateMachineContainerPayload>());
    }
}
