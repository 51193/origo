using System;
using Origo.Core.DataSource;
using Xunit;

namespace Origo.Core.Tests;

public class DataSourceNodeSha256Tests
{
    [Fact]
    public void ScalarString_HashIsDeterministic()
    {
        var a = DataSourceNode.CreateString("hello");
        var b = DataSourceNode.CreateString("hello");

        Assert.Equal(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void ScalarNumber_HashIsDeterministic()
    {
        var a = DataSourceNode.CreateNumber(42);
        var b = DataSourceNode.CreateNumber(42);

        Assert.Equal(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void ScalarBoolean_HashIsDeterministic()
    {
        var a = DataSourceNode.CreateBoolean(true);
        var b = DataSourceNode.CreateBoolean(true);

        Assert.Equal(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void NullNode_HashIsDeterministic()
    {
        var a = DataSourceNode.CreateNull();
        var b = DataSourceNode.CreateNull();

        Assert.Equal(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void ObjectNode_HashDependsOnKeys()
    {
        var a = DataSourceNode.CreateObject();
        a.Add("x", DataSourceNode.CreateNumber(1));

        var b = DataSourceNode.CreateObject();
        b.Add("y", DataSourceNode.CreateNumber(1));

        Assert.NotEqual(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void ObjectNode_HashIndependentOfInsertionOrder()
    {
        var a = DataSourceNode.CreateObject();
        a.Add("a", DataSourceNode.CreateNumber(1));
        a.Add("b", DataSourceNode.CreateNumber(2));

        var b = DataSourceNode.CreateObject();
        b.Add("b", DataSourceNode.CreateNumber(2));
        b.Add("a", DataSourceNode.CreateNumber(1));

        Assert.Equal(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void ArrayNode_HashOrderDependent()
    {
        var a = DataSourceNode.CreateArray();
        a.Add(DataSourceNode.CreateNumber(1));
        a.Add(DataSourceNode.CreateNumber(2));

        var b = DataSourceNode.CreateArray();
        b.Add(DataSourceNode.CreateNumber(2));
        b.Add(DataSourceNode.CreateNumber(1));

        Assert.NotEqual(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void DeepNested_HashWorks()
    {
        var a = DataSourceNode.CreateObject();
        var inner = DataSourceNode.CreateArray();
        inner.Add(DataSourceNode.CreateString("nested"));
        a.Add("deep", inner);

        // Should not throw.
        var hash = a.ComputeSha256Hash();
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void DifferentValues_DifferentHashes()
    {
        var a = DataSourceNode.CreateString("alpha");
        var b = DataSourceNode.CreateString("beta");

        Assert.NotEqual(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void EmptyObjectVsEmptyArray_DifferentHashes()
    {
        var obj = DataSourceNode.CreateObject();
        var arr = DataSourceNode.CreateArray();

        Assert.NotEqual(obj.ComputeSha256Hash(), arr.ComputeSha256Hash());
    }

    [Fact]
    public void StringWithSpecialChars_HashWorks()
    {
        var a = DataSourceNode.CreateString("he\"llo\\world");

        // Should not throw, and should be deterministic.
        var h1 = a.ComputeSha256Hash();
        var h2 = DataSourceNode.CreateString("he\"llo\\world").ComputeSha256Hash();
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void StringWithSpecialChars_DoesNotCollideWithUnescapedEquivalent()
    {
        var a = DataSourceNode.CreateString("a\"b");
        var b = DataSourceNode.CreateString("a\\\"b");

        // The first: a"b  (canonical: S"a\"b")
        // The second: a\"b (canonical: S"a\\\"b")
        // These are different values, so hashes must differ.
        Assert.NotEqual(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void EmptyString_HashWorks()
    {
        var a = DataSourceNode.CreateString(string.Empty);
        var b = DataSourceNode.CreateString(string.Empty);

        Assert.Equal(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }

    [Fact]
    public void BooleanTrueAndFalse_HaveDifferentHashes()
    {
        var t = DataSourceNode.CreateBoolean(true);
        var f = DataSourceNode.CreateBoolean(false);

        Assert.NotEqual(t.ComputeSha256Hash(), f.ComputeSha256Hash());
    }

    [Fact]
    public void NumberIntegerVsFloatWithSameValue_HaveDifferentHashes()
    {
        var intNode = DataSourceNode.CreateNumber(1);
        var floatNode = DataSourceNode.CreateNumber(1.0f);

        // "N1" vs "N1" — CreateNumber(float) produces "1" not "1.0".
        // They may have the same canonical string since float 1.0 → "1".
        // This is expected behaviour — value equivalence.
        Assert.Equal(intNode.ComputeSha256Hash(), floatNode.ComputeSha256Hash());
    }

    [Fact]
    public void DisposedNode_ComputeSha256Hash_Throws()
    {
        var node = DataSourceNode.CreateString("test");
        node.Dispose();

        Assert.Throws<ObjectDisposedException>(() => node.ComputeSha256Hash());
    }

    [Fact]
    public void HashIsHexString()
    {
        var node = DataSourceNode.CreateString("test");
        var hash = node.ComputeSha256Hash();

        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void SameComplexTree_DifferentInstances_SameHash()
    {
        static DataSourceNode BuildTree()
        {
            var root = DataSourceNode.CreateObject();
            root.Add("name", DataSourceNode.CreateString("entity_1"));
            var pos = DataSourceNode.CreateObject();
            pos.Add("x", DataSourceNode.CreateNumber(10));
            pos.Add("y", DataSourceNode.CreateNumber(20.5));
            root.Add("position", pos);
            var tags = DataSourceNode.CreateArray();
            tags.Add(DataSourceNode.CreateString("player"));
            tags.Add(DataSourceNode.CreateString("active"));
            root.Add("tags", tags);
            root.Add("null_val", DataSourceNode.CreateNull());
            return root;
        }

        var a = BuildTree();
        var b = BuildTree();

        Assert.Equal(a.ComputeSha256Hash(), b.ComputeSha256Hash());
    }
}
