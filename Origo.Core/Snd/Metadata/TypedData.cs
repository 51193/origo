using System;
using System.Runtime.CompilerServices;

namespace Origo.Core.Snd.Metadata;

/// <summary>
///     Tagged-union struct for zero-boxing storage of registered value types.
///     The Source Generator reads <see cref="SndInlineTypesAttribute" /> and
///     generates type-specific constructors, accessors, and factory methods.
/// </summary>
public readonly partial struct TypedData : IEquatable<TypedData>
{
    internal static readonly Type?[] KindTypeMap = new Type?[256];

    public const byte UnregisteredKind = 255;

    internal readonly byte _kind;
    internal readonly long _inlineBits;
    internal readonly object? _ref;

    internal TypedData(byte kind, long inlineBits, object? refValue)
    {
        _kind = kind;
        _inlineBits = inlineBits;
        _ref = refValue;
    }

    public Type DataType
    {
        get
        {
            if (_kind == UnregisteredKind) return _ref?.GetType() ?? typeof(object);
            return KindTypeMap[_kind] ?? typeof(object);
        }
    }

    public bool IsNull => _kind == 0;

    public static TypedData Null => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RegisterKind(byte kind, Type type)
    {
        if (kind == 0) return;
        var existing = KindTypeMap[kind];
        if (existing is not null && existing != type)
            throw new InvalidOperationException(
                $"TypedData kind {kind} is already registered to '{existing.FullName}'; " +
                $"cannot register '{type.FullName}' to the same kind. " +
                "Adapter layers must use non-overlapping kind ranges (see SndInlineTypesAttribute StartKind).");
        KindTypeMap[kind] = type ?? typeof(object);
    }

    internal static void ResetForTesting()
    {
        Array.Clear(KindTypeMap, 0, KindTypeMap.Length);
        TypedDataLayeredRegistry.Reset();
        TypedDataHomeKindRegistration.Initialize();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TypedData other)
    {
        if (_kind != other._kind) return false;
        if (_kind == 0) return true;
        if (_ref is not null || other._ref is not null)
            return Equals(_ref, other._ref);
        return _inlineBits == other._inlineBits;
    }

    public override bool Equals(object? obj) =>
        obj is TypedData other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(_kind, _inlineBits, _ref);

    public static bool operator ==(TypedData left, TypedData right) =>
        left.Equals(right);

    public static bool operator !=(TypedData left, TypedData right) =>
        !left.Equals(right);

    public override string ToString()
    {
        if (_kind == 0) return "null";
        var typeName = DataType.Name;
        var data = TypedDataObjectConverter.ToObject(this);
        return data is null ? $"({typeName})null" : $"({typeName}){data}";
    }
}
