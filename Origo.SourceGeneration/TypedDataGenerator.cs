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

    private static readonly DiagnosticDescriptor SystemPrimitiveInAdapter = new(
        id: "ORIGOSG001",
        title: "System primitive registered outside the TypedData home assembly",
        messageFormat:
        "'{0}' is a system primitive and can only be registered as an inline TypedData type in the Origo.Core (home) assembly. "
        + "Adapter assemblies may register only reference types or non-system value types, which are stored through the _ref slot.",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedHomeValueType = new(
        id: "ORIGOSG002",
        title: "Unsupported value type in the TypedData home assembly",
        messageFormat:
        "'{0}' is a value type that cannot be stored inline and is not supported in the Origo.Core (home) assembly. "
        + "Only the supported system primitives may be registered as home inline types.",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor KindOverflow = new(
        id: "ORIGOSG003",
        title: "TypedData kind byte out of range",
        messageFormat:
        "'{0}' resolves to kind {1}, which is outside the valid byte range [1, 255]. " +
        "Each registered type's kind is startKind plus its position in the SndInlineTypes group; "
        + "keep startKind plus the type count within [1, 255].",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor KindCollision = new(
        id: "ORIGOSG004",
        title: "TypedData kind collision",
        messageFormat:
        "Kind {0} is assigned to multiple types ({1}). "
        + "Each registered inline TypedData type must map to a unique kind byte; "
        + "adjust the SndInlineTypes startKind offsets so the kind ranges do not overlap.",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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
        var kindOffset = 0;

        foreach (var ctorArg in attr.ConstructorArguments)
        {
            if (ctorArg.Kind == TypedConstantKind.Array)
            {
                foreach (var element in ctorArg.Values)
                {
                    if (element.Value is INamedTypeSymbol typeSymbol)
                        result.Add(CreateTypeInfo(typeSymbol, startKind + kindOffset++));
                    else if (element.Value is ITypeSymbol ts)
                        result.Add(CreateTypeInfo(ts, startKind + kindOffset++));
                }
            }
            else if (ctorArg.Value is INamedTypeSymbol singleType)
            {
                result.Add(CreateTypeInfo(singleType, startKind + kindOffset++));
            }
        }

        return result;
    }

    private static InlineTypeInfo CreateTypeInfo(ITypeSymbol typeSymbol, int rawKind)
    {
        var kindName = GenerateKindName(typeSymbol);
        var clrName = typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        return new InlineTypeInfo
        {
            KindIndex = kindName,
            RawKind = rawKind,
            KindValue = rawKind is > 0 and <= 255 ? (byte)rawKind : (byte)0,
            ClrTypeName = clrName,
            IsReferenceType = typeSymbol.IsReferenceType,
            SpecialType = typeSymbol.SpecialType,
            FitsInline = IsInlineCandidate(typeSymbol)
        };
    }

    // A type is inline-eligible only when it is one of the system primitive value
    // types that fit in eight bytes. Inline storage is exclusively for these types
    // in the home assembly; every other registered type (reference types such as
    // string, and non-system value types such as decimal or engine structs) is
    // stored through the _ref slot.
    private static bool IsInlineCandidate(ITypeSymbol type)
    {
        if (type.IsReferenceType) return false;
        return type.SpecialType switch
        {
            SpecialType.System_Byte
                or SpecialType.System_SByte
                or SpecialType.System_Int16
                or SpecialType.System_UInt16
                or SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64
                or SpecialType.System_Single
                or SpecialType.System_Double
                or SpecialType.System_Boolean
                or SpecialType.System_Char => true,
            _ => false
        };
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

        var validTypes = ValidateAndFilter(context, input.IsHome, allTypes);
        validTypes = RejectKindCollisions(context, validTypes);
        if (validTypes.Count == 0) return;

        if (input.IsHome)
            GenerateHomeAssembly(context, validTypes);
        else
            GenerateAdapterAssembly(context, validTypes);
    }

    // Enforces the storage model with fail-fast diagnostics, then returns the
    // types that can be generated. Invalid registrations are reported as build
    // errors and excluded from generation so the emitted source stays compilable
    // (the reported error fails the build regardless).
    private static List<InlineTypeInfo> ValidateAndFilter(
        SourceProductionContext context, bool isHome, List<InlineTypeInfo> allTypes)
    {
        var valid = new List<InlineTypeInfo>();

        foreach (var t in allTypes)
        {
            if (t.RawKind is <= 0 or > 255)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(KindOverflow, Location.None, t.ClrTypeName, t.RawKind));
                continue;
            }

            if (t.IsReferenceType)
            {
                valid.Add(t);
                continue;
            }

            if (t.FitsInline)
            {
                if (isHome)
                    valid.Add(t);
                else
                    context.ReportDiagnostic(
                        Diagnostic.Create(SystemPrimitiveInAdapter, Location.None, t.ClrTypeName));
            }
            else
            {
                if (isHome)
                    context.ReportDiagnostic(
                        Diagnostic.Create(UnsupportedHomeValueType, Location.None, t.ClrTypeName));
                else
                    valid.Add(t);
            }
        }

        return valid;
    }

    // Rejects types whose kind byte collides with another distinct type. A kind
    // must map to exactly one type; overlapping SndInlineTypes startKind ranges
    // are reported as ORIGOSG004 and every type on a colliding kind is dropped so
    // the emitted source stays compilable (the reported error fails the build).
    private static List<InlineTypeInfo> RejectKindCollisions(
        SourceProductionContext context, List<InlineTypeInfo> types)
    {
        var firstTypeByKind = new Dictionary<byte, string>();
        var collidingKinds = new HashSet<byte>();

        foreach (var t in types)
        {
            if (firstTypeByKind.TryGetValue(t.KindValue, out var existing))
            {
                if (existing != t.ClrTypeName)
                    collidingKinds.Add(t.KindValue);
            }
            else
            {
                firstTypeByKind[t.KindValue] = t.ClrTypeName;
            }
        }

        foreach (var kind in collidingKinds)
        {
            var names = types
                .Where(t => t.KindValue == kind)
                .Select(t => t.ClrTypeName)
                .Distinct();
            context.ReportDiagnostic(Diagnostic.Create(
                KindCollision, Location.None, kind, string.Join(", ", names)));
        }

        var result = new List<InlineTypeInfo>();
        var emitted = new HashSet<byte>();
        foreach (var t in types)
        {
            if (collidingKinds.Contains(t.KindValue)) continue;
            if (!emitted.Add(t.KindValue)) continue;
            result.Add(t);
        }

        return result;
    }

    // ─── Home assembly ─────────────────────────────────────────────

    private static void GenerateHomeAssembly(SourceProductionContext context, List<InlineTypeInfo> types)
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

        GenerateKindMap(sb, types);
        sb.AppendLine();
        GenerateHomeKindRegistration(sb, types);
        sb.AppendLine();
        GenerateStringConversion(sb);
        sb.AppendLine();
        GenerateIsProperties(sb, types);
        sb.AppendLine();
        GenerateAsMethods(sb, types);
        sb.AppendLine();
        GenerateTryGetMethods(sb, types);
        sb.AppendLine();
        GenerateImplicitConversions(sb, types);
        sb.AppendLine();
        GenerateTypedDataTypeMap(sb, types);
        sb.AppendLine();
        GenerateTypedDataObjectConverter(sb, types);
        sb.AppendLine();
        GenerateTypedDataFactory(sb, types);

        context.AddSource("TypedData.g.cs", sb.ToString());
    }

    // ─── Adapter assembly ──────────────────────────────────────────

    private static void GenerateAdapterAssembly(SourceProductionContext context, List<InlineTypeInfo> types)
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

        GenerateAdapterExtensionMethods(sb, types);
        sb.AppendLine();
        GenerateAdapterKindRegistration(sb, types);
        sb.AppendLine();
        GenerateAdapterConverterRegistration(sb, types);
        sb.AppendLine();
        GenerateAdapterTypeMapRegistration(sb, types);

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
    // Adapter types are always stored through the _ref slot, so both directions
    // are a straight passthrough of the boxed value.

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
            sb.AppendLine($"            case {t.KindValue}: return (0, value);");
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
            sb.AppendLine($"            case {t.KindValue}: return td._ref;");
        }

        sb.AppendLine("            default: return null;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
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
        sb.AppendLine("    internal readonly bool IsString => _kind == KindMap.String;");
        sb.AppendLine();
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public readonly string? AsString() => (string?)_ref;");
        sb.AppendLine();
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public readonly bool TryGetString(out string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_kind == KindMap.String) { value = (string)_ref!; return true; }");
        sb.AppendLine("        value = null!; return false;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    // ─── Home: IsProperties ─────────────────────────────────────────

    private static void GenerateIsProperties(StringBuilder sb, List<InlineTypeInfo> types)
    {
        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");

        foreach (var t in types)
        {
            if (t.IsReferenceType) continue;
            sb.AppendLine($"    internal readonly bool Is{t.KindIndex} => _kind == KindMap.{t.KindIndex};");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    // ─── Home: AsMethods ───────────────────────────────────────────

    private static void GenerateAsMethods(StringBuilder sb, List<InlineTypeInfo> types)
    {
        var inlineTypes = types.Where(t => t.FitsInline).ToList();
        if (inlineTypes.Count == 0) return;

        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");

        foreach (var t in inlineTypes)
        {
            var returnType = t.ClrTypeName;
            var methodName = $"As{t.KindIndex}";

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    internal readonly {returnType} {methodName}()");
            sb.AppendLine("    {");
            sb.AppendLine($"        {ReadInlineBitsExpr(t)}");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    // ─── Home: TryGetMethods ───────────────────────────────────────

    private static void GenerateTryGetMethods(StringBuilder sb, List<InlineTypeInfo> types)
    {
        var inlineTypes = types.Where(t => t.FitsInline).ToList();
        if (inlineTypes.Count == 0) return;

        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");

        foreach (var t in inlineTypes)
        {
            var returnType = t.ClrTypeName;
            var methodName = $"TryGet{t.KindIndex}";

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public readonly bool {methodName}(out {returnType} value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (_kind == KindMap.{t.KindIndex})");
            sb.AppendLine("        {");
            sb.AppendLine($"            value = {ReadInlineBitsValueExpr(t)};");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine($"        value = default;");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    private static string ReadInlineBitsExpr(InlineTypeInfo t) =>
        $"return {ReadInlineBitsValueExpr(t)};";

    private static string ReadInlineBitsValueExpr(InlineTypeInfo t)
    {
        return t.SpecialType switch
        {
            SpecialType.System_Single => "BitConverter.Int32BitsToSingle((int)_inlineBits)",
            SpecialType.System_Double => "BitConverter.Int64BitsToDouble(_inlineBits)",
            SpecialType.System_Boolean => "_inlineBits != 0",
            SpecialType.System_Char => "(char)(ushort)_inlineBits",
            _ => $"({t.ClrTypeName})_inlineBits"
        };
    }

    // ─── Adapter: Extension methods ────────────────────────────────
    // Every adapter type is stored through the _ref slot.

    private static void GenerateAdapterExtensionMethods(StringBuilder sb, List<InlineTypeInfo> types)
    {
        var refTypes = types.Where(t => t.IsReferenceType).ToList();
        var valueTypes = types.Where(t => !t.IsReferenceType).ToList();

        if (refTypes.Count == 0 && valueTypes.Count == 0) return;

        sb.AppendLine("public static class TypedDataLayeredExtensions");
        sb.AppendLine("{");

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

        foreach (var t in valueTypes)
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

        sb.AppendLine("}");
    }

    // ─── Home: Implicit conversions ────────────────────────────────

    private static void GenerateImplicitConversions(StringBuilder sb, List<InlineTypeInfo> types)
    {
        var inlineTypes = types.Where(t => t.FitsInline).ToList();
        if (inlineTypes.Count == 0) return;

        sb.AppendLine("partial struct TypedData");
        sb.AppendLine("{");

        foreach (var t in inlineTypes)
        {
            var typeName = t.ClrTypeName;

            sb.AppendLine($"    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"    public static explicit operator TypedData({typeName} value)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return new TypedData(KindMap.{t.KindIndex}, {PackInlineBitsExpr(t, "value")}, null);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    private static string PackInlineBitsExpr(InlineTypeInfo t, string operand)
    {
        return t.SpecialType switch
        {
            SpecialType.System_Single => $"BitConverter.SingleToInt32Bits({operand})",
            SpecialType.System_Double => $"BitConverter.DoubleToInt64Bits({operand})",
            SpecialType.System_Boolean => $"{operand} ? 1 : 0",
            SpecialType.System_UInt32 or SpecialType.System_UInt64 => $"(long){operand}",
            _ => operand
        };
    }

    // Packs a boxed object into the inline bits, unboxing to the concrete type
    // first. Used by FromObject where the input is typed as object.
    private static string FromObjectBitsExpr(InlineTypeInfo t)
    {
        return t.SpecialType switch
        {
            SpecialType.System_Single => "BitConverter.SingleToInt32Bits((float)value)",
            SpecialType.System_Double => "BitConverter.DoubleToInt64Bits((double)value)",
            SpecialType.System_Boolean => "(bool)value ? 1 : 0",
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
                sb.AppendLine($"            case {t.KindValue}: return td._ref;");
            else
                sb.AppendLine($"            case {t.KindValue}: return td.As{t.KindIndex}();");
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
                sb.AppendLine($"            case {t.KindValue}: return (0, value);");
            else
                sb.AppendLine($"            case {t.KindValue}: return ({FromObjectBitsExpr(t)}, null);");
        }

        sb.AppendLine("        }");
        sb.AppendLine("        var result = TypedDataLayeredRegistry.ResolveFromObject(kind, value);");
        sb.AppendLine("        if (result.HasValue) return result.Value;");
        sb.AppendLine("        return (0, value);");
        sb.AppendLine("    }");

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
        public int RawKind { get; set; }
        public byte KindValue { get; set; }
        public string ClrTypeName { get; set; } = string.Empty;
        public bool IsReferenceType { get; set; }
        public SpecialType SpecialType { get; set; }
        public bool FitsInline { get; set; }
    }
}
