using System;
using System.Collections.Generic;

namespace Origo.Core.Serialization;

/// <summary>
///     在 JSON 中为常用类型分配稳定的字符串标识。
///     作为实例挂载在 <see cref="Snd.SndWorld" /> 上，生命周期随运行时管理。
///     引擎适配层可在启动时通过 <see cref="RegisterType{T}" /> 注册额外类型。
/// </summary>
public sealed class TypeStringMapping
{
    private readonly Dictionary<Type, string> _reverseTypeMap = new();
    private readonly Dictionary<string, Type> _typeMap = new();

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
    }

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

    public Type GetTypeByName(string typeName)
    {
        return _typeMap.TryGetValue(typeName, out var type)
            ? type
            : throw new InvalidOperationException($"Type name '{typeName}' is not registered.");
    }

    public string GetNameByType(Type type)
    {
        return _reverseTypeMap.TryGetValue(type, out var typeName)
            ? typeName
            : throw new InvalidOperationException($"Type '{type.FullName}' is not registered.");
    }
}
