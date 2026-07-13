using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Origo.SourceGeneration;

public sealed partial class TypedDataGenerator
{
    private static void GenerateTypedDataFactory(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("internal static class TypedDataFactory<T>");
        sb.AppendLine("{");

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public static TypedData Create(T value)");
        sb.AppendLine("    {");

        foreach (var t in types)
        {
            var clrName = t.ClrTypeName;

            sb.AppendLine($"        if (typeof(T) == typeof({clrName}))");
            sb.AppendLine("        {");

            if (t.IsReferenceType)
            {
                sb.AppendLine($"            return new TypedData({t.KindValue}, 0, value);");
            }
            else
            {
                var localType = GetFactoryLocalType(t);
                var extractExpr = GetFactoryExtractExpr(t);
                sb.AppendLine($"            {localType} local = {extractExpr};");
                sb.AppendLine($"            return new TypedData({t.KindValue}, {GetFactoryBitsExpr(t)}, null);");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("        var kind = TypedDataTypeMap.GetKindForType(typeof(T));");
        sb.AppendLine("        if (kind != 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var result = TypedDataObjectConverter.FromObject(kind, value!);");
        sb.AppendLine("            return new TypedData(kind, result.inlineBits, result.refValue);");
        sb.AppendLine("        }");
        sb.AppendLine("        return new TypedData(TypedData.UnregisteredKind, 0, value);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public static bool TryExtract(TypedData source, out T value)");
        sb.AppendLine("    {");

        foreach (var t in types)
        {
            var clrName = t.ClrTypeName;

            sb.AppendLine($"        if (typeof(T) == typeof({clrName}) && source._kind == {t.KindValue})");
            sb.AppendLine("        {");

            if (t.IsReferenceType)
            {
                sb.AppendLine("            if (source._ref is T t) { value = t; return true; }");
            }
            else
            {
                var localType = GetFactoryLocalType(t);
                sb.AppendLine($"            {localType} local = {GetFactoryReadFromBitsExpr(t)};");
                sb.AppendLine($"            value = Unsafe.As<{localType}, T>(ref local);");
                sb.AppendLine("            return true;");
            }

            sb.AppendLine("        }");
        }

        sb.AppendLine("        if (source._kind != 0 && source._kind != TypedData.UnregisteredKind)");
        sb.AppendLine("        {");
        sb.AppendLine("            var obj = TypedDataObjectConverter.ToObject(source);");
        sb.AppendLine("            if (obj is T t1) { value = t1; return true; }");
        sb.AppendLine("        }");
        sb.AppendLine("        if (source._ref is T t2) { value = t2; return true; }");
        sb.AppendLine("        value = default!;");
        sb.AppendLine("        return false;");
        sb.AppendLine("    }");

        sb.AppendLine("}");
    }

    private static string GetFactoryLocalType(InlineTypeInfo t)
    {
        return t.SpecialType switch
        {
            SpecialType.System_Byte => "byte",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Char => "char",
            _ => "long"
        };
    }

    private static string GetFactoryExtractExpr(InlineTypeInfo t)
    {
        return t.SpecialType switch
        {
            SpecialType.System_Byte => "Unsafe.As<T, byte>(ref value)",
            SpecialType.System_SByte => "Unsafe.As<T, sbyte>(ref value)",
            SpecialType.System_Int16 => "Unsafe.As<T, short>(ref value)",
            SpecialType.System_UInt16 => "Unsafe.As<T, ushort>(ref value)",
            SpecialType.System_Int32 => "Unsafe.As<T, int>(ref value)",
            SpecialType.System_UInt32 => "Unsafe.As<T, uint>(ref value)",
            SpecialType.System_Int64 => "Unsafe.As<T, long>(ref value)",
            SpecialType.System_UInt64 => "Unsafe.As<T, ulong>(ref value)",
            SpecialType.System_Single => "Unsafe.As<T, float>(ref value)",
            SpecialType.System_Double => "Unsafe.As<T, double>(ref value)",
            SpecialType.System_Boolean => "Unsafe.As<T, bool>(ref value)",
            SpecialType.System_Char => "Unsafe.As<T, char>(ref value)",
            _ => "Unsafe.As<T, long>(ref value)"
        };
    }

    private static string GetFactoryBitsExpr(InlineTypeInfo t)
    {
        return t.SpecialType switch
        {
            SpecialType.System_Single => "BitConverter.SingleToInt32Bits(local)",
            SpecialType.System_Double => "BitConverter.DoubleToInt64Bits(local)",
            SpecialType.System_Boolean => "local ? 1 : 0",
            SpecialType.System_UInt32 or SpecialType.System_UInt64 => "(long)local",
            _ => "local"
        };
    }

    private static string GetFactoryReadFromBitsExpr(InlineTypeInfo t)
    {
        return t.SpecialType switch
        {
            SpecialType.System_Single => "BitConverter.Int32BitsToSingle((int)source._inlineBits)",
            SpecialType.System_Double => "BitConverter.Int64BitsToDouble(source._inlineBits)",
            SpecialType.System_Boolean => "source._inlineBits != 0",
            _ => $"({GetFactoryLocalType(t)})source._inlineBits"
        };
    }
}
