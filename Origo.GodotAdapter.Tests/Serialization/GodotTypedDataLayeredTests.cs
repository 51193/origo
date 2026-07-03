using System;
using Godot;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Snd;
using Origo.GodotAdapter;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class GodotTypedDataLayeredTests
{
    static GodotTypedDataLayeredTests()
    {
        _ = TypedDataInitializer.IsLoaded;
    }
    [Fact]
    public void Godot_Vector2_Kind_Is_Resolved()
    {
        var kind = TypedDataTypeMap.GetKindForType(typeof(Vector2));
        Assert.Equal((byte)128, kind);
    }

    [Fact]
    public void Godot_Vector3_Kind_Is_Resolved()
    {
        var kind = TypedDataTypeMap.GetKindForType(typeof(Vector3));
        Assert.Equal((byte)130, kind);
    }

    [Fact]
    public void Godot_Color_Kind_Is_Resolved()
    {
        var kind = TypedDataTypeMap.GetKindForType(typeof(Color));
        Assert.Equal((byte)137, kind);
    }

    [Fact]
    public void Godot_Transform3D_Kind_Is_Resolved()
    {
        var kind = TypedDataTypeMap.GetKindForType(typeof(Transform3D));
        Assert.Equal((byte)136, kind);
    }

    [Fact]
    public void Godot_Plane_Kind_Is_Resolved()
    {
        var kind = TypedDataTypeMap.GetKindForType(typeof(Plane));
        Assert.Equal((byte)141, kind);
    }

    [Fact]
    public void Godot_Vector2_FromObject_RoundTrip()
    {
        var v = new Vector2(1.5f, 3.0f);
        var td = new TypedData(128, 0, v);

        Assert.Equal(typeof(Vector2), td.DataType);
        Assert.Equal(v, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void Godot_Vector3_FromObject_RoundTrip()
    {
        var v = new Vector3(1.0f, 2.0f, 3.0f);
        var td = new TypedData(130, 0, v);

        Assert.Equal(typeof(Vector3), td.DataType);
        Assert.Equal(v, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void Godot_Color_FromObject_RoundTrip()
    {
        var c = new Color(0.1f, 0.2f, 0.3f, 0.4f);
        var td = new TypedData(137, 0, c);

        Assert.Equal(typeof(Color), td.DataType);
        Assert.Equal(c, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void Godot_Vector2_Extension_TryGet()
    {
        var v = new Vector2(5.0f, 7.0f);
        var td = new TypedData(128, 0, v);

        Assert.True(td.TryGetVector2(out var result));
        Assert.Equal(v, result);
    }

    [Fact]
    public void Godot_Vector3_Extension_TryGet()
    {
        var v = new Vector3(1.0f, 2.0f, 3.0f);
        var td = new TypedData(130, 0, v);

        Assert.True(td.TryGetVector3(out var result));
        Assert.Equal(v, result);
    }

    [Fact]
    public void Godot_Color_Extension_TryGet()
    {
        var c = new Color(0.5f, 0.6f, 0.7f, 0.8f);
        var td = new TypedData(137, 0, c);

        Assert.True(td.TryGetColor(out var result));
        Assert.Equal(c, result);
    }

    [Fact]
    public void Godot_Type_WrongKind_ReturnsFalse()
    {
        var v = new Vector2(1.0f, 2.0f);
        var td = new TypedData(128, 0, v);

        Assert.False(td.TryGetVector3(out _));
        Assert.False(td.TryGetColor(out _));
    }

    [Fact]
    public void Core_Int_DoesNotConflict_With_GodotKind()
    {
        var td = (TypedData)42;
        Assert.False(td.TryGetVector2(out _));
        Assert.True(td.TryGetInt32(out var v));
        Assert.Equal(42, v);
    }

    [Fact]
    public void All_GodotTypes_Registered()
    {
        var godotTypes = new[]
        {
            typeof(Vector2), typeof(Vector2I),
            typeof(Vector3), typeof(Vector3I), typeof(Vector4),
            typeof(Quaternion),
            typeof(Basis),
            typeof(Transform2D), typeof(Transform3D),
            typeof(Color),
            typeof(Rect2), typeof(Rect2I),
            typeof(Aabb),
            typeof(Plane)
        };

        foreach (var t in godotTypes)
        {
            var kind = TypedDataTypeMap.GetKindForType(t);
            Assert.True(kind >= 128, $"Type {t.Name} should have kind >= 128 but got {kind}");
            Assert.True(kind <= 141, $"Type {t.Name} should have kind <= 141 but got {kind}");
        }
    }

    [Fact]
    public void DataType_ForGodotType_ReturnsCorrectType()
    {
        var v = new Vector3(1, 2, 3);
        var td = new TypedData(130, 0, v);

        Assert.Equal(typeof(Vector3), td.DataType);
    }

    [Fact]
    public void Data_ForGodotType_ReturnsUnboxedValue()
    {
        var c = new Color(0.1f, 0.2f, 0.3f, 0.4f);
        var td = new TypedData(137, 0, c);

        Assert.Equal(c, TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void AsXxx_ForGodotType_Works()
    {
        var v = new Vector2(3.5f, 7.0f);
        var td = new TypedData(128, 0, v);

        Assert.Equal(v, td.AsVector2());
    }

    [Fact]
    public void TryGetAllGodotTypes_RoundTrip()
    {
        AssertRoundTrip(Vector2.One, td => td.TryGetVector2(out var v) ? v : default);
        AssertRoundTrip(Vector2I.One, td => td.TryGetVector2I(out var v) ? v : default);
        AssertRoundTrip(new Vector3(1, 2, 3), td => td.TryGetVector3(out var v) ? v : default);
        AssertRoundTrip(new Vector3I(1, 2, 3), td => td.TryGetVector3I(out var v) ? v : default);
        AssertRoundTrip(new Color(0.1f, 0.2f, 0.3f), td => td.TryGetColor(out var v) ? v : default);
        AssertRoundTrip(new Rect2(1, 2, 3, 4), td => td.TryGetRect2(out var v) ? v : default);
        AssertRoundTrip(new Rect2I(1, 2, 3, 4), td => td.TryGetRect2I(out var v) ? v : default);
    }

    [Fact]
    public void GodotType_Null_PreservesDataType()
    {
        var td = new TypedData(130, 0, null);

        Assert.Equal(typeof(Vector3), td.DataType);
        Assert.Null(TypedDataObjectConverter.ToObject(td));
    }

    [Fact]
    public void GodotType_ObjectConverter_ToObject_UsesFallback()
    {
        var v = new Vector3(5, 6, 7);
        var td = new TypedData(130, 0, v);

        var obj = TypedDataObjectConverter.ToObject(td);
        Assert.True(obj is Vector3);
        Assert.Equal(v, (Vector3)obj!);
    }

    [Fact]
    public void GodotType_ObjectConverter_FromObject_UsesFallback()
    {
        var v = new Vector3(8, 9, 10);

        var (inlineBits, refValue) = TypedDataObjectConverter.FromObject(130, v);
        Assert.Equal(0L, inlineBits);
        Assert.Equal(v, refValue);
    }

    [Fact]
    public void GodotKind_NotRecognized_ByCoreOnlyUnregistered()
    {
        var kind = TypedDataTypeMap.GetKindForType(typeof(Vector3));
        Assert.NotEqual((byte)0, kind);
        Assert.True(kind >= 128);

        var coreKind = TypedDataTypeMap.GetKindForType(typeof(int));
        Assert.Equal((byte)5, coreKind);
        Assert.True(coreKind < 128);
    }

    private static void AssertRoundTrip<T>(T value, Func<TypedData, T?> extractor) where T : struct
    {
        var kind = TypedDataTypeMap.GetKindForType(typeof(T));
        var td = new TypedData(kind, 0, value);
        var extracted = extractor(td);
        Assert.True(extracted.HasValue, $"Failed to extract {typeof(T).Name}");
        Assert.Equal(value, extracted.Value);
    }
}
