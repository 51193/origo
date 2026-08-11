using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Origo.SourceGeneration;

/// <summary>
///     Incremental source generator that emits TypedData inline storage
///     members for every type declared via
///     <c>Origo.Core.Snd.Metadata.SndInlineTypesAttribute</c>: the kind map,
///     zero-boxing accessors, the generic factory, and — for adapter
///     assemblies — layered kind/converter/type-map registration.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class TypedDataGenerator : IIncrementalGenerator
{
    private const string _attributeFullName = "Origo.Core.Snd.Metadata.SndInlineTypesAttribute";
    private const string _typedDataFullName = "Origo.Core.Snd.Metadata.TypedData";

    /// <summary>
    ///     Registers the incremental pipeline: the generation input is derived
    ///     from the compilation's assembly attributes and compared by value,
    ///     so identical declaration sets over new compilations skip
    ///     regeneration entirely.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inputProvider = context.CompilationProvider
            .Select((compilation, ct) => ExtractGenerationInput(compilation))
            .WithComparer(GenerationInputEqualityComparer.Instance);

        context.RegisterSourceOutput(inputProvider, GenerateTypedDataExtensions);
    }

    private static GenerationInput ExtractGenerationInput(Compilation compilation)
    {
        var isHome = IsHomeAssembly(compilation);
        var typeGroups = new List<TypeGroup>();

        foreach (var attributeData in compilation.Assembly.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() != _attributeFullName)
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
        var typedDataSymbol = compilation.GetTypeByMetadataName(_typedDataFullName);
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
        var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

        foreach (var ctorArg in attr.ConstructorArguments)
        {
            if (ctorArg.Kind == TypedConstantKind.Array)
            {
                foreach (var element in ctorArg.Values)
                {
                    if (element.Value is ITypeSymbol typeSymbol)
                        result.Add(CreateTypeInfo(typeSymbol, startKind + kindOffset++, attrLocation));
                }
            }
        }

        return result;
    }

    private static InlineTypeInfo CreateTypeInfo(ITypeSymbol typeSymbol, int rawKind, Location location)
    {
        var kindName = GenerateKindName(typeSymbol);
        var clrName = typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        return new InlineTypeInfo
        {
            KindIndex = kindName,
            RawKind = rawKind,
            KindValue = rawKind is > 0 and <= 254 ? (byte)rawKind : (byte)0,
            ClrTypeName = clrName,
            IsReferenceType = typeSymbol.IsReferenceType,
            SpecialType = typeSymbol.SpecialType,
            FitsInline = IsInlineCandidate(typeSymbol),
            Location = location,
            LocationKey = location.SourceTree is null
                ? location.GetLineSpan().ToString()
                : $"{location.SourceTree.FilePath}:{location.SourceSpan.Start}-{location.SourceSpan.End}"
        };
    }

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
        validTypes = RejectKindNameCollisions(context, validTypes);
        validTypes = RejectInvalidKindNames(context, validTypes, input.IsHome);
        if (validTypes.Count == 0) return;

        if (input.IsHome)
            GenerateHomeAssembly(context, validTypes);
        else
            GenerateAdapterAssembly(context, validTypes);
    }

    private static List<InlineTypeInfo> ValidateAndFilter(
        SourceProductionContext context, bool isHome, List<InlineTypeInfo> allTypes)
    {
        var valid = new List<InlineTypeInfo>();

        foreach (var t in allTypes)
        {
            if (t.RawKind is <= 0 or > 254)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(_kindOverflow, t.Location ?? Location.None, t.ClrTypeName, t.RawKind));
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
                        Diagnostic.Create(_systemPrimitiveInAdapter, t.Location ?? Location.None, t.ClrTypeName));
            }
            else
            {
                if (isHome)
                    context.ReportDiagnostic(
                        Diagnostic.Create(_unsupportedHomeValueType, t.Location ?? Location.None, t.ClrTypeName));
                else
                    valid.Add(t);
            }
        }

        return valid;
    }

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
            var colliding = types
                .Where(t => t.KindValue == kind)
                .ToArray();
            var names = colliding
                .Select(t => t.ClrTypeName)
                .Distinct();
            context.ReportDiagnostic(Diagnostic.Create(
                _kindCollision, colliding[0].Location ?? Location.None, kind, string.Join(", ", names)));
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

    /// <summary>
    ///     Rejects types whose generated kind names collide. The kind name is
    ///     the identifier derived from the type name, so any two registered
    ///     types sharing that name — types of the same name from different
    ///     namespaces, generic instantiations whose names collapse to one
    ///     identifier, or the same type registered more than once (same or
    ///     different kinds) — would emit duplicate generated identifiers.
    ///     Numeric kind collisions are handled by
    ///     <see cref="RejectKindCollisions" />; this covers identifier-level
    ///     collisions that would otherwise emit uncompilable code. The KindMap
    ///     sentinel constant <c>Null = 0</c> (and the handwritten
    ///     <c>IsNull</c> property on value types) reserves the kind name
    ///     <c>Null</c>, so a registered type sanitizing to it is rejected too.
    /// </summary>
    private static List<InlineTypeInfo> RejectKindNameCollisions(
        SourceProductionContext context, List<InlineTypeInfo> types)
    {
        // KindMap always emits `public const byte Null = 0`, and TypedData
        // declares a handwritten `IsNull` property; a registered type named
        // Null would collide with the sentinel constant (and, for value
        // types, with the property), producing CS0102. Treat the reserved
        // identifier as an already-taken kind name.
        const string reservedKindName = "Null";
        var firstTypeByKindName = new Dictionary<string, InlineTypeInfo>(StringComparer.Ordinal);
        var collidingKindNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var t in types)
        {
            if (t.KindIndex is null) continue;
            if (string.Equals(t.KindIndex, reservedKindName, StringComparison.Ordinal)
                || firstTypeByKindName.ContainsKey(t.KindIndex))
                collidingKindNames.Add(t.KindIndex);
            else
                firstTypeByKindName[t.KindIndex] = t;
        }

        foreach (var kindName in collidingKindNames)
        {
            var colliding = types
                .Where(t => string.Equals(t.KindIndex, kindName, StringComparison.Ordinal))
                .ToArray();
            if (string.Equals(kindName, reservedKindName, StringComparison.Ordinal))
            {
                foreach (var t in colliding)
                    context.ReportDiagnostic(Diagnostic.Create(
                        _kindNameCollision, t.Location ?? Location.None,
                        t.ClrTypeName, "the reserved KindMap sentinel", kindName));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _kindNameCollision,
                    colliding[0].Location ?? Location.None,
                    colliding[0].ClrTypeName,
                    colliding[1].ClrTypeName,
                    kindName));
            }
        }

        var result = new List<InlineTypeInfo>();
        foreach (var t in types)
        {
            if (t.KindIndex is not null && collidingKindNames.Contains(t.KindIndex)) continue;
            result.Add(t);
        }

        return result;
    }

    /// <summary>
    ///     Home inline kind names whose generated accessor identifiers are
    ///     reserved, mapped to the BCL type each name belongs to. In adapter
    ///     mode a type that sanitizes to one of these names would generate a
    ///     public extension method that shadows the Home accessor semantics
    ///     for consumers (extension methods are only considered when no
    ///     applicable instance member exists, so a public <c>AsInt32</c>
    ///     extension would silently win for consumer code while Origo.Core's
    ///     internal calls still bind to the instance member): such
    ///     registrations are rejected — unless the registered type *is* the
    ///     BCL type the name belongs to (e.g. registering <c>string</c> under
    ///     a custom kind), whose accessor semantics are identical to the Home
    ///     member. In Home mode the BCL kinds are registered by the Home
    ///     attribute itself and same-assembly duplicates are already caught by
    ///     <see cref="RejectKindNameCollisions" />, so no extra gate applies.
    /// </summary>
    private static readonly Dictionary<string, SpecialType> _homeReservedKindNames = new()
    {
        ["Byte"] = SpecialType.System_Byte,
        ["SByte"] = SpecialType.System_SByte,
        ["Int16"] = SpecialType.System_Int16,
        ["UInt16"] = SpecialType.System_UInt16,
        ["Int32"] = SpecialType.System_Int32,
        ["UInt32"] = SpecialType.System_UInt32,
        ["Int64"] = SpecialType.System_Int64,
        ["UInt64"] = SpecialType.System_UInt64,
        ["Single"] = SpecialType.System_Single,
        ["Double"] = SpecialType.System_Double,
        ["Boolean"] = SpecialType.System_Boolean,
        ["Char"] = SpecialType.System_Char,
        ["String"] = SpecialType.System_String
    };

    /// <summary>
    ///     Rejects types whose sanitized kind names are not valid C#
    ///     identifiers (e.g. pointer types, whose <c>Name</c> contains
    ///     <c>*</c>, would emit accessor identifiers like <c>AsInt32*</c>),
    ///     and — in adapter mode — types whose kind names collide with the
    ///     Home assembly's reserved kind names (a *custom* type named like a
    ///     Home inline kind, e.g. the user's own <c>Int32</c>, diverges from
    ///     the Home accessor semantics; the BCL type itself does not).
    /// </summary>
    private static List<InlineTypeInfo> RejectInvalidKindNames(
        SourceProductionContext context, List<InlineTypeInfo> types, bool isHome)
    {
        var result = new List<InlineTypeInfo>();

        foreach (var t in types)
        {
            if (t.KindIndex is null)
            {
                result.Add(t);
                continue;
            }

            if (!SyntaxFacts.IsValidIdentifier(t.KindIndex))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _invalidKindName, t.Location ?? Location.None, t.ClrTypeName, t.KindIndex));
                continue;
            }

            if (!isHome
                && _homeReservedKindNames.TryGetValue(t.KindIndex, out var bclSpecialType)
                && t.SpecialType != bclSpecialType)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    _kindNameCollision, t.Location ?? Location.None,
                    t.ClrTypeName, "the Home assembly's reserved kind name", t.KindIndex));
                continue;
            }

            result.Add(t);
        }

        return result;
    }

    private sealed record GenerationInput
    {
        public bool IsHome { get; set; }
        public List<TypeGroup> TypeGroups { get; set; } = [];
    }

    /// <summary>
    ///     Value-based equality for <see cref="GenerationInput" />, so the
    ///     incremental pipeline can recognize an unchanged declaration set
    ///     (e.g. a new <see cref="Compilation" /> instance over identical
    ///     sources) and skip regeneration entirely.
    /// </summary>
    private sealed class GenerationInputEqualityComparer : IEqualityComparer<GenerationInput>
    {
        public static readonly GenerationInputEqualityComparer Instance = new();

        public bool Equals(GenerationInput? x, GenerationInput? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.IsHome != y.IsHome) return false;
            if (x.TypeGroups.Count != y.TypeGroups.Count) return false;

            for (var i = 0; i < x.TypeGroups.Count; i++)
            {
                var gx = x.TypeGroups[i];
                var gy = y.TypeGroups[i];
                if (gx.StartKind != gy.StartKind || gx.Types.Count != gy.Types.Count)
                    return false;

                for (var j = 0; j < gx.Types.Count; j++)
                {
                    var tx = gx.Types[j];
                    var ty = gy.Types[j];
                    if (tx.RawKind != ty.RawKind
                        || tx.KindValue != ty.KindValue
                        || !string.Equals(tx.ClrTypeName, ty.ClrTypeName, StringComparison.Ordinal)
                        || tx.IsReferenceType != ty.IsReferenceType
                        || tx.SpecialType != ty.SpecialType
                        || tx.FitsInline != ty.FitsInline
                        || !string.Equals(tx.LocationKey, ty.LocationKey, StringComparison.Ordinal))
                        return false;
                }
            }

            return true;
        }

        public int GetHashCode(GenerationInput obj)
        {
            var hash = new HashCode();
            hash.Add(obj.IsHome);
            hash.Add(obj.TypeGroups.Count);
            foreach (var g in obj.TypeGroups)
            {
                hash.Add(g.StartKind);
                hash.Add(g.Types.Count);
                foreach (var t in g.Types)
                {
                    hash.Add(t.RawKind);
                    hash.Add(t.KindValue);
                    hash.Add(t.ClrTypeName);
                    hash.Add(t.IsReferenceType);
                    hash.Add(t.SpecialType);
                    hash.Add(t.FitsInline);
                    hash.Add(t.LocationKey);
                }
            }

            return hash.ToHashCode();
        }
    }

    private sealed record TypeGroup
    {
        public int StartKind { get; set; }
        public List<InlineTypeInfo> Types { get; set; } = [];
    }

    private sealed record InlineTypeInfo
    {
        public string? KindIndex { get; set; }
        public int RawKind { get; set; }
        public byte KindValue { get; set; }
        public string ClrTypeName { get; set; } = string.Empty;
        public bool IsReferenceType { get; set; }
        public SpecialType SpecialType { get; set; }
        public bool FitsInline { get; set; }
        public Location? Location { get; set; }

        /// <summary>
        ///     Stable string key for <see cref="Location" />, used by the
        ///     generation-input comparer so moved-but-unchanged declarations
        ///     are still detected (the raw <see cref="Location" /> object has
        ///     no value equality across compilations).
        /// </summary>
        public string LocationKey { get; set; } = string.Empty;
    }

    /// <summary>
    ///     Single source of truth for the inline bit-packing expressions
    ///     emitted for each supported system primitive. Shared by the home
    ///     accessor/conversion generation and the factory generation so the
    ///     type-to-expression mapping exists exactly once.
    /// </summary>
    private static class InlineTypeExprs
    {
        /// <summary>Expression packing a value operand into <c>_inlineBits</c> storage form.</summary>
        public static string Pack(InlineTypeInfo t, string operand) =>
            t.SpecialType switch
            {
                SpecialType.System_Single => $"BitConverter.SingleToInt32Bits({operand})",
                SpecialType.System_Double => $"BitConverter.DoubleToInt64Bits({operand})",
                SpecialType.System_Boolean => $"{operand} ? 1 : 0",
                // Bit-pattern conversions: a consumer compiling with /checked
                // must not hit overflow checks on the reinterpretation.
                SpecialType.System_UInt32 or SpecialType.System_UInt64 => $"unchecked((long){operand})",
                _ => operand
            };

        /// <summary>Expression unpacking an <c>_inlineBits</c> operand back into the target type.</summary>
        public static string Unpack(InlineTypeInfo t, string bitsOperand) =>
            t.SpecialType switch
            {
                SpecialType.System_Single => $"BitConverter.Int32BitsToSingle((int){bitsOperand})",
                SpecialType.System_Double => $"BitConverter.Int64BitsToDouble({bitsOperand})",
                SpecialType.System_Boolean => $"{bitsOperand} != 0",
                SpecialType.System_Char => $"(char)(ushort){bitsOperand}",
                // Bit-pattern conversions: a consumer compiling with /checked
                // must not hit overflow checks on the reinterpretation.
                SpecialType.System_UInt32 => $"unchecked((uint){bitsOperand})",
                SpecialType.System_UInt64 => $"unchecked((ulong){bitsOperand})",
                _ => $"({t.ClrTypeName}){bitsOperand}"
            };

        /// <summary>Expression converting a boxed <c>value</c> operand to inline bits.</summary>
        public static string FromObject(InlineTypeInfo t) =>
            t.SpecialType switch
            {
                SpecialType.System_Single => "BitConverter.SingleToInt32Bits((float)value)",
                SpecialType.System_Double => "BitConverter.DoubleToInt64Bits((double)value)",
                SpecialType.System_Boolean => "(bool)value ? 1 : 0",
                SpecialType.System_Byte => "(long)(byte)value",
                SpecialType.System_SByte => "(long)(sbyte)value",
                SpecialType.System_Int16 => "(long)(short)value",
                SpecialType.System_UInt16 => "(long)(ushort)value",
                SpecialType.System_Int32 => "(long)(int)value",
                SpecialType.System_UInt32 => "unchecked((long)(uint)value)",
                SpecialType.System_Int64 => "(long)value",
                SpecialType.System_UInt64 => "unchecked((long)(ulong)value)",
                SpecialType.System_Char => "(long)(char)value",
                _ => "(long)value"
            };

        /// <summary>Expression reinterpreting a generic <c>value</c> as the target local type.</summary>
        public static string FactoryExtractExpr(InlineTypeInfo t) =>
            $"Unsafe.As<T, {t.ClrTypeName}>(ref value)";
    }
}
