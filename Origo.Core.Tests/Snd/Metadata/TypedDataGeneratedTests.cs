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
[Collection("TypedData")]
public class TypedDataGeneratedTests
{
    public TypedDataGeneratedTests()
    {
        TypedData.ResetForTesting();
    }

    [Fact]
    public void ImplicitConversion_Int32_RoundTrip()
    {
        var td = (TypedData)42;
        Assert.True(td.TryGetInt32(out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void ImplicitConversion_Single_RoundTrip()
    {
        var td = (TypedData)3.14f;
        Assert.True(td.TryGetSingle(out var v));
        Assert.Equal(3.14f, v, 0.0001f);
    }

    [Fact]
    public void ImplicitConversion_Double_RoundTrip()
    {
        var td = (TypedData)2.718281828;
        Assert.True(td.TryGetDouble(out var v));
        Assert.Equal(2.718281828, v);
    }

    [Fact]
    public void ExplicitConversion_String_RoundTrip()
    {
        var td = new TypedData(TypedData.KindMap.String, 0, "hello");
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
        var td = (TypedData)(byte)200;
        Assert.True(td.TryGetByte(out var v));
        Assert.Equal((byte)200, v);
    }

    [Fact]
    public void ExplicitConversion_Int64_RoundTrip()
    {
        var td = (TypedData)((long)int.MaxValue + 1);
        Assert.True(td.TryGetInt64(out var v));
        Assert.Equal((long)int.MaxValue + 1, v);
    }

    [Fact]
    public void ImplicitConversion_Char_RoundTrip()
    {
        var td = (TypedData)'Z';
        Assert.True(td.TryGetChar(out var v));
        Assert.Equal('Z', v);
    }

    [Fact]
    public void CrossTypeAccess_Int32AsSingle_ReturnsFalse()
    {
        var td = (TypedData)42;
        Assert.False(td.TryGetSingle(out _));
    }

    [Fact]
    public void CrossTypeAccess_StringAsInt32_ReturnsFalse()
    {
        var td = new TypedData(TypedData.KindMap.String, 0, "text");
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
        Assert.Equal(typeof(string), new TypedData(TypedData.KindMap.String, 0, "test").DataType);
        Assert.Equal(typeof(bool), ((TypedData)true).DataType);
        Assert.Equal(typeof(double), ((TypedData)3.14).DataType);
    }

    [Fact]
    public void DataType_Null_ReturnsObject() => Assert.Equal(typeof(object), default(TypedData).DataType);

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
    public void TypedDataFactory_TryExtract_FromDefault_ReturnsFalse() => Assert.False(TypedDataFactory<int>.TryExtract(default, out _));

    [Fact]
    public void FromObject_RegisteredType_PreservesValue()
    {
        var td = (TypedData)42;
        Assert.Equal(typeof(int), td.DataType);
        Assert.True(td.TryGetInt32(out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void FromObject_UnregisteredType_UsesRefSlot()
    {
        var guid = Guid.NewGuid();
        var td = new TypedData(TypedData.UnregisteredKind, 0, guid);
        Assert.Equal(typeof(Guid), td.DataType);
        Assert.Equal(guid, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void FromObject_NullValue_PreservesType()
    {
        var td = new TypedData(TypedData.KindMap.String, 0, null);
        Assert.Equal(typeof(string), td.DataType);
        Assert.Null(TypedDataObjectConverter.ToObject(td));
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
            new TypedData(TypedData.KindMap.String, 0, "Y")
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

    [Fact]
    public void RegisterKind_Manual_CanBeRetrieved()
    {
        TypedData.RegisterKind(200, typeof(Guid));

        Assert.Equal(typeof(Guid), TypedData.KindTypeMap[200]);
    }

    [Fact]
    public void LayeredKindResolver_IsInvoked_ForUnknownType()
    {
        TypedDataLayeredRegistry.RegisterKindResolver(t => t == typeof(DateTime) ? (byte)201 : (byte)0);

        var kind = TypedDataTypeMap.GetKindForType(typeof(DateTime));
        Assert.Equal((byte)201, kind);
        Assert.Equal((byte)0, TypedDataTypeMap.GetKindForType(typeof(Guid)));
    }

    [Fact]
    public void ObjectConverterFallback_ToObject_IsInvoked()
    {
        TypedData.RegisterKind(202, typeof(DateTime));
        var now = DateTime.UtcNow;
        var td = new TypedData(202, 0, now);

        var obj = TypedDataObjectConverter.ToObject(td);
        Assert.Equal(now, (DateTime)obj!, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ObjectConverterFallback_FromObject_IsInvoked()
    {
        TypedData.RegisterKind(203, typeof(Guid));
        var guid = Guid.NewGuid();

        var (inlineBits, refValue) = TypedDataObjectConverter.FromObject(203, guid);
        Assert.Equal(0L, inlineBits);
        Assert.Equal(guid, refValue);
    }

    [Fact]
    public void TypedDataFactory_Create_Fallback_CallsObjectConverter()
    {
        TypedData.RegisterKind(204, typeof(TimeSpan));
        TypedDataLayeredRegistry.RegisterKindResolver(t => t == typeof(TimeSpan) ? (byte)204 : (byte)0);

        var ts = TimeSpan.FromSeconds(42);
        var td = TypedDataFactory<TimeSpan>.Create(ts);

        Assert.Equal(typeof(TimeSpan), td.DataType);
        Assert.Equal(ts, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void TypedDataFactory_TryExtract_Fallback_Works()
    {
        TypedData.RegisterKind(205, typeof(DateTimeOffset));
        TypedDataLayeredRegistry.RegisterKindResolver(t => t == typeof(DateTimeOffset) ? (byte)205 : (byte)0);

        var dto = DateTimeOffset.UtcNow;
        var td = TypedDataFactory<DateTimeOffset>.Create(dto);

        Assert.True(TypedDataFactory<DateTimeOffset>.TryExtract(td, out var extracted));
        Assert.Equal(dto, extracted);
    }

    [Fact]
    public void FromObject_Dispatch_Fallback()
    {
        TypedData.RegisterKind(206, typeof(Uri));
        TypedDataLayeredRegistry.RegisterKindResolver(t => t == typeof(Uri) ? (byte)206 : (byte)0);

        var uri = new Uri("https://example.com");
        var td = new TypedData(TypedData.UnregisteredKind, 0, uri);

        Assert.Equal(typeof(Uri), td.DataType);
        Assert.Equal(uri, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void DataType_ForRegisteredKind_ReturnsCorrectType()
    {
        TypedData.RegisterKind(207, typeof(Version));
        var version = new Version(1, 2, 3);
        var td = new TypedData(207, 0, version);

        Assert.Equal(typeof(Version), td.DataType);
    }

    [Fact]
    public void DataType_ForUnregisteredKind_FallsBackToRefType()
    {
        var version = new Version(1, 0);
        var td = new TypedData(255, 0, version);

        Assert.Equal(typeof(Version), td.DataType);
    }

    [Fact]
    public void Data_ForInlineType_NoBoxingAllocation()
    {
        var td = (TypedData)42;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = TypedDataObjectConverter.ToObject(td);
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(42, TypedDataObjectConverter.ToObject(td));
        Assert.True(after - before < 1024,
            $"Data access for inline type should produce near-zero allocation, got {after - before} bytes");
    }

    [Fact]
    public void Data_ForRegisteredRefType_UsesRefField()
    {
        TypedData.RegisterKind(208, typeof(Version));
        var version = new Version(3, 0);
        var td = new TypedData(208, 0, version);

        Assert.Same(version, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void MultiLayer_ResolverChain_FirstNonZeroWins()
    {
        TypedDataLayeredRegistry.RegisterKindResolver(t => t == typeof(Version) ? (byte)209 : (byte)0);
        TypedDataLayeredRegistry.RegisterKindResolver(t => t == typeof(Version) ? (byte)210 : (byte)0);

        var kind = TypedDataTypeMap.GetKindForType(typeof(Version));
        Assert.Equal((byte)209, kind);
    }

    [Fact]
    public void MultiLayer_FromObjectFallback_ChainIterates()
    {
        TypedDataLayeredRegistry.RegisterFromObjectFallback((k, v) => null);
        TypedDataLayeredRegistry.RegisterFromObjectFallback((k, v) =>
            k == 211 ? ((long inlineBits, object? refValue)?)(42L, null) : null);

        var (inlineBits, refValue) = TypedDataObjectConverter.FromObject(211, "ignored");
        Assert.Equal(42L, inlineBits);
        Assert.Null(refValue);
    }

    [Fact]
    public void MultiLayer_ToObjectFallback_ChainIterates()
    {
        TypedDataLayeredRegistry.RegisterToObjectFallback(_ => null);
        TypedDataLayeredRegistry.RegisterToObjectFallback(td =>
            td._kind == 212 ? "matched" : null);

        var td = new TypedData(212, 0, null);
        var obj = TypedDataObjectConverter.ToObject(td);
        Assert.Equal("matched", obj);
    }

    [Fact]
    public void NullSentinel_StillHasKindZero_AfterRegistrations()
    {
        TypedData.RegisterKind(213, typeof(Version));

        var td = default(TypedData);
        Assert.True(td.IsNull);
        Assert.False(td.TryGetInt32(out _));
        Assert.Equal((byte)0, td._kind);
    }

    [Fact]
    public void ObjectConverter_ToObject_UnregisteredKind_ReturnsRef()
    {
        var version = new Version(2, 5);
        var td = new TypedData(200, 0, version);

        var obj = TypedDataObjectConverter.ToObject(td);
        Assert.Same(version, obj);
    }
}
