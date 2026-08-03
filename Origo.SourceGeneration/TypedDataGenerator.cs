using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Origo.SourceGeneration;

[Generator(LanguageNames.CSharp)]
public sealed partial class TypedDataGenerator : IIncrementalGenerator
{
    private const string _attributeFullName = "Origo.Core.Snd.Metadata.SndInlineTypesAttribute";
    private const string _typedDataFullName = "Origo.Core.Snd.Metadata.TypedData";
    private const string _registryFullName = "Origo.Core.Snd.Metadata.TypedDataLayeredRegistry";

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
                    if (element.Value is INamedTypeSymbol typeSymbol)
                        result.Add(CreateTypeInfo(typeSymbol, startKind + kindOffset++, attrLocation));
                    else if (element.Value is ITypeSymbol ts)
                        result.Add(CreateTypeInfo(ts, startKind + kindOffset++, attrLocation));
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
            Location = location
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

    private sealed record GenerationInput
    {
        public bool IsHome { get; set; }
        public List<TypeGroup> TypeGroups { get; set; } = [];
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
    }
}
