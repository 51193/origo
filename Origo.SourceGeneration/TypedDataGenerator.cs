using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Origo.SourceGeneration;

[Generator(LanguageNames.CSharp)]
public sealed class TypedDataGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Origo.Core.Snd.Metadata.SndInlineTypesAttribute";
    private const string TypedDataFullName = "Origo.Core.Snd.Metadata.TypedData";
    private const string RegistryFullName = "Origo.Core.Snd.Metadata.TypedDataLayeredRegistry";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inputProvider = context.CompilationProvider
            .Select((compilation, ct) => ExtractGenerationInput(compilation));

        context.RegisterSourceOutput(inputProvider, GenerateTypedDataExtensions);
    }

    private static GenerationInput ExtractGenerationInput(Compilation compilation)
    {
        var isHome = IsHomeAssembly(compilation);
        var typeGroups = new List<TypeGroup>();

        foreach (var attributeData in compilation.Assembly.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() != AttributeFullName)
                continue;

            var startKind = ExtractStartKind(attributeData);
            var types = ExtractTypes(attributeData, startKind);

            if (types.Count > 0)
                typeGroups.Add(new TypeGroup { StartKind = startKind, Types = types });
        }

        return new GenerationInput
        {
            IsHome = isHome,
            TypeGroups = typeGroups
        };
    }

    private static bool IsHomeAssembly(Compilation compilation)
    {
        var typedDataSymbol = compilation.GetTypeByMetadataName(TypedDataFullName);
        return typedDataSymbol?.ContainingAssembly
            .Equals(compilation.Assembly, SymbolEqualityComparer.Default) == true;
    }

    private static int ExtractStartKind(AttributeData attr)
    {
        foreach (var ctorArg in attr.ConstructorArguments)
        {
            if (ctorArg is { Kind: TypedConstantKind.Primitive, Value: int startKind })
                return startKind;
        }
        return 1;
    }

    private static List<InlineTypeInfo> ExtractTypes(AttributeData attr, int startKind)
    {
        var result = new List<InlineTypeInfo>();
        byte kindOffset = 0;

        foreach (var ctorArg in attr.ConstructorArguments)
        {
            if (ctorArg.Kind == TypedConstantKind.Array)
            {
                foreach (var element in ctorArg.Values)
                {
                    if (element.Value is INamedTypeSymbol typeSymbol)
                        result.Add(CreateTypeInfo(typeSymbol, (byte)(startKind + kindOffset++)));
                    else if (element.Value is ITypeSymbol ts)
                        result.Add(CreateTypeInfo(ts, (byte)(startKind + kindOffset++)));
                }
            }
            else if (ctorArg.Value is INamedTypeSymbol singleType)
            {
                result.Add(CreateTypeInfo(singleType, (byte)(startKind + kindOffset++)));
            }
        }

        return result;
    }

    private static InlineTypeInfo CreateTypeInfo(ITypeSymbol typeSymbol, byte kindValue)
    {
        var kindName = GenerateKindName(typeSymbol);
        var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var clrName = typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var isReferenceType = typeSymbol.IsReferenceType;
        var specialType = typeSymbol.SpecialType;
        var fitsInline = IsInlineCandidate(typeSymbol);

        return new InlineTypeInfo
        {
            KindIndex = kindName,
            KindValue = kindValue,
            FullTypeName = fullName,
            ClrTypeName = clrName,
            IsReferenceType = isReferenceType,
            SpecialType = specialType,
            FitsInline = fitsInline,
            TypeSymbol = typeSymbol
        };
    }

    private static bool IsInlineCandidate(ITypeSymbol type)
    {
        if (type.IsReferenceType) return false;
        if (type.SpecialType == SpecialType.None) return false;
        var full = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return !full.Contains("global::System.Decimal");
    }

    private static string GenerateKindName(ITypeSymbol type)
    {
        var full = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return full switch
        {
            "global::System.Byte" => "Byte",
            "global::System.SByte" => "SByte",
            "global::System.Int16" => "Int16",
            "global::System.UInt16" => "UInt16",
            "global::System.Int32" => "Int32",
            "global::System.UInt32" => "UInt32",
            "global::System.Int64" => "Int64",
            "global::System.UInt64" => "UInt64",
            "global::System.Single" => "Single",
            "global::System.Double" => "Double",
            "global::System.Boolean" => "Boolean",
            "global::System.Char" => "Char",
            "global::System.String" => "String",
            _ => SanitizeKindName(type.Name)
        };
    }

    private static string SanitizeKindName(string name) =>
        name.Replace(".", "_").Replace("<", "").Replace(">", "").Replace("?", "").Replace("[]", "Array");

    private static void GenerateTypedDataExtensions(SourceProductionContext context, GenerationInput input)
    {
        var allTypes = input.TypeGroups.SelectMany(g => g.Types).ToList();
        if (allTypes.Count == 0) return;

        if (input.IsHome)
            GenerateHomeAssembly(context, input.TypeGroups, allTypes);
        else
            GenerateAdapterAssembly(context, input.TypeGroups, allTypes);
    }

    // ─── Home assembly ─────────────────────────────────────────────

    private static void GenerateHomeAssembly(SourceProductionContext context,
        List<TypeGroup> groups, List<InlineTypeInfo> allTypes)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS0105");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace Origo.Core.Snd.Metadata;");
        sb.AppendLine();

        GenerateKindMap(sb, allTypes);
        sb.AppendLine();
        GenerateHomeKindRegistration(sb, allTypes);
        sb.AppendLine();
        GenerateStringConversion(sb);
        sb.AppendLine();
        GenerateAsMethods(sb, allTypes);
        sb.AppendLine();
        GenerateTryGetMethods(sb, allTypes);
        sb.AppendLine();
        GenerateImplicitConversions(sb, allTypes);
        sb.AppendLine();
        GenerateTypedDataTypeMap(sb, allTypes);
        sb.AppendLine();
        GenerateTypedDataObjectConverter(sb, allTypes);
        sb.AppendLine();
        GenerateTypedDataFactory(sb, allTypes);

        context.AddSource("TypedData.g.cs", sb.ToString());
    }

    // ─── Adapter assembly ──────────────────────────────────────────

    private static void GenerateAdapterAssembly(SourceProductionContext context,
        List<TypeGroup> groups, List<InlineTypeInfo> allTypes)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS0105");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Origo.Core.Snd.Metadata;");
        sb.AppendLine();

        GenerateAdapterExtensionMethods(sb, allTypes);
        sb.AppendLine();
        GenerateAdapterKindRegistration(sb, allTypes);
        sb.AppendLine();
        GenerateAdapterConverterRegistration(sb, allTypes);
        sb.AppendLine();
        GenerateAdapterTypeMapRegistration(sb, allTypes);

        context.AddSource("TypedData.g.cs", sb.ToString());
    }

    // ─── KindMap ───────────────────────────────────────────────────

    private static void GenerateKindMap(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");
        sb.AppendLine("    internal static class KindMap");
        sb.AppendLine("    {");
        sb.AppendLine("        public const byte Null = 0;");

        foreach (var t in types)
        {
            sb.AppendLine($"        public const byte {t.KindIndex} = {t.KindValue};");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    // ─── Home: ModuleInitializer for kind registration ─────────────

    private static void GenerateHomeKindRegistration(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("internal static class TypedDataHomeKindRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        TypedData.RegisterKind(0, typeof(object));");

        foreach (var t in types)
        {
            sb.AppendLine($"        TypedData.RegisterKind({t.KindValue}, typeof({t.ClrTypeName}));");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    // ─── Adapter: ModuleInitializer for kind registration ──────────

    private static void GenerateAdapterKindRegistration(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("internal static class TypedDataAdapterKindRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");

        foreach (var t in types)
        {
            sb.AppendLine($"        TypedData.RegisterKind({t.KindValue}, typeof({t.ClrTypeName}));");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    // ─── Adapter: converter fallback registration ──────────────────

    private static void GenerateAdapterConverterRegistration(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("internal static class TypedDataAdapterConverterRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        TypedDataLayeredRegistry.RegisterFromObjectFallback(AdapterFromObject);");
        sb.AppendLine("        TypedDataLayeredRegistry.RegisterToObjectFallback(AdapterToObject);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static (long inlineBits, object? refValue)? AdapterFromObject(byte kind, object value)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (kind)");
        sb.AppendLine("        {");

        foreach (var t in types)
        {
            if (t.IsReferenceType)
            {
                sb.AppendLine($"            case {t.KindValue}: return (0, value);");
            }
            else if (t.FitsInline)
            {
                var castExpr = GetNonSystemCastForBitsExpr(t);
                sb.AppendLine($"            case {t.KindValue}: return (BitsFrom{GetShortTypeName(t)}(({t.ClrTypeName})value), null);");
            }
            else
            {
                sb.AppendLine($"            case {t.KindValue}: return (0, value);");
            }
        }

        sb.AppendLine("            default: return null;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static object? AdapterToObject(TypedData td)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (td._kind)");
        sb.AppendLine("        {");

        foreach (var t in types)
        {
            if (t.IsReferenceType)
            {
                sb.AppendLine($"            case {t.KindValue}: return td._ref;");
            }
            else if (t.FitsInline)
            {
                sb.AppendLine($"            case {t.KindValue}: return Read{GetShortTypeName(t)}(td._inlineBits);");
            }
            else
            {
                sb.AppendLine($"            case {t.KindValue}: return td._ref;");
            }
        }

        sb.AppendLine("            default: return null;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        foreach (var t in types.Where(t => t.FitsInline))
        {
            sb.AppendLine();
            sb.AppendLine($"    private static long BitsFrom{GetShortTypeName(t)}({t.ClrTypeName} value)");
            sb.AppendLine("    {");
            var bitExpr = GetNonSystemBitsExpr(t);
            sb.AppendLine($"        {bitExpr}");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    private static {t.ClrTypeName} Read{GetShortTypeName(t)}(long bits)");
            sb.AppendLine("    {");
            var readExpr = GetNonSystemReadExpr(t);
            sb.AppendLine($"        {readExpr}");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
    }

    private static string GetShortTypeName(InlineTypeInfo t)
    {
        return SanitizeKindName(t.KindIndex ?? "Unknown");
    }

    private static string GetNonSystemCastForBitsExpr(InlineTypeInfo t)
    {
        return $"BitsFrom{GetShortTypeName(t)}(({t.ClrTypeName})value)";
    }

    private static string GetNonSystemBitsExpr(InlineTypeInfo t)
    {
        return "return 0;";
    }

    private static string GetNonSystemReadExpr(InlineTypeInfo t)
    {
        return "return default;";
    }

    // ─── Adapter: TypeMap registration ─────────────────────────────

    private static void GenerateAdapterTypeMapRegistration(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("internal static class TypedDataAdapterTypeMapRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        TypedDataLayeredRegistry.RegisterKindResolver(ResolveKind);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    private static byte ResolveKind(Type type)");
        sb.AppendLine("    {");

        foreach (var t in types)
        {
            sb.AppendLine($"        if (type == typeof({t.ClrTypeName})) return {t.KindValue};");
        }

        sb.AppendLine("        return 0;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    // ─── String conversion ─────────────────────────────────────────

    private static void GenerateStringConversion(StringBuilder sb)
    {
        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public string? AsString() => (string?)_ref;");
        sb.AppendLine();
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public bool TryGetString(out string? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_kind == KindMap.String) { value = (string?)_ref; return true; }");
        sb.AppendLine("        value = null; return false;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    // ─── Home: AsMethods ───────────────────────────────────────────

    private static void GenerateAsMethods(StringBuilder sb, List<InlineTypeInfo> types)
    {
        var inlineTypes = types.Where(t => t.FitsInline && !t.IsReferenceType).ToList();
        if (inlineTypes.Count == 0) return;

        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");

        foreach (var t in inlineTypes)
        {
            var returnType = t.ClrTypeName;
            var methodName = $"As{t.KindIndex}";

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    internal {returnType} {methodName}()");
            sb.AppendLine("    {");

            if (t.SpecialType == SpecialType.System_Single)
            {
                sb.AppendLine("        return BitConverter.Int32BitsToSingle((int)_inlineBits);");
            }
            else if (t.SpecialType == SpecialType.System_Double)
            {
                sb.AppendLine("        return BitConverter.Int64BitsToDouble(_inlineBits);");
            }
            else if (t.SpecialType == SpecialType.System_Boolean)
            {
                sb.AppendLine("        return _inlineBits != 0;");
            }
            else if (t.SpecialType == SpecialType.System_Char)
            {
                sb.AppendLine("        return (char)(ushort)_inlineBits;");
            }
            else
            {
                sb.AppendLine($"        return ({returnType})_inlineBits;");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    // ─── Home: TryGetMethods ───────────────────────────────────────

    private static void GenerateTryGetMethods(StringBuilder sb, List<InlineTypeInfo> types)
    {
        var inlineTypes = types.Where(t => t.FitsInline && !t.IsReferenceType).ToList();
        if (inlineTypes.Count == 0) return;

        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");

        foreach (var t in inlineTypes)
        {
            var returnType = t.ClrTypeName;
            var methodName = $"TryGet{t.KindIndex}";

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public bool {methodName}(out {returnType} value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (_kind == KindMap.{t.KindIndex})");
            sb.AppendLine("        {");

            if (t.SpecialType == SpecialType.System_Single)
            {
                sb.AppendLine("            value = BitConverter.Int32BitsToSingle((int)_inlineBits);");
            }
            else if (t.SpecialType == SpecialType.System_Double)
            {
                sb.AppendLine("            value = BitConverter.Int64BitsToDouble(_inlineBits);");
            }
            else if (t.SpecialType == SpecialType.System_Boolean)
            {
                sb.AppendLine("            value = _inlineBits != 0;");
            }
            else if (t.SpecialType == SpecialType.System_Char)
            {
                sb.AppendLine("            value = (char)(ushort)_inlineBits;");
            }
            else
            {
                sb.AppendLine($"            value = ({returnType})_inlineBits;");
            }

            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine($"        value = default;");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    // ─── Adapter: Extension methods ────────────────────────────────

    private static void GenerateAdapterExtensionMethods(StringBuilder sb, List<InlineTypeInfo> types)
    {
        var inlineTypes = types.Where(t => t.FitsInline && !t.IsReferenceType).ToList();
        var refTypes = types.Where(t => t.IsReferenceType).ToList();
        var largeValueTypes = types.Where(t => !t.FitsInline && !t.IsReferenceType).ToList();

        if (inlineTypes.Count == 0 && refTypes.Count == 0 && largeValueTypes.Count == 0) return;

        sb.AppendLine("public static class TypedDataLayeredExtensions");
        sb.AppendLine("{");

        foreach (var t in inlineTypes)
        {
            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static {t.ClrTypeName} As{t.KindIndex}(this TypedData td)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return ReadBitsAs{GetShortTypeName(t)}(td._inlineBits);");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static bool TryGet{t.KindIndex}(this TypedData td, out {t.ClrTypeName} value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (td._kind == {t.KindValue})");
            sb.AppendLine("        {");
            sb.AppendLine($"            value = ReadBitsAs{GetShortTypeName(t)}(td._inlineBits);");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine("        value = default;");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var t in refTypes)
        {
            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static {t.ClrTypeName}? As{t.KindIndex}(this TypedData td)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return ({t.ClrTypeName}?)td._ref;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static bool TryGet{t.KindIndex}(this TypedData td, out {t.ClrTypeName}? value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (td._kind == {t.KindValue})");
            sb.AppendLine("        {");
            sb.AppendLine($"            value = ({t.ClrTypeName}?)td._ref;");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine("        value = default;");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var t in largeValueTypes)
        {
            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static {t.ClrTypeName} As{t.KindIndex}(this TypedData td)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return ({t.ClrTypeName})td._ref!;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static bool TryGet{t.KindIndex}(this TypedData td, out {t.ClrTypeName} value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (td._kind == {t.KindValue} && td._ref is {t.ClrTypeName} v)");
            sb.AppendLine("        {");
            sb.AppendLine("            value = v;");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine("        value = default;");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        GenerateInlineBitHelpers(sb, inlineTypes);

        sb.AppendLine("}");
    }

    private static void GenerateInlineBitHelpers(StringBuilder sb, List<InlineTypeInfo> types)
    {
        foreach (var t in types)
        {
            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    private static {t.ClrTypeName} ReadBitsAs{GetShortTypeName(t)}(long bits)");
            sb.AppendLine("    {");
            sb.AppendLine("        return default;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    // ─── Home: Implicit conversions ────────────────────────────────

    private static void GenerateImplicitConversions(StringBuilder sb, List<InlineTypeInfo> types)
    {
        var inlineTypes = types.Where(t => t.FitsInline && IsSystemType(t)).ToList();
        if (inlineTypes.Count == 0) return;

        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");

        foreach (var t in inlineTypes)
        {
            var typeName = t.ClrTypeName;
            var kindValue = t.KindValue;

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static explicit operator TypedData({typeName} value)");
            sb.AppendLine("    {");

            if (t.SpecialType == SpecialType.System_String)
            {
                sb.AppendLine($"        return new TypedData(KindMap.{t.KindIndex}, 0, value);");
            }
            else if (t.SpecialType == SpecialType.System_Single)
            {
                sb.AppendLine($"        return new TypedData(KindMap.{t.KindIndex}, BitConverter.SingleToInt32Bits(value), null);");
            }
            else if (t.SpecialType == SpecialType.System_Double)
            {
                sb.AppendLine($"        return new TypedData(KindMap.{t.KindIndex}, BitConverter.DoubleToInt64Bits(value), null);");
            }
            else if (t.SpecialType == SpecialType.System_Boolean)
            {
                sb.AppendLine($"        return new TypedData(KindMap.{t.KindIndex}, value ? 1 : 0, null);");
            }
            else if (t.SpecialType == SpecialType.System_Char)
            {
                sb.AppendLine($"        return new TypedData(KindMap.{t.KindIndex}, value, null);");
            }
            else if (t.SpecialType == SpecialType.System_UInt32 || t.SpecialType == SpecialType.System_UInt64)
            {
                sb.AppendLine($"        return new TypedData(KindMap.{t.KindIndex}, (long)value, null);");
            }
            else
            {
                sb.AppendLine($"        return new TypedData(KindMap.{t.KindIndex}, value, null);");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    private static bool IsSystemType(InlineTypeInfo t)
    {
        return t.SpecialType != SpecialType.None;
    }

    // ─── TypedDataTypeMap ──────────────────────────────────────────

    private static void GenerateTypedDataTypeMap(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("internal static class TypedDataTypeMap");
        sb.AppendLine("{");

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public static byte GetKindForType(Type type)");
        sb.AppendLine("    {");

        foreach (var t in types)
        {
            sb.AppendLine($"        if (type == typeof({t.ClrTypeName})) return {t.KindValue};");
        }

        sb.AppendLine("        var kind = TypedDataLayeredRegistry.ResolveKind(type);");
        sb.AppendLine("        if (kind != 0) return kind;");
        sb.AppendLine("        return 0;");
        sb.AppendLine("    }");

        sb.AppendLine("}");
    }

    // ─── TypedDataObjectConverter ──────────────────────────────────

    private static void GenerateTypedDataObjectConverter(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("internal static class TypedDataObjectConverter");
        sb.AppendLine("{");

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public static object? ToObject(TypedData td)");
        sb.AppendLine("    {");

        sb.AppendLine("        switch (td._kind)");
        sb.AppendLine("        {");

        sb.AppendLine("            case 0: return null;");

        foreach (var t in types)
        {
            if (t.IsReferenceType)
            {
                sb.AppendLine($"            case {t.KindValue}: return td._ref;");
            }
            else if (t.FitsInline && IsSystemType(t))
            {
                sb.AppendLine($"            case {t.KindValue}: return td.As{t.KindIndex}();");
            }
            else if (t.FitsInline)
            {
                sb.AppendLine($"            case {t.KindValue}: return td.As{t.KindIndex}();");
            }
            else
            {
                sb.AppendLine($"            case {t.KindValue}: return td._ref;");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("        var obj = TypedDataLayeredRegistry.ResolveToObject(td);");
        sb.AppendLine("        if (obj is not null) return obj;");
        sb.AppendLine("        return td._ref;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public static (long inlineBits, object? refValue) FromObject(byte kind, object value)");
        sb.AppendLine("    {");

        sb.AppendLine("        switch (kind)");
        sb.AppendLine("        {");

        foreach (var t in types)
        {
            if (t.IsReferenceType)
            {
                sb.AppendLine($"            case {t.KindValue}: return (0, value);");
            }
            else if (t.FitsInline && IsSystemType(t))
            {
                if (t.SpecialType == SpecialType.System_Single)
                {
                    sb.AppendLine($"            case {t.KindValue}: return (BitConverter.SingleToInt32Bits((float)value), null);");
                }
                else if (t.SpecialType == SpecialType.System_Double)
                {
                    sb.AppendLine($"            case {t.KindValue}: return (BitConverter.DoubleToInt64Bits((double)value), null);");
                }
                else if (t.SpecialType == SpecialType.System_Boolean)
                {
                    sb.AppendLine($"            case {t.KindValue}: return ((bool)value ? 1 : 0, null);");
                }
                else
                {
                    var castExpr = GetFromObjectCastExpr(t);
                    sb.AppendLine($"            case {t.KindValue}: return ({castExpr}, null);");
                }
            }
            else if (t.FitsInline)
            {
                sb.AppendLine($"            case {t.KindValue}: return (Pack{GetShortTypeName(t)}(({t.ClrTypeName})value), null);");
            }
            else
            {
                sb.AppendLine($"            case {t.KindValue}: return (0, value);");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("        var result = TypedDataLayeredRegistry.ResolveFromObject(kind, value);");
        sb.AppendLine("        if (result.HasValue) return result.Value;");
        sb.AppendLine("        return (0, value);");
        sb.AppendLine("    }");

        foreach (var t in types.Where(t => t.FitsInline && !IsSystemType(t)))
        {
            sb.AppendLine();
            sb.AppendLine($"    private static long Pack{GetShortTypeName(t)}({t.ClrTypeName} value)");
            sb.AppendLine("    {");
            sb.AppendLine("        return 0;");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
    }

    // ─── TypedDataFactory ─────────────────────────────────────────

    private static void GenerateTypedDataFactory(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("internal static class TypedDataFactory<T>");
        sb.AppendLine("{");

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public static TypedData Create(T value)");
        sb.AppendLine("    {");

        foreach (var t in types.Where(t => IsSystemType(t)))
        {
            var clrName = t.ClrTypeName;

            sb.AppendLine($"        if (typeof(T) == typeof({clrName}))");
            sb.AppendLine("        {");

            if (t.IsReferenceType)
            {
                sb.AppendLine($"            return new TypedData({t.KindValue}, 0, value);");
            }
            else if (t.FitsInline)
            {
                var localType = GetFactoryLocalType(t);
                var extractExpr = GetFactoryExtractExpr(t);
                sb.AppendLine($"            {localType} local = {extractExpr};");
                var bitsExpr = GetFactoryBitsExpr(t);
                sb.AppendLine($"            return new TypedData({t.KindValue}, {bitsExpr}, null);");
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

        foreach (var t in types.Where(t => IsSystemType(t)))
        {
            var clrName = t.ClrTypeName;

            sb.AppendLine($"        if (typeof(T) == typeof({clrName}) && source._kind == {t.KindValue})");
            sb.AppendLine("        {");

            if (t.IsReferenceType)
            {
                sb.AppendLine("            if (source._ref is T t) { value = t; return true; }");
            }
            else if (t.FitsInline)
            {
                var localType = GetFactoryLocalType(t);
                var readExpr = GetFactoryReadFromBitsExpr(t);
                sb.AppendLine($"            {localType} local = {readExpr};");
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

    // ─── Helper: factory local type ────────────────────────────────

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

    private static string GetFromObjectCastExpr(InlineTypeInfo t)
    {
        return t.SpecialType switch
        {
            SpecialType.System_Byte => "(long)(byte)value",
            SpecialType.System_SByte => "(long)(sbyte)value",
            SpecialType.System_Int16 => "(long)(short)value",
            SpecialType.System_UInt16 => "(long)(ushort)value",
            SpecialType.System_Int32 => "(long)(int)value",
            SpecialType.System_UInt32 => "(long)(uint)value",
            SpecialType.System_Int64 => "(long)value",
            SpecialType.System_UInt64 => "(long)(ulong)value",
            SpecialType.System_Char => "(long)(char)value",
            _ => "(long)value"
        };
    }

    // ─── Data types ────────────────────────────────────────────────

    private sealed class GenerationInput
    {
        public bool IsHome { get; set; }
        public List<TypeGroup> TypeGroups { get; set; } = new();
    }

    private sealed class TypeGroup
    {
        public int StartKind { get; set; }
        public List<InlineTypeInfo> Types { get; set; } = new();
    }

    private sealed class InlineTypeInfo
    {
        public string? KindIndex { get; set; }
        public byte KindValue { get; set; }
        public string FullTypeName { get; set; } = string.Empty;
        public string ClrTypeName { get; set; } = string.Empty;
        public bool IsReferenceType { get; set; }
        public SpecialType SpecialType { get; set; }
        public bool FitsInline { get; set; }
        public ITypeSymbol TypeSymbol { get; set; } = null!;
    }
}
