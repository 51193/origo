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

    /// <summary>
    ///     The reserved kind sentinel for type-erased (unregistered) values.
    ///     Cannot be registered via <see cref="RegisterKind" />; registering it
    ///     throws <see cref="ArgumentOutOfRangeException" />.
    /// </summary>
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

    /// <summary>
    ///     The runtime type of the stored value: the registered type for
    ///     registered kinds, or the value's runtime type for unregistered
    ///     reference values.
    /// </summary>
    public Type DataType
    {
        get
        {
            if (_kind == UnregisteredKind) return _ref?.GetType() ?? typeof(object);
            return KindTypeMap[_kind] ?? typeof(object);
        }
    }

    /// <summary>
    ///     Whether this instance is the null sentinel (kind 0, no value).
    /// </summary>
    public bool IsNull => _kind == 0;

    /// <summary>Gets the null sentinel instance (kind 0, no value).</summary>
    public static TypedData Null => default;

    /// <summary>
    ///     Registers the CLR type for a kind byte. Idempotent for the same
    ///     type; registering a different type to an already-occupied kind
    ///     throws <see cref="InvalidOperationException" />.
    /// </summary>
    /// <param name="kind">The kind byte to register. Kind 0 (null sentinel) is
    ///     ignored; kind <see cref="UnregisteredKind" /> is rejected.</param>
    /// <param name="type">The CLR type to map to <paramref name="kind" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="type" /> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if <paramref name="kind" /> is the reserved
    ///     <see cref="UnregisteredKind" /> sentinel.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if <paramref name="kind" /> is already mapped to a different
    ///     type.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RegisterKind(byte kind, Type type)
    {
        if (kind == 0) return;
        if (kind == UnregisteredKind)
            throw new ArgumentOutOfRangeException(nameof(kind), kind,
                $"Kind {UnregisteredKind} is the reserved UnregisteredKind sentinel and cannot be registered.");
        ArgumentNullException.ThrowIfNull(type);
        var existing = KindTypeMap[kind];
        if (existing is not null && existing != type)
            throw new InvalidOperationException(
                $"TypedData kind {kind} is already registered to '{existing.FullName}'; " +
                $"cannot register '{type.FullName}' to the same kind. " +
                "Adapter layers must use non-overlapping kind ranges (see SndInlineTypesAttribute StartKind).");
        KindTypeMap[kind] = type;
    }

    /// <summary>
    ///     Value equality: same kind, and equal reference values or equal
    ///     inline bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TypedData other)
    {
        if (_kind != other._kind) return false;
        if (_kind == 0) return true;
        if (_ref is not null || other._ref is not null)
            return Equals(_ref, other._ref);
        return _inlineBits == other._inlineBits;
    }

    /// <summary>Value equality against another boxed <see cref="TypedData" />.</summary>
    public override bool Equals(object? obj) =>
        obj is TypedData other && Equals(other);

    /// <summary>Combined hash of the kind, inline bits, and reference value.</summary>
    public override int GetHashCode() =>
        HashCode.Combine(_kind, _inlineBits, _ref);

    /// <summary>Value equality operator.</summary>
    public static bool operator ==(TypedData left, TypedData right) =>
        left.Equals(right);

    /// <summary>Value inequality operator.</summary>
    public static bool operator !=(TypedData left, TypedData right) =>
        !left.Equals(right);

    /// <summary>Debug-friendly representation: "(Type)value" or "null".</summary>
    public override string ToString()
    {
        if (_kind == 0) return "null";
        var typeName = DataType.Name;
        var data = TypedDataObjectConverter.ToObject(this);
        return data is null ? $"({typeName})null" : $"({typeName}){data}";
    }
}
