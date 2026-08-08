using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class TryGetNumericExtensionsTests
{
    [Fact]
    public void TryGetNumeric_FloatStored_ReturnsFloat()
    {
        var entity = new TestNumericEntity();
        entity.SetData("val", 3.14f);

        var result = entity.TryGetNumeric("val", out var value);
        Assert.True(result);
        Assert.Equal(3.14f, value);
    }

    [Fact]
    public void TryGetNumeric_IntStored_ReturnsFloat()
    {
        var entity = new TestNumericEntity();
        entity.SetData("val", 42);

        var result = entity.TryGetNumeric("val", out var value);
        Assert.True(result);
        Assert.Equal(42f, value);
    }

    [Fact]
    public void TryGetNumeric_LongStored_ReturnsFloat()
    {
        var entity = new TestNumericEntity();
        entity.SetData("val", 123L);

        var result = entity.TryGetNumeric("val", out var value);
        Assert.True(result);
        Assert.Equal(123f, value);
    }

    [Fact]
    public void TryGetNumeric_DoubleStored_ReturnsFloat()
    {
        var entity = new TestNumericEntity();
        entity.SetData("val", 2.5d);

        var result = entity.TryGetNumeric("val", out var value);
        Assert.True(result);
        Assert.Equal(2.5f, value);
    }

    [Fact]
    public void TryGetNumeric_StringStored_ReturnsFalse()
    {
        var entity = new TestNumericEntity();
        entity.SetData("val", "hello");

        var result = entity.TryGetNumeric("val", out var value);
        Assert.False(result);
        Assert.Equal(0f, value);
    }

    [Theory]
    [InlineData((byte)7, 7f)]
    [InlineData((sbyte)-3, -3f)]
    [InlineData((short)-1234, -1234f)]
    [InlineData((ushort)4321, 4321f)]
    [InlineData('x', 120f)]
    [InlineData(3000000000u, 3000000000f)]
    [InlineData(5000000000ul, 5000000000f)]
    public void TryGetNumeric_IntegerTypesStored_ReturnsFloat(object stored, float expected)
    {
        var entity = new TestNumericEntity();
        entity.SetData("val", stored);

        var result = entity.TryGetNumeric("val", out var value);
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetNumeric_BoolStored_ReturnsFalse()
    {
        var entity = new TestNumericEntity();
        entity.SetData("val", true);

        var result = entity.TryGetNumeric("val", out var value);
        Assert.False(result);
        Assert.Equal(0f, value);
    }

    [Fact]
    public void TryGetNumeric_MissingKey_ReturnsFalse()
    {
        var entity = new TestNumericEntity();
        var result = entity.TryGetNumeric("missing", out var value);
        Assert.False(result);
        Assert.Equal(0f, value);
    }

    [Fact]
    public void GetNumeric_FloatStored_ReturnsValue()
    {
        var entity = new TestNumericEntity();
        entity.SetData("val", 5.5f);

        var value = entity.GetNumeric("val", 0f);
        Assert.Equal(5.5f, value);
    }

    [Fact]
    public void GetNumeric_Missing_ReturnsFallback()
    {
        var entity = new TestNumericEntity();
        var value = entity.GetNumeric("missing", 10f);
        Assert.Equal(10f, value);
    }

    private sealed class TestNumericEntity : ISndDataAccess
    {
        private readonly Dictionary<string, TypedData> _data = new(StringComparer.Ordinal);

        public void SetData<T>(string name, T value) => _data[name] = new TypedData(TypedData.UnregisteredKind, 0, value);

        public T GetData<T>(string name) where T : notnull => throw new NotImplementedException();
        public (bool found, T? value) TryGetData<T>(string name)
        {
            if (_data.TryGetValue(name, out var td) && TypedDataObjectConverter.ToObject(td) is T v)
                return (true, v);
            return (false, default);
        }

        public bool TryGetData<T>(string name, out T? value)
        {
            var (found, stored) = TryGetData<T>(name);
            value = stored;
            return found;
        }

    }
}
