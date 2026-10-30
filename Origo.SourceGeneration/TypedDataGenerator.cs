using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private const string _typedDataLayeredRegistryFullName =
        "Origo.Core.Snd.Metadata.TypedDataLayeredRegistry";

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

        // Diagnostics are reported through a separate output that includes
        // the current Compilation. Caching a SourceOutput that contains
        // Diagnostic objects is unsafe: a later compilation with equal
        // declaration data replays locations from the previous SyntaxTree and
        // Roslyn drops them with CS8785. Source generation remains cached; the
        // diagnostic output always runs against the current compilation.
        var diagnosticProvider = context.CompilationProvider.Select(
            (compilation, ct) => (Compilation: compilation,
                Input: ExtractGenerationInput(compilation)));

        context.RegisterSourceOutput(inputProvider, GenerateTypedDataExtensions);
        context.RegisterSourceOutput(diagnosticProvider,
            (spc, pair) => ReportDiagnostics(spc, pair.Input));
    }

    private static GenerationInput ExtractGenerationInput(Compilation compilation)
    {
        var isHome = IsHomeAssembly(compilation);
        var typeGroups = new List<TypeGroup>();
        var hasNullInlineType = false;
        Location? nullInlineTypeLocation = null;
        Location? attributeLocation = null;

        foreach (var attributeData in compilation.Assembly.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() != _attributeFullName)
                continue;

            attributeLocation ??= attributeData.ApplicationSyntaxReference?
                .GetSyntax().GetLocation();
            var startKind = ExtractStartKind(attributeData);
            var types = ExtractTypes(
                attributeData, startKind, out var hasNullElement, out var nullElementLocation);
            if (hasNullElement)
            {
                hasNullInlineType = true;
                nullInlineTypeLocation ??= nullElementLocation;
            }

            if (types.Count > 0)
                typeGroups.Add(new TypeGroup { StartKind = startKind, Types = types });
        }

        var hasTypedDataFriendAccess = isHome || HasTypedDataFriendAccess(compilation);
        var assemblyLocation = attributeLocation
            ?? compilation.Assembly.Locations.FirstOrDefault()
            ?? Location.None;

        return new GenerationInput
        {
            IsHome = isHome,
            HasTypedDataFriendAccess = hasTypedDataFriendAccess,
            AssemblyName = compilation.AssemblyName ?? "<unknown>",
            AdapterDiagnosticLocation = assemblyLocation,
            HasNullInlineType = hasNullInlineType,
            NullInlineTypeLocation = nullInlineTypeLocation,
            TypeGroups = typeGroups
        };
    }

    private static bool IsHomeAssembly(Compilation compilation)
    {
        var typedDataSymbol = compilation.GetTypeByMetadataName(_typedDataFullName);
        return typedDataSymbol?.ContainingAssembly
            .Equals(compilation.Assembly, SymbolEqualityComparer.Default) == true;
    }

    /// <summary>
    ///     True when the current assembly can legally access TypedData's
    ///     internal registration surface. Adapter-mode generated code reads
    ///     <c>_kind</c>/<c>_ref</c> and calls internal registration APIs, so an
    ///     adapter without friend access would fail with CS0122/CS0117 after
    ///     generation. Detecting friend access here turns that into the
    ///     actionable ORIGOSG007 diagnostic before any source is emitted.
    /// </summary>
    private static bool HasTypedDataFriendAccess(Compilation compilation)
    {
        var typedDataSymbol = compilation.GetTypeByMetadataName(_typedDataFullName);
        if (typedDataSymbol is null)
            return false;

        var registerKind = typedDataSymbol.GetMembers("RegisterKind").FirstOrDefault();
        if (registerKind is null)
            return false;

        var layeredRegistry = compilation.GetTypeByMetadataName(_typedDataLayeredRegistryFullName);
        if (layeredRegistry is null)
            return false;

        return compilation.IsSymbolAccessibleWithin(registerKind, compilation.Assembly)
            && compilation.IsSymbolAccessibleWithin(layeredRegistry, compilation.Assembly);
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

    private static List<InlineTypeInfo> ExtractTypes(
        AttributeData attr,
        int startKind,
        out bool hasNullElement,
        out Location? firstNullElementLocation)
    {
        var result = new List<InlineTypeInfo>();
        var kindOffset = 0;
        hasNullElement = false;
        firstNullElementLocation = null;
        var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

        foreach (var ctorArg in attr.ConstructorArguments)
        {
            if (ctorArg.IsNull)
            {
                if (!hasNullElement)
                {
                    hasNullElement = true;
                    firstNullElementLocation = attrLocation;
                }
                continue;
            }

            if (ctorArg.Kind == TypedConstantKind.Array)
            {
                if (ctorArg.Values.IsDefault)
                {
                    if (!hasNullElement)
                    {
                        hasNullElement = true;
                        firstNullElementLocation = attrLocation;
                    }
                    continue;
                }

                foreach (var element in ctorArg.Values)
                {
                    if (element.Value is ITypeSymbol typeSymbol)
                    {
                        result.Add(CreateTypeInfo(typeSymbol, startKind + kindOffset++, attrLocation));
                    }
                    else if (!hasNullElement)
                    {
                        hasNullElement = true;
                        firstNullElementLocation = attrLocation;
                    }
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
            IsRefLikeType = typeSymbol.IsRefLikeType,
            IsStatic = typeSymbol is INamedTypeSymbol { IsStatic: true },
            IsUnboundGenericType = typeSymbol is INamedTypeSymbol { IsUnboundGenericType: true },
            IsNullableValueType = typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T },
            IsVoid = typeSymbol.SpecialType == SpecialType.System_Void,
            IsTypedReference = typeSymbol.SpecialType == SpecialType.System_TypedReference,
            TypeKind = typeSymbol.TypeKind,
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
        var plan = BuildGenerationPlan(input);
        if (plan.ValidTypes.Count == 0)
            return;

        if (input.IsHome)
            GenerateHomeAssembly(context, plan.ValidTypes);
        else
            GenerateAdapterAssembly(context, plan.ValidTypes);
    }

    private static void ReportDiagnostics(SourceProductionContext context, GenerationInput input)
    {
        foreach (var diagnostic in BuildGenerationPlan(input).Diagnostics)
            context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    ///     Pure validation pass shared by source generation and the separate
    ///     diagnostic output. Diagnostics are returned instead of reported so
    ///     a cached source output never replays a Diagnostic whose Location
    ///     belongs to an older compilation.
    /// </summary>
    private static GenerationPlan BuildGenerationPlan(GenerationInput input)
    {
        var diagnostics = new List<Diagnostic>();
        if (input.HasNullInlineType)
        {
            diagnostics.Add(Diagnostic.Create(
                _nullInlineType, input.NullInlineTypeLocation ?? Location.None));
            return new GenerationPlan([], diagnostics);
        }

        var allTypes = input.TypeGroups.SelectMany(g => g.Types).ToList();
        if (allTypes.Count == 0)
            return new GenerationPlan([], diagnostics);

        if (!input.IsHome && !input.HasTypedDataFriendAccess)
        {
            diagnostics.Add(Diagnostic.Create(
                _adapterFriendAccessRequired,
                input.AdapterDiagnosticLocation ?? Location.None,
                input.AssemblyName));
            return new GenerationPlan([], diagnostics);
        }

        var validTypes = ValidateAndFilter(input.IsHome, allTypes, diagnostics);
        // Identifier validity runs before the name-collision gate: unnamed types
        // (pointer/array types have an empty Name) sanitize to an empty kind
        // name, and each such registration must report ORIGOSG006 instead of a
        // pair of them looking like one empty-name ORIGOSG005 collision.
        validTypes = RejectInvalidKindNames(validTypes, input.IsHome, diagnostics);
        validTypes = RejectKindCollisions(validTypes, diagnostics);
        validTypes = RejectKindNameCollisions(validTypes, diagnostics);
        return new GenerationPlan(validTypes, diagnostics);
    }

    private static List<InlineTypeInfo> ValidateAndFilter(
        bool isHome, List<InlineTypeInfo> allTypes, List<Diagnostic> diagnostics)
    {
        var valid = new List<InlineTypeInfo>();

        foreach (var t in allTypes)
        {
            if (IsUnsupportedGeneratedType(t))
            {
                diagnostics.Add(Diagnostic.Create(
                    _unsupportedAdapterType, t.Location ?? Location.None, t.ClrTypeName));
                continue;
            }

            if (t.RawKind is <= 0 or > 254)
            {
                diagnostics.Add(
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
                    diagnostics.Add(
                        Diagnostic.Create(_systemPrimitiveInAdapter, t.Location ?? Location.None, t.ClrTypeName));
            }
            else
            {
                if (isHome)
                    diagnostics.Add(
                        Diagnostic.Create(_unsupportedHomeValueType, t.Location ?? Location.None, t.ClrTypeName));
                else
                    valid.Add(t);
            }
        }

        return valid;
    }

    private static bool IsUnsupportedGeneratedType(InlineTypeInfo type) =>
        type.IsVoid
        || type.IsTypedReference
        || type.IsRefLikeType
        || type.IsStatic
        || type.IsUnboundGenericType
        || type.IsNullableValueType;

    private static List<InlineTypeInfo> RejectKindCollisions(
        List<InlineTypeInfo> types, List<Diagnostic> diagnostics)
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
            diagnostics.Add(Diagnostic.Create(
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
        List<InlineTypeInfo> types, List<Diagnostic> diagnostics)
    {
        // KindMap always emits `public const byte Null = 0` and is itself a
        // generated class named KindMap; TypedData also declares a handwritten
        // `IsNull` property. A registered type whose sanitized name equals any
        // of these identifiers would emit a duplicate member or CS0542.
        const string reservedNullKindName = "Null";
        const string reservedKindMapClassName = "KindMap";
        var firstTypeByKindName = new Dictionary<string, InlineTypeInfo>(StringComparer.Ordinal);
        var collidingKindNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var t in types)
        {
            if (t.KindIndex is null) continue;
            if (string.Equals(t.KindIndex, reservedNullKindName, StringComparison.Ordinal)
                || string.Equals(t.KindIndex, reservedKindMapClassName, StringComparison.Ordinal)
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
            if (string.Equals(kindName, reservedNullKindName, StringComparison.Ordinal))
            {
                foreach (var t in colliding)
                    diagnostics.Add(Diagnostic.Create(
                        _kindNameCollision, t.Location ?? Location.None,
                        t.ClrTypeName, "the reserved KindMap sentinel", kindName));
            }
            else if (string.Equals(kindName, reservedKindMapClassName, StringComparison.Ordinal))
            {
                foreach (var t in colliding)
                    diagnostics.Add(Diagnostic.Create(
                        _kindNameCollision, t.Location ?? Location.None,
                        t.ClrTypeName, "the generated KindMap container", kindName));
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(
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
    ///     extension would silently win for consumer code while the home assembly's
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
    ///     identifiers (pointer and array types are unnamed, so they sanitize
    ///     to the empty string, which cannot become a generated accessor
    ///     identifier), and — in adapter mode — types whose kind names collide
    ///     with the Home assembly's reserved kind names (a *custom* type named
    ///     like a Home inline kind, e.g. the user's own <c>Int32</c>, diverges
    ///     from the Home accessor semantics; the BCL type itself does not).
    ///     This gate runs before the kind-name-collision gate so every type
    ///     whose identifier cannot be emitted reports ORIGOSG006, even when two
    ///     such types would otherwise look like one empty-name kind collision.
    /// </summary>
    private static List<InlineTypeInfo> RejectInvalidKindNames(
        List<InlineTypeInfo> types, bool isHome, List<Diagnostic> diagnostics)
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
                diagnostics.Add(Diagnostic.Create(
                    _invalidKindName, t.Location ?? Location.None, t.ClrTypeName, t.KindIndex));
                continue;
            }

            if (!isHome
                && _homeReservedKindNames.TryGetValue(t.KindIndex, out var bclSpecialType)
                && t.SpecialType != bclSpecialType)
            {
                diagnostics.Add(Diagnostic.Create(
                    _kindNameCollision, t.Location ?? Location.None,
                    t.ClrTypeName, "the Home assembly's reserved kind name", t.KindIndex));
                continue;
            }

            result.Add(t);
        }

        return result;
    }

    /// <summary>
    ///     StringBuilder variant that always terminates lines with LF.
    ///     Generated source must not depend on <see cref="Environment.NewLine" />:
    ///     the same generator input would otherwise produce CRLF on Windows
    ///     and LF elsewhere, making emitted sources host-dependent.
    /// </summary>
    private sealed class SourceBuilder
    {
        private readonly StringBuilder _builder = new();

        public SourceBuilder AppendLine()
        {
            _builder.Append('\n');
            return this;
        }

        public SourceBuilder AppendLine(string? value)
        {
            _builder.Append(value);
            return AppendLine();
        }

        public override string ToString() => _builder.ToString();
    }

    private sealed class GenerationPlan(List<InlineTypeInfo> validTypes, List<Diagnostic> diagnostics)
    {
        public List<InlineTypeInfo> ValidTypes { get; } = validTypes;
        public List<Diagnostic> Diagnostics { get; } = diagnostics;
    }

    private sealed record GenerationInput
    {
        public bool IsHome { get; set; }
        public bool HasTypedDataFriendAccess { get; set; }
        public string AssemblyName { get; set; } = string.Empty;
        public Location? AdapterDiagnosticLocation { get; set; }
        public bool HasNullInlineType { get; set; }
        public Location? NullInlineTypeLocation { get; set; }
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
            if (x.HasTypedDataFriendAccess != y.HasTypedDataFriendAccess) return false;
            if (x.HasNullInlineType != y.HasNullInlineType) return false;
            if (!string.Equals(x.AssemblyName, y.AssemblyName, StringComparison.Ordinal)) return false;
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
                        || tx.IsRefLikeType != ty.IsRefLikeType
                        || tx.IsStatic != ty.IsStatic
                        || tx.IsUnboundGenericType != ty.IsUnboundGenericType
                        || tx.IsNullableValueType != ty.IsNullableValueType
                        || tx.IsVoid != ty.IsVoid
                        || tx.IsTypedReference != ty.IsTypedReference
                        || tx.TypeKind != ty.TypeKind
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
            hash.Add(obj.HasTypedDataFriendAccess);
            hash.Add(obj.HasNullInlineType);
            hash.Add(obj.AssemblyName);
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
                    hash.Add(t.IsRefLikeType);
                    hash.Add(t.IsStatic);
                    hash.Add(t.IsUnboundGenericType);
                    hash.Add(t.IsNullableValueType);
                    hash.Add(t.IsVoid);
                    hash.Add(t.IsTypedReference);
                    hash.Add(t.TypeKind);
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
        public bool IsRefLikeType { get; set; }
        public bool IsStatic { get; set; }
        public bool IsUnboundGenericType { get; set; }
        public bool IsNullableValueType { get; set; }
        public bool IsVoid { get; set; }
        public bool IsTypedReference { get; set; }
        public TypeKind TypeKind { get; set; }
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
