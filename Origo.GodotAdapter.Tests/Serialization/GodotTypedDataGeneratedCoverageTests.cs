using System;
using System.Collections.Generic;
using Godot;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter;
using Origo.GodotAdapter.Snd;
using Xunit;

namespace Origo.GodotAdapter.Tests;

/// <summary>
///     Exercises every generated adapter accessor (As*/TryGet*), the kind map,
///     and the object converter round trips for all 14 registered Godot types.
///     Written parametrically so every generated branch is executed.
/// </summary>
public class GodotTypedDataGeneratedCoverageTests
{
    static GodotTypedDataGeneratedCoverageTests()
    {
        _ = TypedDataInitializer.IsLoaded;
    }

    public static IEnumerable<(byte Kind, object Value)> AllTypes()
    {
        yield return (128, new Vector2(1.5f, 2.5f));
        yield return (129, new Vector2I(1, 2));
        yield return (130, new Vector3(1, 2, 3));
        yield return (131, new Vector3I(1, 2, 3));
        yield return (132, new Vector4(1, 2, 3, 4));
        yield return (133, new Quaternion(0, 0, 0, 1));
        yield return (134, Basis.Identity);
        yield return (135, Transform2D.Identity);
        yield return (136, Transform3D.Identity);
        yield return (137, new Color(0.1f, 0.2f, 0.3f, 0.4f));
        yield return (138, new Rect2(1, 2, 3, 4));
        yield return (139, new Rect2I(1, 2, 3, 4));
        yield return (140, new Aabb(new Vector3(1, 2, 3), new Vector3(4, 5, 6)));
        yield return (141, new Plane(new Vector3(0, 1, 0), 5));
    }

    [Fact]
    public void KindMap_ResolvesEveryRegisteredType()
    {
        foreach (var (kind, value) in AllTypes())
            Assert.Equal(kind, TypedDataTypeMap.GetKindForType(value.GetType()));
    }

    [Fact]
    public void Converter_FromObject_RoundTrips()
    {
        foreach (var (kind, value) in AllTypes())
        {
            var (inlineBits, refValue) = TypedDataObjectConverter.FromObject(kind, value);
            var td = new TypedData(kind, inlineBits, refValue);

            Assert.Equal(value.GetType(), td.DataType);
            Assert.Equal(value, TypedDataObjectConverter.ToObject(td));
        }
    }

    [Fact]
    public void Converter_UnregisteredKind_FromObjectFallsBackToRef()
    {
        foreach (var (kind, value) in AllTypes())
        {
            _ = kind;
            var (inlineBits, refValue) = TypedDataObjectConverter.FromObject(250, value);
            Assert.Equal(0, inlineBits);
            Assert.Same(value, refValue);
        }
    }

    [Fact]
    public void Converter_ToObject_UnregisteredKind_FallsBackToRef()
    {
        foreach (var (kind, value) in AllTypes())
        {
            _ = kind;
            Assert.Equal(value, TypedDataObjectConverter.ToObject(new TypedData(250, 0, value)));
            Assert.Null(TypedDataObjectConverter.ToObject(new TypedData(250, 0, null)));
        }
    }

    [Fact]
    public void Accessors_AsAndTryGet_ReturnValue()
    {
        foreach (var (kind, value) in AllTypes())
        {
            var td = new TypedData(kind, 0, value);

            Assert.Equal(value, As(td));
            Assert.True(TryGet(td, out var got));
            Assert.Equal(value, got);
        }
    }

    [Fact]
    public void Accessors_TryGet_RefTypeMismatch_Fails()
    {
        foreach (var (kind, value) in AllTypes())
        {
            _ = value;
            var td = new TypedData(kind, 0, "not-a-godot-value");

            Assert.False(TryGet(td, out _));
        }
    }

    [Fact]
    public void Accessors_As_UnregisteredKind_Throws()
    {
        foreach (var (kind, value) in AllTypes())
        {
            _ = kind;
            var td = new TypedData(250, 0, value);
            Assert.Throws<InvalidCastException>(() => As(td));
        }
    }

    private static object As(TypedData td)
    {
        return TypedDataTypeMap.GetKindForType(td.DataType) switch
        {
            128 => td.AsVector2(),
            129 => td.AsVector2I(),
            130 => td.AsVector3(),
            131 => td.AsVector3I(),
            132 => td.AsVector4(),
            133 => td.AsQuaternion(),
            134 => td.AsBasis(),
            135 => td.AsTransform2D(),
            136 => td.AsTransform3D(),
            137 => td.AsColor(),
            138 => td.AsRect2(),
            139 => td.AsRect2I(),
            140 => td.AsAabb(),
            141 => td.AsPlane(),
            _ => throw new InvalidCastException()
        };
    }

    private static bool TryGet(TypedData td, out object? value)
    {
        switch (TypedDataTypeMap.GetKindForType(td.DataType))
        {
            case 128: if (td.TryGetVector2(out var v2)) { value = v2; return true; } break;
            case 129: if (td.TryGetVector2I(out var v2i)) { value = v2i; return true; } break;
            case 130: if (td.TryGetVector3(out var v3)) { value = v3; return true; } break;
            case 131: if (td.TryGetVector3I(out var v3i)) { value = v3i; return true; } break;
            case 132: if (td.TryGetVector4(out var v4)) { value = v4; return true; } break;
            case 133: if (td.TryGetQuaternion(out var q)) { value = q; return true; } break;
            case 134: if (td.TryGetBasis(out var b)) { value = b; return true; } break;
            case 135: if (td.TryGetTransform2D(out var t2)) { value = t2; return true; } break;
            case 136: if (td.TryGetTransform3D(out var t3)) { value = t3; return true; } break;
            case 137: if (td.TryGetColor(out var c)) { value = c; return true; } break;
            case 138: if (td.TryGetRect2(out var r2)) { value = r2; return true; } break;
            case 139: if (td.TryGetRect2I(out var r2i)) { value = r2i; return true; } break;
            case 140: if (td.TryGetAabb(out var a)) { value = a; return true; } break;
            case 141: if (td.TryGetPlane(out var p)) { value = p; return true; } break;
        }

        value = null;
        return false;
    }
}
