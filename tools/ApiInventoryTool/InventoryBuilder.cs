using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ApiInventoryTool;

/// <summary>One shell assembly to inventory.</summary>
internal sealed record AssemblyInput(string Name, string Path);

/// <summary>Result of one inventory build: either the document or explicit errors.</summary>
internal sealed record InventoryBuildResult(InventoryDocument? Document, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Document is not null && Errors.Count == 0;
}

/// <summary>
///     Builds a deterministic Roslyn metadata inventory from real Release
///     build outputs. Exported types and members are read through
///     <see cref="IAssemblySymbol" />, so members emitted by source generators
///     and generated nested Godot signal types are part of the baseline.
///     <para>
///         The canonical lines record generic variance (<c>out</c>/<c>in</c>)
///         and the whitelisted nullable-flow attributes that change a
///         consumer's nullable analysis, because both are part of the
///         source-compatibility surface. Attributes are rendered explicitly:
///         <see cref="SymbolDisplayFormat" /> cannot emit them.
///     </para>
/// </summary>
internal static class InventoryBuilder
{
    private static readonly SymbolDisplayParameterOptions _parameterOptions =
        SymbolDisplayParameterOptions.IncludeName
        | SymbolDisplayParameterOptions.IncludeType
        | SymbolDisplayParameterOptions.IncludeDefaultValue
        | SymbolDisplayParameterOptions.IncludeParamsRefOut
        | SymbolDisplayParameterOptions.IncludeExtensionThis;

    private static readonly SymbolDisplayFormat _typeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    // Declaration display: only the type declaration line and the member
    // prefix (which repeats the containing type declaration) may carry
    // `in`/`out` markers. Constructed uses in member signatures use
    // _typeFormat/_memberFormat without IncludeVariance, because Roslyn renders
    // the definition's variance marker on constructed types as well.
    private static readonly SymbolDisplayFormat _declarationTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
            | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat _memberFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeRef
            | SymbolDisplayMemberOptions.IncludeExplicitInterface
            | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeAccessibility
            | SymbolDisplayMemberOptions.IncludeConstantValue,
        parameterOptions: _parameterOptions,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat _parameterFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        parameterOptions: _parameterOptions,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat _attributeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    // Only attributes that change the consumer's nullable flow analysis are
    // recorded. Rendering the full attribute set would freeze unrelated
    // metadata such as Obsolete or Flags, which the shell contract does not
    // promise.
    private static readonly HashSet<string> _nullableFlowAttributeNames = new(StringComparer.Ordinal)
    {
        "System.Diagnostics.CodeAnalysis.AllowNullAttribute",
        "System.Diagnostics.CodeAnalysis.DisallowNullAttribute",
        "System.Diagnostics.CodeAnalysis.MaybeNullAttribute",
        "System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute",
        "System.Diagnostics.CodeAnalysis.MemberNotNullAttribute",
        "System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullWhenAttribute",
    };

    public static InventoryBuildResult Build(
        IReadOnlyList<AssemblyInput> assemblies,
        IReadOnlyList<string> referenceDirectories,
        IReadOnlyList<string> forbiddenAssemblyNames)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(referenceDirectories);
        ArgumentNullException.ThrowIfNull(forbiddenAssemblyNames);

        var errors = new List<string>();
        var inputs = assemblies
            .OrderBy(a => a.Name, StringComparer.Ordinal)
            .ToArray();

        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                errors.Add("assembly name cannot be empty.");
            if (!File.Exists(input.Path))
                errors.Add($"assembly '{input.Name}' was not found at '{input.Path}'.");
        }

        if (errors.Count > 0)
            return new InventoryBuildResult(null, errors);

        var referencePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in referenceDirectories)
        {
            if (!Directory.Exists(directory))
            {
                errors.Add($"reference directory was not found: '{directory}'.");
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
                referencePaths.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        foreach (var input in inputs)
            referencePaths[Path.GetFileNameWithoutExtension(input.Path)] = input.Path;

        if (errors.Count > 0)
            return new InventoryBuildResult(null, errors);

        var frameworkReferences = CreateFrameworkReferences();
        var frameworkNames = frameworkReferences
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Display))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = frameworkReferences
            .Concat(referencePaths
                .Where(pair => !frameworkNames.Contains(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => MetadataReference.CreateFromFile(pair.Value)))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "Origo.ApiInventory",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var document = new InventoryDocument();
        var forbidden = forbiddenAssemblyNames.ToHashSet(StringComparer.Ordinal);

        foreach (var input in inputs)
        {
            var reference = references.FirstOrDefault(r =>
                string.Equals(Path.GetFileNameWithoutExtension(r.Display), input.Name, StringComparison.OrdinalIgnoreCase));
            if (reference is null)
            {
                errors.Add($"metadata reference for assembly '{input.Name}' was not created.");
                continue;
            }

            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                errors.Add($"cannot read assembly symbol for '{input.Name}' at '{input.Path}'.");
                continue;
            }

            var api = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in EnumerateExportedTypes(assembly.GlobalNamespace))
                AddType(api, errors, forbidden, type);

            document.Assemblies.Add(new AssemblyInventory { Name = input.Name, Api = [.. api] });
        }

        return errors.Count == 0
            ? new InventoryBuildResult(document, [])
            : new InventoryBuildResult(null, errors);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateExportedTypes(INamespaceSymbol root)
    {
        foreach (var type in root.GetTypeMembers().OrderBy(t => t.MetadataName, StringComparer.Ordinal))
            foreach (var exported in EnumerateTypeAndNested(type))
                yield return exported;

        foreach (var child in root.GetNamespaceMembers().OrderBy(n => n.Name, StringComparer.Ordinal))
            foreach (var type in EnumerateExportedTypes(child))
                yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypeAndNested(INamedTypeSymbol type)
    {
        if (type.DeclaredAccessibility != Accessibility.Public)
            yield break;

        yield return type;

        foreach (var nested in type.GetTypeMembers().OrderBy(t => t.MetadataName, StringComparer.Ordinal))
            foreach (var exported in EnumerateTypeAndNested(nested))
                yield return exported;
    }

    private static void AddType(
        SortedSet<string> api,
        List<string> errors,
        HashSet<string> forbidden,
        INamedTypeSymbol type)
    {
        if (FindForbidden(type, forbidden, out var forbiddenReference))
        {
            errors.Add($"exported type '{type.ToDisplayString(_typeFormat)}' exposes kernel type '{forbiddenReference}'.");
            return;
        }

        if (FindUnresolved(type, out var unresolvedTypeReference))
        {
            errors.Add(
                $"exported type '{type.ToDisplayString(_typeFormat)}' references unresolved type " +
                $"'{unresolvedTypeReference}'. Add the missing assembly directory through --reference-dir.");
            return;
        }

        var kind = GetTypeKind(type);
        var modifiers = GetTypeModifiers(type);
        var modifierPrefix = modifiers.Count == 0 ? string.Empty : string.Join(" ", modifiers) + " ";
        var line = $"T:{GetAccessibility(type.DeclaredAccessibility)} {modifierPrefix}{kind} {type.ToDisplayString(_declarationTypeFormat)}";
        if (GetBaseTypeLine(type) is { } baseLine)
            line += $" : {baseLine}";
        foreach (var constraint in GetTypeParameterConstraints(type))
            line += $"; {constraint}";
        api.Add(line);

        foreach (var member in type.GetMembers().OrderBy(SortKey, StringComparer.Ordinal))
            AddMember(api, errors, forbidden, member);
    }

    private static void AddMember(
        SortedSet<string> api,
        List<string> errors,
        HashSet<string> forbidden,
        ISymbol member)
    {
        if (!IsShellAccessibility(member.DeclaredAccessibility))
            return;
        if (member.IsImplicitlyDeclared)
            return;
        if (member is IMethodSymbol method)
        {
            if (method.AssociatedSymbol is not null)
                return;
            if (method.MethodKind is MethodKind.StaticConstructor or MethodKind.Destructor)
                return;
            if (FindForbidden(member, forbidden, out var forbiddenMethodReference))
            {
                errors.Add($"member '{method.ToDisplayString(_memberFormat)}' exposes kernel type '{forbiddenMethodReference}'.");
                return;
            }

            if (FindUnresolved(member, out var unresolvedMethodReference))
            {
                errors.Add(
                    $"member '{method.ToDisplayString(_memberFormat)}' references unresolved type " +
                    $"'{unresolvedMethodReference}'. Add the missing assembly directory through --reference-dir.");
                return;
            }

            var methodLine = method.ToDisplayString(_memberFormat);
            foreach (var constraint in GetMethodTypeParameterConstraints(method))
                methodLine += $"; {constraint}";
            methodLine = ApplyParameterFlowAttributes(method.Parameters, methodLine);
            methodLine = PrefixFlowAttributes(method, methodLine);
            api.Add($"M:{MemberPrefix(method)}{methodLine}");
            return;
        }

        if (member is IPropertySymbol property)
        {
            if (FindForbidden(property, forbidden, out var forbiddenPropertyReference))
            {
                errors.Add($"property '{property.ToDisplayString(_memberFormat)}' exposes kernel type '{forbiddenPropertyReference}'.");
                return;
            }

            if (FindUnresolved(property, out var unresolvedPropertyReference))
            {
                errors.Add(
                    $"property '{property.ToDisplayString(_memberFormat)}' references unresolved type " +
                    $"'{unresolvedPropertyReference}'. Add the missing assembly directory through --reference-dir.");
                return;
            }

            var accessors = string.Join(" ", GetAccessorVisibility(property));
            var propertyLine = property.ToDisplayString(_memberFormat);
            propertyLine = ApplyParameterFlowAttributes(property.Parameters, propertyLine);
            propertyLine = PrefixFlowAttributes(property, propertyLine);
            api.Add($"P:{MemberPrefix(property)}{propertyLine} {accessors}");
            return;
        }

        if (member is IFieldSymbol field)
        {
            if (FindForbidden(field, forbidden, out var forbiddenFieldReference))
            {
                errors.Add($"field '{field.ToDisplayString(_memberFormat)}' exposes kernel type '{forbiddenFieldReference}'.");
                return;
            }

            if (FindUnresolved(field, out var unresolvedFieldReference))
            {
                errors.Add(
                    $"field '{field.ToDisplayString(_memberFormat)}' references unresolved type " +
                    $"'{unresolvedFieldReference}'. Add the missing assembly directory through --reference-dir.");
                return;
            }

            var fieldLine = PrefixFlowAttributes(field, field.ToDisplayString(_memberFormat));
            api.Add($"F:{MemberPrefix(field)}{fieldLine}");
            return;
        }

        if (member is IEventSymbol @event)
        {
            if (FindForbidden(@event, forbidden, out var forbiddenEventReference))
            {
                errors.Add($"event '{@event.ToDisplayString(_memberFormat)}' exposes kernel type '{forbiddenEventReference}'.");
                return;
            }

            if (FindUnresolved(@event, out var unresolvedEventReference))
            {
                errors.Add(
                    $"event '{@event.ToDisplayString(_memberFormat)}' references unresolved type " +
                    $"'{unresolvedEventReference}'. Add the missing assembly directory through --reference-dir.");
                return;
            }

            api.Add($"E:{MemberPrefix(@event)}{@event.ToDisplayString(_memberFormat)}");
        }
    }

    /// <summary>
    ///     Renders the whitelisted nullable-flow attributes on one parameter list
    ///     directly into the parameter text, so the canonical line shows the same
    ///     annotations a consumer sees. The parameter display is unique inside the
    ///     member line because it includes the parameter name.
    /// </summary>
    private static string ApplyParameterFlowAttributes(
        IReadOnlyList<IParameterSymbol> parameters,
        string memberLine)
    {
        // Locate each parameter positionally instead of searching for its display
        // text in isolation: parameter displays can be prefixes of one another
        // (for example "out string? val" inside "out string? value"), and an
        // isolated IndexOf would then annotate the wrong parameter. Walking the
        // parameter list in order and searching after the previous parameter's end
        // keeps every parameter distinct.
        var insertions = new List<(int Index, string Annotation)>();
        var searchStart = 0;
        foreach (var parameter in parameters)
        {
            var parameterDisplay = parameter.ToDisplayString(_parameterFormat);
            var index = memberLine.IndexOf(parameterDisplay, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                // Formatting and rendering must stay aligned; a mismatch means
                // the canonical line would silently lose a nullable-flow
                // annotation, so fail explicitly instead.
                throw new InvalidOperationException(
                    $"Could not locate parameter display '{parameterDisplay}' inside member line '{memberLine}'.");
            }

            searchStart = index + parameterDisplay.Length;
            var attributes = RenderFlowAttributes(parameter.GetAttributes());
            if (attributes.Count > 0)
                insertions.Add((index, string.Join(" ", attributes) + " "));
        }

        // Apply from last to first so earlier insertion indexes stay valid.
        for (var index = insertions.Count - 1; index >= 0; index--)
        {
            var (position, annotation) = insertions[index];
            memberLine = memberLine[..position] + annotation + memberLine[position..];
        }

        return memberLine;
    }

    /// <summary>
    ///     Prefixes whitelisted member-level and return-level nullable-flow
    ///     attributes. Member attributes come first, then a combined
    ///     <c>[return: ...]</c> group, both sorted ordinally.
    /// </summary>
    private static string PrefixFlowAttributes(ISymbol member, string memberLine)
    {
        var memberAttributes = RenderFlowAttributes(member.GetAttributes());
        List<string> returnAttributes = [];
        if (member is IMethodSymbol method)
        {
            returnAttributes = RenderFlowAttributes(method.GetReturnTypeAttributes());
        }
        else if (member is IPropertySymbol property && property.GetMethod is { } getter)
        {
            returnAttributes = RenderFlowAttributes(getter.GetReturnTypeAttributes());
        }

        var prefix = string.Empty;
        if (memberAttributes.Count > 0)
            prefix += string.Join(" ", memberAttributes) + " ";
        if (returnAttributes.Count > 0)
        {
            // RenderFlowAttributes returns bracketed groups; a combined
            // [return: ...] group needs the inner names without their brackets.
            var returnNames = returnAttributes
                .Select(rendered => rendered[1..^1])
                .OrderBy(name => name, StringComparer.Ordinal);
            prefix += $"[return: {string.Join(", ", returnNames)}] ";
        }
        return prefix + memberLine;
    }

    private static List<string> RenderFlowAttributes(IEnumerable<AttributeData> attributes)
    {
        var rendered = new List<string>();
        foreach (var attribute in attributes)
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
                continue;

            var attributeName = attributeClass.ToDisplayString(_attributeFormat);
            if (!_nullableFlowAttributeNames.Contains(attributeName))
                continue;

            const string attributeSuffix = "Attribute";
            if (attributeName.EndsWith(attributeSuffix, StringComparison.Ordinal))
                attributeName = attributeName[..^attributeSuffix.Length];

            rendered.Add($"[{attributeName}{RenderAttributeArguments(attribute)}]");
        }

        rendered.Sort(StringComparer.Ordinal);
        return rendered;
    }

    private static string RenderAttributeArguments(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return string.Empty;

        var arguments = new List<string>();
        foreach (var argument in attribute.ConstructorArguments)
            AppendTypedConstant(argument, arguments);
        return $"({string.Join(", ", arguments)})";
    }

    private static void AppendTypedConstant(TypedConstant constant, List<string> arguments)
    {
        if (constant.Kind == TypedConstantKind.Array)
        {
            foreach (var element in constant.Values)
                AppendTypedConstant(element, arguments);
            return;
        }

        arguments.Add(constant.Value switch
        {
            bool boolean => boolean ? "true" : "false",
            string text => $"\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
            null => "null",
            var value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        });
    }

    private static IEnumerable<string> GetMethodTypeParameterConstraints(IMethodSymbol method)
    {
        foreach (var parameter in method.TypeParameters)
        {
            var parts = BuildConstraintParts(parameter);
            if (parts.Count > 0)
                yield return $"where {parameter.Name} : {string.Join(", ", parts)}";
        }
    }

    private static string MemberPrefix(ISymbol member) =>
        member.ContainingType.ToDisplayString(_declarationTypeFormat) + ".";

    private static IEnumerable<string> GetAccessorVisibility(IPropertySymbol property)
    {
        if (property.GetMethod is not null && IsShellAccessibility(property.GetMethod.DeclaredAccessibility))
            yield return $"get:{GetAccessibility(property.GetMethod.DeclaredAccessibility)}";
        if (property.SetMethod is not null && IsShellAccessibility(property.SetMethod.DeclaredAccessibility))
        {
            var setter = property.SetMethod.IsInitOnly ? "init" : "set";
            yield return $"{setter}:{GetAccessibility(property.SetMethod.DeclaredAccessibility)}";
        }
    }

    /// <summary>
    ///     Creates metadata references for the .NET shared-framework directory
    ///     the shell assemblies were compiled against. The full framework set is
    ///     required for metadata-only resolution: shell assemblies reference
    ///     facade assemblies (for example <c>System.Collections</c> for
    ///     <c>System.Collections.Generic.Dictionary&lt;,&gt;</c>), and a facade
    ///     left out of the reference set resolves to an error symbol, which the
    ///     unresolved-reference gate would report as a false positive.
    /// </summary>
    private static MetadataReference[] CreateFrameworkReferences()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)
            ?? throw new InvalidOperationException("Could not locate the .NET runtime directory.");
        return [.. Directory.EnumerateFiles(runtimeDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            // The shared framework directory also contains native Windows
            // libraries (coreclr.dll, clrjit.dll, ...). Roslyn rejects those
            // with BadImageFormatException; on Unix they are not *.dll, so
            // without this filter the gate would pass locally and fail on the
            // Windows matrix.
            .Where(HasManagedMetadata)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }

    private static bool HasManagedMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            return peReader.HasMetadata;
        }
        catch (BadImageFormatException)
        {
            // Not a PE image; it cannot be a metadata reference.
            return false;
        }
    }

    private static string GetBaseTypeLine(INamedTypeSymbol type)
    {
        var parts = new List<string>();
        if (type.BaseType is { } baseType)
        {
            var display = baseType.ToDisplayString(_typeFormat);
            var isFrameworkBase = baseType.SpecialType is SpecialType.System_Object
                or SpecialType.System_ValueType
                or SpecialType.System_Enum
                || display is "System.Object" or "object" or "System.ValueType" or "System.Enum";
            if (!isFrameworkBase)
                parts.Add(display);
        }
        parts.AddRange(type.Interfaces
            .Select(i => i.ToDisplayString(_typeFormat))
            .OrderBy(s => s, StringComparer.Ordinal));
        return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
    }

    private static IEnumerable<string> GetTypeParameterConstraints(INamedTypeSymbol type)
    {
        foreach (var parameter in type.TypeParameters)
        {
            var parts = BuildConstraintParts(parameter);
            if (parts.Count > 0)
                yield return $"where {parameter.Name} : {string.Join(", ", parts)}";
        }
    }

    private static List<string> BuildConstraintParts(ITypeParameterSymbol parameter)
    {
        var parts = new List<string>();
        if (parameter.HasReferenceTypeConstraint)
            parts.Add("class");
        if (parameter.HasValueTypeConstraint)
            parts.Add("struct");
        if (parameter.HasNotNullConstraint)
            parts.Add("notnull");
        if (parameter.HasUnmanagedTypeConstraint)
            parts.Add("unmanaged");
        parts.AddRange(parameter.ConstraintTypes
            .Select(t => t.ToDisplayString(_typeFormat))
            .OrderBy(s => s, StringComparer.Ordinal));
        if (parameter.HasConstructorConstraint)
            parts.Add("new()");
        return parts;
    }

    private static bool FindForbidden(ISymbol symbol, HashSet<string> forbidden, out string forbiddenReference)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        return FindForbiddenInTypes(GetReferencedTypes(symbol), forbidden, visited, out forbiddenReference);
    }

    private static bool FindUnresolved(ISymbol symbol, out string unresolvedReference)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        return FindUnresolvedInTypes(GetReferencedTypes(symbol), visited, out unresolvedReference);
    }

    /// <summary>
    ///     Detects metadata types whose defining assembly could not be resolved.
    ///     A missing reference otherwise yields a plausible-looking display name
    ///     and would silently produce a baseline that no real consumer can compile.
    /// </summary>
    private static bool FindUnresolvedInTypes(
        IEnumerable<ITypeSymbol?> types,
        HashSet<ITypeSymbol> visited,
        out string unresolvedReference)
    {
        foreach (var type in types)
        {
            if (type is null || !visited.Add(type))
                continue;

            if (type is IErrorTypeSymbol || type.TypeKind == TypeKind.Error)
            {
                unresolvedReference = type.ToDisplayString(_typeFormat);
                return true;
            }

            if (type is IArrayTypeSymbol array
                && FindUnresolvedInTypes([array.ElementType], visited, out unresolvedReference))
                return true;

            if (type is IPointerTypeSymbol pointer
                && FindUnresolvedInTypes([pointer.PointedAtType], visited, out unresolvedReference))
                return true;

            if (type is IFunctionPointerTypeSymbol functionPointer
                && FindUnresolvedInTypes(
                    GetReferencedTypes(functionPointer.Signature),
                    visited,
                    out unresolvedReference))
                return true;

            if (type is INamedTypeSymbol named
                && FindUnresolvedInTypes(named.TypeArguments.Cast<ITypeSymbol?>(), visited, out unresolvedReference))
                return true;
        }

        unresolvedReference = string.Empty;
        return false;
    }

    private static bool FindForbiddenInTypes(
        IEnumerable<ITypeSymbol?> types,
        HashSet<string> forbidden,
        HashSet<ITypeSymbol> visited,
        out string forbiddenReference)
    {
        foreach (var type in types)
        {
            if (type is null || !visited.Add(type))
                continue;

            if (string.Equals(type.ContainingAssembly?.Name, "Origo.Core.Kernel", StringComparison.Ordinal)
                || forbidden.Contains(type.ContainingAssembly?.Name ?? string.Empty))
            {
                forbiddenReference = type.ToDisplayString(_typeFormat);
                return true;
            }

            if (type is IArrayTypeSymbol array
                && FindForbiddenInTypes([array.ElementType], forbidden, visited, out forbiddenReference))
                return true;

            if (type is IPointerTypeSymbol pointer
                && FindForbiddenInTypes([pointer.PointedAtType], forbidden, visited, out forbiddenReference))
                return true;

            if (type is IFunctionPointerTypeSymbol functionPointer
                && FindForbiddenInTypes(
                    GetReferencedTypes(functionPointer.Signature),
                    forbidden,
                    visited,
                    out forbiddenReference))
                return true;

            if (type is INamedTypeSymbol named
                && FindForbiddenInTypes(named.TypeArguments.Cast<ITypeSymbol?>(), forbidden, visited, out forbiddenReference))
                return true;
        }

        forbiddenReference = string.Empty;
        return false;
    }

    private static IEnumerable<ITypeSymbol?> GetReferencedTypes(ISymbol symbol)
    {
        switch (symbol)
        {
            case INamedTypeSymbol type:
                yield return type.BaseType;
                foreach (var @interface in type.Interfaces)
                    yield return @interface;
                foreach (var argument in type.TypeArguments)
                    yield return argument;
                foreach (var parameter in type.TypeParameters)
                    foreach (var constraint in parameter.ConstraintTypes)
                        yield return constraint;
                break;
            case IMethodSymbol method:
                yield return method.ReturnType;
                foreach (var parameter in method.Parameters)
                    yield return parameter.Type;
                foreach (var argument in method.TypeArguments)
                    yield return argument;
                foreach (var parameter in method.TypeParameters)
                    foreach (var constraint in parameter.ConstraintTypes)
                        yield return constraint;
                break;
            case IPropertySymbol property:
                yield return property.Type;
                foreach (var parameter in property.Parameters)
                    yield return parameter.Type;
                break;
            case IFieldSymbol field:
                yield return field.Type;
                break;
            case IEventSymbol @event:
                yield return @event.Type;
                break;
        }
    }

    private static List<string> GetTypeModifiers(INamedTypeSymbol type)
    {
        var modifiers = new List<string>();
        if (type.IsStatic)
        {
            modifiers.Add("static");
        }
        else
        {
            if (type.TypeKind == TypeKind.Class && type.IsAbstract)
                modifiers.Add("abstract");
            if (type.TypeKind == TypeKind.Class && type.IsSealed)
                modifiers.Add("sealed");
        }

        if (type.TypeKind == TypeKind.Struct)
        {
            if (type.IsReadOnly)
                modifiers.Add("readonly");
            if (type.IsRefLikeType)
                modifiers.Add("ref");
        }

        return modifiers;
    }

    private static string GetTypeKind(INamedTypeSymbol type) =>
        type.TypeKind switch
        {
            TypeKind.Interface => "Interface",
            TypeKind.Enum => "Enum",
            TypeKind.Delegate => "Delegate",
            TypeKind.Struct => type.IsRecord ? "RecordStruct" : "Struct",
            TypeKind.Class => type.IsRecord ? "Record" : "Class",
            _ => type.TypeKind.ToString(),
        };

    private static string GetAccessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Private => "private",
            _ => accessibility.ToString(),
        };

    private static bool IsShellAccessibility(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;

    private static string SortKey(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => $"M:{method.MetadataName}:{method.ToDisplayString(_memberFormat)}",
            IPropertySymbol property => $"P:{property.MetadataName}",
            IFieldSymbol field => $"F:{field.MetadataName}",
            IEventSymbol @event => $"E:{@event.MetadataName}",
            _ => member.MetadataName,
        };
}
