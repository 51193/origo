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
                var localType = t.ClrTypeName;
                var extractExpr = InlineTypeExprs.FactoryExtractExpr(t);
                sb.AppendLine($"            {localType} local = {extractExpr};");
                sb.AppendLine($"            return new TypedData({t.KindValue}, {InlineTypeExprs.Pack(t, "local")}, null);");
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
                sb.AppendLine("            value = (T)source._ref!;");
                sb.AppendLine("            return true;");
            }
            else
            {
                var localType = t.ClrTypeName;
                sb.AppendLine($"            {localType} local = {InlineTypeExprs.Unpack(t, "source._inlineBits")};");
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
}
