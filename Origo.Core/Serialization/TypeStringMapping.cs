using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origo.Core.Serialization;

/// <summary>
///     Assigns stable string identifiers to common types for use in JSON.
///     Mounted as an instance on <see cref="Snd.SndWorld" />, with lifecycle managed by the runtime.
///     Engine adapter layers can register additional types at startup via <see cref="RegisterType{T}" />.
/// </summary>
public sealed class TypeStringMapping
{
    private readonly Dictionary<Type, string> _reverseTypeMap = [];
    private readonly Dictionary<string, Type> _typeMap = [];

    /// <summary>Creates a mapping pre-populated with BCL primitive, array, and collection type names.</summary>
    public TypeStringMapping()
    {
        RegisterType<byte>(BclTypeNames.Byte);
        RegisterType<sbyte>(BclTypeNames.SByte);
        RegisterType<short>(BclTypeNames.Int16);
        RegisterType<ushort>(BclTypeNames.UInt16);
        RegisterType<int>(BclTypeNames.Int32);
        RegisterType<uint>(BclTypeNames.UInt32);
        RegisterType<long>(BclTypeNames.Int64);
        RegisterType<ulong>(BclTypeNames.UInt64);
        RegisterType<bool>(BclTypeNames.Boolean);
        RegisterType<float>(BclTypeNames.Single);
        RegisterType<double>(BclTypeNames.Double);
        RegisterType<decimal>(BclTypeNames.Decimal);
        RegisterType<char>(BclTypeNames.Char);
        RegisterType<string>(BclTypeNames.String);

        // Array types
        RegisterType<byte[]>(BclTypeNames.ArrayByte);
        RegisterType<sbyte[]>(BclTypeNames.ArraySByte);
        RegisterType<short[]>(BclTypeNames.ArrayInt16);
        RegisterType<ushort[]>(BclTypeNames.ArrayUInt16);
        RegisterType<int[]>(BclTypeNames.ArrayInt32);
        RegisterType<uint[]>(BclTypeNames.ArrayUInt32);
        RegisterType<long[]>(BclTypeNames.ArrayInt64);
        RegisterType<ulong[]>(BclTypeNames.ArrayUInt64);
        RegisterType<float[]>(BclTypeNames.ArraySingle);
        RegisterType<double[]>(BclTypeNames.ArrayDouble);
        RegisterType<decimal[]>(BclTypeNames.ArrayDecimal);
        RegisterType<bool[]>(BclTypeNames.ArrayBoolean);
        RegisterType<char[]>(BclTypeNames.ArrayChar);
        RegisterType<string[]>(BclTypeNames.ArrayString);

        // Immutable collection types
        RegisterType<IReadOnlyDictionary<string, string>>(BclTypeNames.IReadOnlyDictionaryStringString);
        RegisterType<ReadOnlyDictionary<string, string>>(BclTypeNames.ReadOnlyDictionaryStringString);
    }

    /// <summary>Registers a type-to-name mapping, throwing on collisions with an existing name.</summary>
    public void RegisterType<T>(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        var type = typeof(T);

        if (_typeMap.TryGetValue(typeName, out var existingType) && existingType != type)
            throw new InvalidOperationException(
                $"Type name '{typeName}' is already mapped to '{existingType.FullName}', cannot remap to '{type.FullName}'.");

        if (_reverseTypeMap.TryGetValue(type, out var existingName) &&
            !string.Equals(existingName, typeName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Type '{type.FullName}' is already mapped to '{existingName}', cannot remap to '{typeName}'.");

        _typeMap[typeName] = type;
        _reverseTypeMap[type] = typeName;
    }

    /// <summary>Resolves a registered name to its type, throwing when the name is unknown.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="typeName" /> is null or whitespace.</exception>
    public Type GetTypeByName(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        return _typeMap.TryGetValue(typeName, out var type)
            ? type
            : throw new InvalidOperationException($"Type name '{typeName}' is not registered.");
    }

    /// <summary>Resolves a type to its registered stable name, throwing when the type is unknown.</summary>
    public string GetNameByType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _reverseTypeMap.TryGetValue(type, out var typeName)
            ? typeName
            : throw new InvalidOperationException($"Type '{type.FullName}' is not registered.");
    }
}
