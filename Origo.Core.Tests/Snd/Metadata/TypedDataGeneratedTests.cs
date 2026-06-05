using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Blackboard;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Verifies that the Source Generator produces correct TypedData members
///     for all types registered via [assembly: SndInlineTypes].
/// </summary>
public class TypedDataGeneratedTests
{
    [Fact]
    public void ImplicitConversion_Int32_RoundTrip()
    {
        TypedData td = (TypedData)42;
        Assert.True(td.TryGetInt32(out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void ImplicitConversion_Single_RoundTrip()
    {
        TypedData td = (TypedData)3.14f;
        Assert.True(td.TryGetSingle(out var v));
        Assert.Equal(3.14f, v, 0.0001f);
    }

    [Fact]
    public void ImplicitConversion_Double_RoundTrip()
    {
        TypedData td = (TypedData)2.718281828;
        Assert.True(td.TryGetDouble(out var v));
        Assert.Equal(2.718281828, v);
    }

    [Fact]
    public void ExplicitConversion_String_RoundTrip()
    {
        var td = TypedData.FromObject(typeof(string), "hello");
        Assert.True(td.TryGetString(out var v));
        Assert.Equal("hello", v);
    }

    [Fact]
    public void ImplicitConversion_Boolean_RoundTrip()
    {
        var tdTrue = (TypedData)true;
        Assert.True(tdTrue.TryGetBoolean(out var b));
        Assert.True(b);

        var tdFalse = (TypedData)false;
        Assert.True(tdFalse.TryGetBoolean(out b));
        Assert.False(b);
    }

    [Fact]
    public void ImplicitConversion_Byte_RoundTrip()
    {
        TypedData td = (TypedData)(byte)200;
        Assert.True(td.TryGetByte(out var v));
        Assert.Equal((byte)200, v);
    }

    [Fact]
    public void ExplicitConversion_Int64_RoundTrip()
    {
        TypedData td = (TypedData)((long)int.MaxValue + 1);
        Assert.True(td.TryGetInt64(out var v));
        Assert.Equal((long)int.MaxValue + 1, v);
    }

    [Fact]
    public void ImplicitConversion_Char_RoundTrip()
    {
        TypedData td = (TypedData)'Z';
        Assert.True(td.TryGetChar(out var v));
        Assert.Equal('Z', v);
    }

    [Fact]
    public void CrossTypeAccess_Int32AsSingle_ReturnsFalse()
    {
        TypedData td = (TypedData)42;
        Assert.False(td.TryGetSingle(out _));
    }

    [Fact]
    public void CrossTypeAccess_StringAsInt32_ReturnsFalse()
    {
        var td = TypedData.FromObject(typeof(string), "text");
        Assert.False(td.TryGetInt32(out _));
    }

    [Fact]
    public void NullSentinel_HasKindZero()
    {
        TypedData td = default;
        Assert.True(td.IsNull);
        Assert.False(td.TryGetInt32(out _));
        Assert.False(td.TryGetString(out _));
        Assert.False(td.TryGetSingle(out _));
    }

    [Fact]
    public void Equals_SameValueSameType_ReturnsTrue()
    {
        var a = (TypedData)100;
        var b = (TypedData)100;
        Assert.True(a.Equals(b));
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_DifferentValueSameType_ReturnsFalse()
    {
        var a = (TypedData)100;
        var b = (TypedData)200;
        Assert.False(a.Equals(b));
        Assert.False(a == b);
    }

    [Fact]
    public void Equals_DifferentType_SameInlineBits_ReturnsFalse()
    {
        var a = (TypedData)65;        // int Kind=Int32, bits=65
        var b = (TypedData)'A';       // char Kind=Char, bits=65
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_BothNull_ReturnsTrue()
    {
        Assert.True(default(TypedData).Equals(default));
        Assert.True(default(TypedData) == default);
    }

    [Fact]
    public void GetHashCode_SameValue_Consistent()
    {
        var a = (TypedData)42;
        var b = (TypedData)42;
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void DataType_ReturnsCorrectType()
    {
        Assert.Equal(typeof(int), ((TypedData)42).DataType);
        Assert.Equal(typeof(float), ((TypedData)1.5f).DataType);
        Assert.Equal(typeof(string), TypedData.FromObject(typeof(string), "test").DataType);
        Assert.Equal(typeof(bool), ((TypedData)true).DataType);
        Assert.Equal(typeof(double), ((TypedData)3.14).DataType);
    }

    [Fact]
    public void DataType_Null_ReturnsObject()
    {
        Assert.Equal(typeof(object), default(TypedData).DataType);
    }

    [Fact]
    public void TypedDataFactory_Create_Int32_Correct()
    {
        var td = TypedDataFactory<int>.Create(42);
        Assert.True(td.TryGetInt32(out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void TypedDataFactory_Create_String_Correct()
    {
        var td = TypedDataFactory<string>.Create("factory");
        Assert.True(td.TryGetString(out var v));
        Assert.Equal("factory", v);
    }

    [Fact]
    public void TypedDataFactory_Create_Float_Correct()
    {
        var td = TypedDataFactory<float>.Create(1.23f);
        Assert.True(td.TryGetSingle(out var v));
        Assert.Equal(1.23f, v, 0.0001f);
    }

    [Fact]
    public void TypedDataFactory_TryExtract_Int32_Correct()
    {
        var td = (TypedData)99;
        Assert.True(TypedDataFactory<int>.TryExtract(td, out var v));
        Assert.Equal(99, v);
    }

    [Fact]
    public void TypedDataFactory_TryExtract_WrongType_ReturnsFalse()
    {
        var td = (TypedData)99;
        Assert.False(TypedDataFactory<string>.TryExtract(td, out _));
    }

    [Fact]
    public void TypedDataFactory_TryExtract_FromDefault_ReturnsFalse()
    {
        Assert.False(TypedDataFactory<int>.TryExtract(default, out _));
    }

    [Fact]
    public void FromObject_RegisteredType_PreservesValue()
    {
        var td = TypedData.FromObject(typeof(int), 42);
        Assert.Equal(typeof(int), td.DataType);
        Assert.True(td.TryGetInt32(out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void FromObject_UnregisteredType_UsesRefSlot()
    {
        var guid = Guid.NewGuid();
        var td = TypedData.FromObject(typeof(Guid), guid);
        Assert.Equal(typeof(Guid), td.DataType);
        Assert.Equal(guid, td.Data);
    }

    [Fact]
    public void FromObject_NullValue_PreservesType()
    {
        var td = TypedData.FromObject(typeof(string), null);
        Assert.Equal(typeof(string), td.DataType);
        Assert.Null(td.Data);
    }

    [Fact]
    public void AllRegisteredTypes_AreCovered()
    {
        var typesToCheck = new[]
        {
            (TypedData)(byte)1,
            (TypedData)(sbyte)2,
            (TypedData)(short)3,
            (TypedData)(ushort)4,
            (TypedData)5,
            (TypedData)6u,
            (TypedData)7L,
            (TypedData)8UL,
            (TypedData)9.0f,
            (TypedData)10.0,
            (TypedData)true,
            (TypedData)'X',
            TypedData.FromObject(typeof(string), "Y")
        };

        foreach (var td in typesToCheck)
            Assert.False(td.IsNull);

        Assert.Equal(13, typesToCheck.Length);
    }

    [Fact]
    public void TypedDataTypeMap_GetKindForType_Correct()
    {
        Assert.NotEqual((byte)0, TypedDataTypeMap.GetKindForType(typeof(int)));
        Assert.NotEqual((byte)0, TypedDataTypeMap.GetKindForType(typeof(float)));
        Assert.NotEqual((byte)0, TypedDataTypeMap.GetKindForType(typeof(string)));
        Assert.Equal((byte)0, TypedDataTypeMap.GetKindForType(typeof(Guid)));
    }
}
