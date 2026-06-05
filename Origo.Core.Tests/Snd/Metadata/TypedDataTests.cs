using System;
using System.Collections.Generic;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class TypedDataTests
{
    [Fact]
    public void Constructor_StoresTypeAndValue()
    {
        var td = new TypedData(typeof(int), 42);
        Assert.Equal(typeof(int), td.DataType);
        Assert.Equal(42, td.Data);
    }

    [Fact]
    public void NullValue_IsAllowed()
    {
        var td = new TypedData(typeof(string), null);
        Assert.Equal(typeof(string), td.DataType);
        Assert.Null(td.Data);
    }

    [Fact]
    public void WithIntValue_PreservesExactType()
    {
        var td = new TypedData(typeof(int), 100);
        Assert.Equal(typeof(int), td.DataType);
        Assert.IsType<int>(td.Data);
        Assert.Equal(100, td.Data);
    }

    [Fact]
    public void WithFloatValue_PreservesExactType()
    {
        var td = new TypedData(typeof(float), 3.14f);
        Assert.Equal(typeof(float), td.DataType);
        Assert.IsType<float>(td.Data);
        Assert.Equal(3.14f, td.Data);
    }

    [Fact]
    public void WithDoubleValue_PreservesExactType()
    {
        var td = new TypedData(typeof(double), 2.718281828);
        Assert.Equal(typeof(double), td.DataType);
        Assert.IsType<double>(td.Data);
        Assert.Equal(2.718281828, td.Data);
    }

    [Fact]
    public void WithBoolValue_PreservesExactType()
    {
        var td = new TypedData(typeof(bool), true);
        Assert.Equal(typeof(bool), td.DataType);
        Assert.IsType<bool>(td.Data);
        Assert.Equal(true, td.Data);
    }

    [Fact]
    public void WithStringValue_PreservesExactType()
    {
        var td = new TypedData(typeof(string), "hello");
        Assert.Equal(typeof(string), td.DataType);
        Assert.IsType<string>(td.Data);
        Assert.Equal("hello", td.Data);
    }

    [Fact]
    public void WithStructValue_PreservesExactType()
    {
        var guid = Guid.NewGuid();
        var td = new TypedData(typeof(Guid), guid);
        Assert.Equal(typeof(Guid), td.DataType);
        Assert.IsType<Guid>(td.Data);
        Assert.Equal(guid, td.Data);
    }

    [Fact]
    public void WithDateTimeValue_PreservesExactType()
    {
        var dt = new DateTime(2024, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var td = new TypedData(typeof(DateTime), dt);
        Assert.Equal(typeof(DateTime), td.DataType);
        Assert.IsType<DateTime>(td.Data);
        Assert.Equal(dt, td.Data);
    }

    [Fact]
    public void WithBoxedInt_KeepsRuntimeType()
    {
        var td = new TypedData(typeof(int), 42);
        Assert.Equal(typeof(int), td.DataType);
        Assert.Equal(42, td.Data);
    }

    [Fact]
    public void WithArrayType_PreservesExactType()
    {
        var arr = new[] { 1, 2, 3 };
        var td = new TypedData(typeof(int[]), arr);
        Assert.Equal(typeof(int[]), td.DataType);
        Assert.Same(arr, td.Data);
    }

    [Fact]
    public void WithReferenceType_PreservesIdentity()
    {
        var obj = new object();
        var td = new TypedData(typeof(object), obj);
        Assert.Same(obj, td.Data);
    }

    [Fact]
    public void WithNullValueForReferenceType_PreservesTypeInfo()
    {
        var td = new TypedData(typeof(List<int>), null);
        Assert.Equal(typeof(object), td.DataType);
        Assert.Null(td.Data);
    }

    [Fact]
    public void TwoInstances_SameTypeAndSameValue_AreEqual()
    {
        var a = new TypedData(typeof(int), 42);
        var b = new TypedData(typeof(int), 42);
        Assert.Equal(a, b);
    }

    [Fact]
    public void TwoInstances_DifferentType_HaveDifferentReferences()
    {
        var a = new TypedData(typeof(int), 42);
        var b = new TypedData(typeof(long), 42L);
        Assert.NotEqual(a, b);
    }
}
