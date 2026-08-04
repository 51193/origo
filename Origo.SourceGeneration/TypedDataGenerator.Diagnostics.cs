using Microsoft.CodeAnalysis;

namespace Origo.SourceGeneration;

public sealed partial class TypedDataGenerator
{
    private static readonly DiagnosticDescriptor _systemPrimitiveInAdapter = new(
        id: "ORIGOSG001",
        title: "System primitive registered outside the TypedData home assembly",
        messageFormat:
        "'{0}' is a system primitive and can only be registered as an inline TypedData type in the Origo.Core (home) assembly. "
        + "Adapter assemblies may register only reference types or non-system value types, which are stored through the _ref slot.",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _unsupportedHomeValueType = new(
        id: "ORIGOSG002",
        title: "Unsupported value type in the TypedData home assembly",
        messageFormat:
        "'{0}' is a value type that cannot be stored inline and is not supported in the Origo.Core (home) assembly. "
        + "Only the supported system primitives may be registered as home inline types.",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _kindOverflow = new(
        id: "ORIGOSG003",
        title: "TypedData kind byte out of range",
        messageFormat:
        "'{0}' resolves to kind {1}, which is outside the valid byte range [1, 254]. " +
        "Each registered type's kind is startKind plus its position in the SndInlineTypes group; "
        + "keep startKind plus the type count within [1, 254].",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _kindCollision = new(
        id: "ORIGOSG004",
        title: "TypedData kind collision",
        messageFormat:
        "Kind {0} is assigned to multiple types ({1}). "
        + "Each registered inline TypedData type must map to a unique kind byte; "
        + "adjust the SndInlineTypes startKind offsets so the kind ranges do not overlap.",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor _kindNameCollision = new(
        id: "ORIGOSG005",
        title: "TypedData kind name collision",
        messageFormat:
        "'{0}' and '{1}' produce the same kind name '{2}'. "
        + "Generated accessor identifiers are derived from the type name, so every registered "
        + "type must have a distinct name: types of the same name from different namespaces, "
        + "generic instantiations with the same name, and the same type registered more than "
        + "once (with the same or different kinds) are all rejected; register each type exactly once.",
        category: "Origo.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
