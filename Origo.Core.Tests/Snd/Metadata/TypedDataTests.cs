using System;
using System.Collections.Generic;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

[Collection("TypedData")]
public class TypedDataTests
{
    public TypedDataTests()
    {
        TypedData.ResetForTesting();
    }

    [Fact]
    public void Constructor_StoresTypeAndValue()
    {
        var td = (TypedData)42;
        Assert.Equal(typeof(int), td.DataType);
        Assert.Equal(42, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void NullValue_IsAllowed()
    {
        var td = new TypedData(TypedData.KindMap.String, 0, null);
        Assert.Equal(typeof(string), td.DataType);
        Assert.Null(TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithIntValue_PreservesExactType()
    {
        var td = (TypedData)100;
        Assert.Equal(typeof(int), td.DataType);
        Assert.IsType<int>(TypedDataObjectConverter.ToObject(td));
        Assert.Equal(100, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithFloatValue_PreservesExactType()
    {
        var td = (TypedData)3.14f;
        Assert.Equal(typeof(float), td.DataType);
        Assert.IsType<float>(TypedDataObjectConverter.ToObject(td));
        Assert.Equal(3.14f, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithDoubleValue_PreservesExactType()
    {
        var td = (TypedData)2.718281828;
        Assert.Equal(typeof(double), td.DataType);
        Assert.IsType<double>(TypedDataObjectConverter.ToObject(td));
        Assert.Equal(2.718281828, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithBoolValue_PreservesExactType()
    {
        var td = (TypedData)true;
        Assert.Equal(typeof(bool), td.DataType);
        Assert.IsType<bool>(TypedDataObjectConverter.ToObject(td));
        Assert.Equal(true, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithStringValue_PreservesExactType()
    {
        var td = new TypedData(TypedData.KindMap.String, 0, "hello");
        Assert.Equal(typeof(string), td.DataType);
        Assert.IsType<string>(TypedDataObjectConverter.ToObject(td));
        Assert.Equal("hello", TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithStructValue_PreservesExactType()
    {
        var guid = Guid.NewGuid();
        var td = new TypedData(TypedData.UnregisteredKind, 0, guid);
        Assert.Equal(typeof(Guid), td.DataType);
        Assert.IsType<Guid>(TypedDataObjectConverter.ToObject(td));
        Assert.Equal(guid, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithDateTimeValue_PreservesExactType()
    {
        var dt = new DateTime(2024, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var td = new TypedData(TypedData.UnregisteredKind, 0, dt);
        Assert.Equal(typeof(DateTime), td.DataType);
        Assert.IsType<DateTime>(TypedDataObjectConverter.ToObject(td));
        Assert.Equal(dt, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithBoxedInt_KeepsRuntimeType()
    {
        var td = (TypedData)42;
        Assert.Equal(typeof(int), td.DataType);
        Assert.Equal(42, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithArrayType_PreservesExactType()
    {
        var arr = new[] { 1, 2, 3 };
        var td = new TypedData(TypedData.UnregisteredKind, 0, arr);
        Assert.Equal(typeof(int[]), td.DataType);
        Assert.Same(arr, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithReferenceType_PreservesIdentity()
    {
        var obj = new object();
        var td = new TypedData(TypedData.UnregisteredKind, 0, obj);
        Assert.Same(obj, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void WithNullValueForReferenceType_PreservesTypeInfo()
    {
        var td = new TypedData(TypedData.UnregisteredKind, 0, null);
        Assert.Equal(typeof(object), td.DataType);
        Assert.Null(TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void TwoInstances_SameTypeAndSameValue_AreEqual()
    {
        var a = (TypedData)42;
        var b = (TypedData)42;
        Assert.Equal(a, b);
    }

    [Fact]
    public void TwoInstances_DifferentType_HaveDifferentReferences()
    {
        var a = (TypedData)42;
        var b = (TypedData)42L;
        Assert.NotEqual(a, b);
    }

    // ── RegisterKind conflict detection (cross-assembly kind collisions) ──

    [Fact]
    public void RegisterKind_SameTypeTwice_IsIdempotent()
    {
        TypedData.RegisterKind(200, typeof(DateTime));
        TypedData.RegisterKind(200, typeof(DateTime));
        Assert.Equal(typeof(DateTime), TypedData.KindTypeMap[200]);
    }

    [Fact]
    public void RegisterKind_DifferentTypeSameKind_Throws()
    {
        TypedData.RegisterKind(201, typeof(DateTime));

        var ex = Assert.Throws<InvalidOperationException>(
            () => TypedData.RegisterKind(201, typeof(Guid)));
        Assert.Contains("201", ex.Message);
        Assert.Contains("DateTime", ex.Message);
        Assert.Contains("Guid", ex.Message);

        // The original mapping must survive the failed registration.
        Assert.Equal(typeof(DateTime), TypedData.KindTypeMap[201]);
    }

    [Fact]
    public void RegisterKind_KindZero_IsIgnored()
    {
        TypedData.RegisterKind(0, typeof(DateTime));
        Assert.Null(TypedData.KindTypeMap[0]);
    }

    [Fact]
    public void RegisterKind_UnregisteredKindSentinel_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => TypedData.RegisterKind(TypedData.UnregisteredKind, typeof(DateTime)));
        Assert.Contains(nameof(TypedData.UnregisteredKind), ex.Message);
        Assert.Null(TypedData.KindTypeMap[TypedData.UnregisteredKind]);
    }

    [Fact]
    public void RegisterKind_NullType_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypedData.RegisterKind(202, null!));
        Assert.Null(TypedData.KindTypeMap[202]);
    }
}
