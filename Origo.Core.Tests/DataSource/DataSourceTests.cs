using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Origo.Core.DataSource;
using Origo.Core.DataSource.Codec;
using Origo.Core.DataSource.Converters;
using Origo.Core.Snd.Metadata;
using Origo.Core.StateMachine;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

public class DataSourceTests
{
    // ── 23. DataSourceNode IDisposable ──

    [Fact]
    public void Dispose_PreventsSubsequentAccess()
    {
        var node = DataSourceNode.CreateString("test");
        node.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = node.Kind);
        Assert.Throws<ObjectDisposedException>(() => _ = node.AsString());
        Assert.Throws<ObjectDisposedException>(() => _ = node.IsNull);
    }

    [Fact]
    public void Dispose_RecursivelyDisposesChildren()
    {
        var child = DataSourceNode.CreateString("child");
        var parent = DataSourceNode.CreateObject()
            .Add("key", child);

        parent.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = child.Kind);
    }

    [Fact]
    public void Dispose_RecursivelyDisposesArrayChildren()
    {
        var child = DataSourceNode.CreateNumber(42);
        var parent = DataSourceNode.CreateArray()
            .Add(child);

        parent.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = child.As<int>());
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var node = DataSourceNode.CreateNull();
        node.Dispose();
        var ex = Record.Exception(node.Dispose);
        Assert.Null(ex);
        Assert.Throws<ObjectDisposedException>(() => _ = node.Kind);
    }

    [Fact]
    public void Dispose_LazyNodeReleasesExpander()
    {
        var expanderCalled = false;
        var node = DataSourceNode.CreateLazy("{}", _ =>
        {
            expanderCalled = true;
            return DataSourceNode.CreateObject();
        });

        node.Dispose();

        Assert.False(expanderCalled);
        Assert.Throws<ObjectDisposedException>(() => _ = node.Kind);
    }

    [Fact]
    public void UsingStatement_DisposesAfterScope()
    {
        DataSourceNode captured;
        using (var node = DataSourceNode.CreateString("scoped"))
        {
            captured = node;
            Assert.Equal("scoped", captured.AsString());
        }

        Assert.Throws<ObjectDisposedException>(() => _ = captured.AsString());
    }

    // ── 24. DataSourceNode new accessor methods ──

    [Fact]
    public void AsByte_ParsesCorrectly()
    {
        Assert.Equal((byte)0, DataSourceNode.CreateNumber("0").As<byte>());
        Assert.Equal((byte)255, DataSourceNode.CreateNumber("255").As<byte>());
    }

    [Fact]
    public void AsSByte_ParsesCorrectly()
    {
        Assert.Equal((sbyte)-128, DataSourceNode.CreateNumber("-128").As<sbyte>());
        Assert.Equal((sbyte)127, DataSourceNode.CreateNumber("127").As<sbyte>());
    }

    [Fact]
    public void AsShort_ParsesCorrectly()
    {
        Assert.Equal((short)-32768, DataSourceNode.CreateNumber("-32768").As<short>());
        Assert.Equal((short)32767, DataSourceNode.CreateNumber("32767").As<short>());
    }

    [Fact]
    public void AsUShort_ParsesCorrectly()
    {
        Assert.Equal((ushort)0, DataSourceNode.CreateNumber("0").As<ushort>());
        Assert.Equal((ushort)65535, DataSourceNode.CreateNumber("65535").As<ushort>());
    }

    [Fact]
    public void AsUInt_ParsesCorrectly()
    {
        Assert.Equal(0u, DataSourceNode.CreateNumber("0").As<uint>());
        Assert.Equal(4294967295u, DataSourceNode.CreateNumber("4294967295").As<uint>());
    }

    [Fact]
    public void AsULong_ParsesCorrectly()
    {
        Assert.Equal(0ul, DataSourceNode.CreateNumber("0").As<ulong>());
        Assert.Equal(18446744073709551615ul, DataSourceNode.CreateNumber("18446744073709551615").As<ulong>());
    }

    [Fact]
    public void AsDecimal_ParsesCorrectly()
    {
        Assert.Equal(3.14159m, DataSourceNode.CreateNumber("3.14159").As<decimal>());
        Assert.Equal(-99.99m, DataSourceNode.CreateNumber("-99.99").As<decimal>());
    }

    [Fact]
    public void AsChar_ParsesCorrectly()
    {
        Assert.Equal('A', DataSourceNode.CreateString("A").AsChar());
        Assert.Equal('\u4e2d', DataSourceNode.CreateString("\u4e2d").AsChar());
    }

    // ── 25. TypeStringMapping new type registrations ──

    [Fact]
    public void TypeStringMapping_RegistersAllNewTypes()
    {
        var tm = new TypeStringMapping();

        // Verify all primitive types are registered
        Assert.Equal(typeof(byte), tm.GetTypeByName("Byte"));
        Assert.Equal(typeof(sbyte), tm.GetTypeByName("SByte"));
        Assert.Equal(typeof(short), tm.GetTypeByName("Int16"));
        Assert.Equal(typeof(ushort), tm.GetTypeByName("UInt16"));
        Assert.Equal(typeof(int), tm.GetTypeByName("Int32"));
        Assert.Equal(typeof(uint), tm.GetTypeByName("UInt32"));
        Assert.Equal(typeof(long), tm.GetTypeByName("Int64"));
        Assert.Equal(typeof(ulong), tm.GetTypeByName("UInt64"));
        Assert.Equal(typeof(float), tm.GetTypeByName("Single"));
        Assert.Equal(typeof(double), tm.GetTypeByName("Double"));
        Assert.Equal(typeof(decimal), tm.GetTypeByName("Decimal"));
        Assert.Equal(typeof(char), tm.GetTypeByName("Char"));
        Assert.Equal(typeof(bool), tm.GetTypeByName("Boolean"));
        Assert.Equal(typeof(string), tm.GetTypeByName("String"));

        // Verify all array types are registered
        Assert.Equal(typeof(byte[]), tm.GetTypeByName("ArrayByte"));
        Assert.Equal(typeof(sbyte[]), tm.GetTypeByName("ArraySByte"));
        Assert.Equal(typeof(short[]), tm.GetTypeByName("ArrayInt16"));
        Assert.Equal(typeof(ushort[]), tm.GetTypeByName("ArrayUInt16"));
        Assert.Equal(typeof(int[]), tm.GetTypeByName("ArrayInt32"));
        Assert.Equal(typeof(uint[]), tm.GetTypeByName("ArrayUInt32"));
        Assert.Equal(typeof(long[]), tm.GetTypeByName("ArrayInt64"));
        Assert.Equal(typeof(ulong[]), tm.GetTypeByName("ArrayUInt64"));
        Assert.Equal(typeof(float[]), tm.GetTypeByName("ArraySingle"));
        Assert.Equal(typeof(double[]), tm.GetTypeByName("ArrayDouble"));
        Assert.Equal(typeof(decimal[]), tm.GetTypeByName("ArrayDecimal"));
        Assert.Equal(typeof(bool[]), tm.GetTypeByName("ArrayBoolean"));
        Assert.Equal(typeof(char[]), tm.GetTypeByName("ArrayChar"));
        Assert.Equal(typeof(string[]), tm.GetTypeByName("ArrayString"));
    }

    // ── Lazy expansion failure recovery ──

    [Fact]
    public void LazyNode_WhenExpanderThrows_NodeStaysLazy_AndCanRetrySuccessfully()
    {
        var callCount = 0;

        DataSourceNode expander(string raw)
        {
            callCount++;
            if (callCount == 1)
                throw new InvalidOperationException("Simulated first-time expansion failure.");
            return DataSourceNode.CreateString("hello");
        }

        var lazyNode = DataSourceNode.CreateLazy("{}", expander);

        // First access should throw
        Assert.Throws<InvalidOperationException>(() => lazyNode.Kind);

        // Second access should succeed because node stayed in lazy state
        Assert.Equal(DataSourceNodeKind.Text, lazyNode.Kind);
        Assert.Equal("hello", lazyNode.AsString());
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void LazyNode_WhenExpanderThrows_NodeCanStillBeDisposed()
    {
        static DataSourceNode expander(string raw)
        {
            throw new InvalidOperationException("Always fails.");
        }

        var lazyNode = DataSourceNode.CreateLazy("{}", expander);

        Assert.Throws<InvalidOperationException>(() => lazyNode.Kind);

        // Dispose should succeed even though expansion failed
        lazyNode.Dispose();
        Assert.Throws<ObjectDisposedException>(() => lazyNode.Kind);
    }

    // ── MapDataSourceCodec edge cases ──

    [Fact]
    public void MapCodec_Decode_LineWithoutColon_Throws()
    {
        var codec = TestFactory.CreateMapCodec();
        var text = "validkey: value\nno_colon_here\nanotherkey: value2";

        Assert.Throws<FormatException>(() => codec.Decode(text));
    }

    [Fact]
    public void MapCodec_Decode_EmptyValueAfterColon_ReturnsEmptyString()
    {
        var codec = TestFactory.CreateMapCodec();
        var text = "emptyval:";
        var node = codec.Decode(text);

        Assert.True(node.ContainsKey("emptyval"));
        Assert.Equal("", node["emptyval"].AsString());
    }

    [Fact]
    public void MapCodec_Decode_OnlyCommentsAndEmptyLines_ReturnsEmptyObject()
    {
        var codec = TestFactory.CreateMapCodec();
        var text = "# comment\n\n  # another comment\n   ";
        var node = codec.Decode(text);

        Assert.Equal(DataSourceNodeKind.Map, node.Kind);
        Assert.Empty(node.Keys);
    }

    [Fact]
    public void MapCodec_Encode_RejectsMultilineValue()
    {
        // A value containing a line break would be written as several lines
        // that the strict decoder cannot parse back: the codec must reject
        // such values instead of producing files it cannot read.
        var codec = TestFactory.CreateMapCodec();
        var node = DataSourceNode.CreateObject()
            .Add("key", DataSourceNode.CreateString("line1\nline2"));

        Assert.Throws<InvalidOperationException>(() => codec.Encode(node));
    }

    [Fact]
    public void MapCodec_Encode_RejectsColonInKey()
    {
        // A colon in the key is the decoder's field separator; writing it
        // would silently split one entry into a different key/value pair.
        var codec = TestFactory.CreateMapCodec();
        var node = DataSourceNode.CreateObject()
            .Add("a:b", DataSourceNode.CreateString("v"));

        Assert.Throws<InvalidOperationException>(() => codec.Encode(node));
    }

    [Fact]
    public void MapCodec_Encode_RejectsCommentKey()
    {
        // A key starting with '#' would make the decoder treat the whole
        // line as a comment and silently drop the entry.
        var codec = TestFactory.CreateMapCodec();
        var node = DataSourceNode.CreateObject()
            .Add("#comment", DataSourceNode.CreateString("hidden"));

        Assert.Throws<InvalidOperationException>(() => codec.Encode(node));
    }

    [Fact]
    public void MapCodec_Encode_RejectsUntrimmedKeyOrValue()
    {
        // The strict decoder trims both fields; writing leading/trailing
        // whitespace would change the key/value on read-back.
        var codec = TestFactory.CreateMapCodec();

        Assert.Throws<InvalidOperationException>(() => codec.Encode(
            DataSourceNode.CreateObject().Add(" padded ", DataSourceNode.CreateString("v"))));
        Assert.Throws<InvalidOperationException>(() => codec.Encode(
            DataSourceNode.CreateObject().Add("k", DataSourceNode.CreateString(" v "))));
    }

    [Fact]
    public void MapCodec_Encode_RejectsNonTextChild()
    {
        // .map is a string-keyed string-value format. A number/bool child
        // would decode back as Text and silently lose its type.
        var codec = TestFactory.CreateMapCodec();

        Assert.Throws<InvalidOperationException>(() => codec.Encode(
            DataSourceNode.CreateObject().Add("n", DataSourceNode.CreateNumber(42))));
        Assert.Throws<InvalidOperationException>(() => codec.Encode(
            DataSourceNode.CreateObject().Add("b", DataSourceNode.CreateBoolean(true))));
    }

    [Fact]
    public void MapCodec_Encode_EmptyKey_Throws()
    {
        var codec = TestFactory.CreateMapCodec();
        var node = DataSourceNode.CreateObject().Add("", DataSourceNode.CreateString("v"));

        Assert.Throws<InvalidOperationException>(() => codec.Encode(node));
    }

    [Fact]
    public void DataSourceNode_Keys_OnNonMap_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DataSourceNode.CreateArray().Keys.Count());
        Assert.Throws<InvalidOperationException>(() => DataSourceNode.CreateString("x").Keys.Count());
    }

    [Fact]
    public void DataSourceNode_CountAndElements_OnNonArray_Throw()
    {
        var scalar = DataSourceNode.CreateString("x");
        var map = DataSourceNode.CreateObject();

        Assert.Throws<InvalidOperationException>(() => scalar.Count);
        Assert.Throws<InvalidOperationException>(() => scalar.Elements.Count());
        Assert.Throws<InvalidOperationException>(() => map.Count);
        Assert.Throws<InvalidOperationException>(() => map.Elements.Count());
    }

    [Fact]
    public void MapCodec_DuplicateKey_WarningIsObservable()
    {
        // The duplicate-key warning must reach a real logger instead of
        // being silently discarded.
        var logger = new TestLogger();
        var codec = new MapDataSourceCodec(logger);

        var node = codec.Decode("a: 1\na: 2");

        Assert.Single(node.Keys);
        Assert.Contains(logger.Warnings, w => w.Contains("Duplicate key", StringComparison.Ordinal));
    }

    // ── 26. DataSourceConverterRegistry type hierarchy fallback ──

    [Fact]
    public void ConverterRegistry_TypeHierarchyFallback_FindsInterfaceConverterForConcreteType()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var dict = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
        var boxed = (object)dict;

#pragma warning disable CA2263
        var node = registry.Write(typeof(ReadOnlyDictionary<string, string>), boxed);
        var result = registry.Read(typeof(IReadOnlyDictionary<string, string>), node);
#pragma warning restore CA2263
        var castResult = Assert.IsType<IReadOnlyDictionary<string, string>>(result, exactMatch: false);

        Assert.Equal(2, castResult.Count);
        Assert.Equal("1", castResult["a"]);
        Assert.Equal("2", castResult["b"]);
    }

    [Fact]
    public void ConverterRegistry_TypeHierarchyFallback_ExactTypeMatchStillWorks()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var node = registry.Write(42);
        var result = registry.Read<int>(node);

        Assert.Equal(42, result);
    }

    // ── 27. ReadOnlyDictionary blackboard round-trip ──

    [Fact]
    public void ReadOnlyDictionary_BlackboardRoundTrip_SurvivesSerialization()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new Blackboard.Blackboard();
        original.SetValue<IReadOnlyDictionary<string, string>>("map", new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string> { ["x"] = "10", ["y"] = "20" }));

        var serialized = original.SerializeAll();
        var node = registry.Write(serialized);
        var restoredDict = registry.Read<IReadOnlyDictionary<string, TypedData>>(node);
        var restored = new Blackboard.Blackboard();
        restored.DeserializeAll(restoredDict);

        var (found, value) = restored.TryGet<IReadOnlyDictionary<string, string>>("map");
        Assert.True(found);
        Assert.NotNull(value);
        Assert.Equal(2, value!.Count);
        Assert.Equal("10", value["x"]);
        Assert.Equal("20", value["y"]);
    }

    [Fact]
    public void TypeStringMapping_RegistersReadOnlyDictionary()
    {
        var tm = new TypeStringMapping();

        Assert.Equal(typeof(ReadOnlyDictionary<string, string>),
            tm.GetTypeByName(BclTypeNames.ReadOnlyDictionaryStringString));
        Assert.Equal(typeof(IReadOnlyDictionary<string, string>),
            tm.GetTypeByName(BclTypeNames.IReadOnlyDictionaryStringString));
    }

    [Fact]
    public void Dispose_DeeplyNestedTree_DoesNotStackOverflow()
    {
        const int depth = 2000;
        var leaf = DataSourceNode.CreateNumber(1);
        for (var i = 0; i < depth; i++)
        {
            var parent = DataSourceNode.CreateObject();
            parent.Add("child", leaf);
            leaf = parent;
        }

        leaf.Dispose();
    }

    [Fact]
    public void ComputeSha256Hash_DeeplyNestedTree_DoesNotStackOverflow()
    {
        const int depth = 2000;
        var leaf = DataSourceNode.CreateNumber(1);
        for (var i = 0; i < depth; i++)
        {
            var parent = DataSourceNode.CreateObject();
            parent.Add("child", leaf);
            leaf = parent;
        }

        var hash = leaf.ComputeSha256Hash();
        Assert.NotEmpty(hash);
    }

    // ── 28. Generic Read<T> through the interface chain returns a T-compatible instance ──

    [Fact]
    public void ConverterRegistry_GenericRead_ConcreteTypeThroughInterfaceChain()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var node = registry.Write<ReadOnlyDictionary<string, string>>(
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }));

        var result = registry.Read<ReadOnlyDictionary<string, string>>(node);

        Assert.Equal("1", result["a"]);
        Assert.Equal("2", result["b"]);
    }

    [Fact]
    public void ConverterRegistry_GenericRead_IncompatibleRequest_FailsFastWithClearError()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var node = registry.Write<ReadOnlyDictionary<string, string>>(
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string> { ["a"] = "1" }));

        // SortedDictionary has no converter of its own and the interface-chain
        // converter cannot produce it; the request must fail fast with a clear
        // error instead of an opaque InvalidCastException.
        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Read<SortedDictionary<string, string>>(node));
        Assert.Contains("SortedDictionary", ex.Message);
    }

    // ── 29. ReadOnlyDictionary blackboard round-trip preserves the concrete type ──

    [Fact]
    public void ReadOnlyDictionary_BlackboardRoundTrip_PreservesConcreteTypeAndResaves()
    {
        var tm = new TypeStringMapping();
        var registry = TestFactory.CreateRegistry(tm);

        var original = new Blackboard.Blackboard();
        original.SetValue<IReadOnlyDictionary<string, string>>("map",
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string> { ["x"] = "10", ["y"] = "20" }));

        var serialized = original.SerializeAll();
        var node = registry.Write(serialized);
        var restoredDict = registry.Read<IReadOnlyDictionary<string, TypedData>>(node);
        var restored = new Blackboard.Blackboard();
        restored.DeserializeAll(restoredDict);

        var (found, value) = restored.TryGet<ReadOnlyDictionary<string, string>>("map");
        Assert.True(found);
        Assert.NotNull(value);
        Assert.Equal("10", value!["x"]);

        // A resave must succeed: the round-trip must not drift the stored type
        // to Dictionary (whose stable name is not registered).
        _ = restored.SerializeAll();
    }
}
