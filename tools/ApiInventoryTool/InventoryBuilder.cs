using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// </summary>
internal static class InventoryBuilder
{
    private static readonly SymbolDisplayFormat _typeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
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
        parameterOptions: SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeDefaultValue
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeExtensionThis,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

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

        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)
            ?? throw new InvalidOperationException("Could not locate the .NET runtime directory.");
        var systemRuntimePath = Path.Combine(runtimeDirectory, "System.Runtime.dll");
        if (!File.Exists(systemRuntimePath))
        {
            errors.Add($"runtime reference was not found: '{systemRuntimePath}'.");
            return new InventoryBuildResult(null, errors);
        }

        var runtimeReferences = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(systemRuntimePath),
        };
        var references = runtimeReferences
            .Concat(referencePaths
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

        var kind = GetTypeKind(type);
        var modifiers = GetTypeModifiers(type);
        var modifierPrefix = modifiers.Count == 0 ? string.Empty : string.Join(" ", modifiers) + " ";
        var line = $"T:{GetAccessibility(type.DeclaredAccessibility)} {modifierPrefix}{kind} {type.ToDisplayString(_typeFormat)}";
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

            var methodLine = method.ToDisplayString(_memberFormat);
            foreach (var constraint in GetMethodTypeParameterConstraints(method))
                methodLine += $"; {constraint}";
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

            var accessors = string.Join(" ", GetAccessorVisibility(property));
            api.Add($"P:{MemberPrefix(property)}{property.ToDisplayString(_memberFormat)} {accessors}");
            return;
        }

        if (member is IFieldSymbol field)
        {
            if (FindForbidden(field, forbidden, out var forbiddenFieldReference))
            {
                errors.Add($"field '{field.ToDisplayString(_memberFormat)}' exposes kernel type '{forbiddenFieldReference}'.");
                return;
            }

            api.Add($"F:{MemberPrefix(field)}{field.ToDisplayString(_memberFormat)}");
            return;
        }

        if (member is IEventSymbol @event)
        {
            if (FindForbidden(@event, forbidden, out var forbiddenEventReference))
            {
                errors.Add($"event '{@event.ToDisplayString(_memberFormat)}' exposes kernel type '{forbiddenEventReference}'.");
                return;
            }

            api.Add($"E:{MemberPrefix(@event)}{@event.ToDisplayString(_memberFormat)}");
        }
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
        member.ContainingType.ToDisplayString(_typeFormat) + ".";

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
