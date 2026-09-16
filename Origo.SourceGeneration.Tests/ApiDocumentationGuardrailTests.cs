using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Origo.SourceGeneration.Tests;

/// <summary>
///     CI-enforceable guard for AGENTS §1.7: every public/protected API
///     declaration in production source carries either a summary XML doc
///     comment or the standard C# <c>&lt;inheritdoc /&gt;</c> element.
/// </summary>
public class ApiDocumentationGuardrailTests
{
    private static readonly string[] _productionRoots =
    [
        "Origo.Core",
        "Origo.GodotAdapter",
        "Origo.ConsoleBridge",
        "Origo.SourceGeneration",
        "Origo.TestSupport",
        "tools/DocSyncTool"
    ];

    [Fact]
    public async Task PublicApi_EveryDeclaration_HasSummaryOrInheritDoc()
    {
        var repoRoot = FindRepoRoot();
        var violations = new List<string>();
        var documentedTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in _productionRoots)
        {
            var sourceRoot = Path.Combine(repoRoot, root);
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                if (relative.Contains("/obj/", StringComparison.Ordinal)
                    || relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/.godot/", StringComparison.Ordinal)
                    || relative.Contains("FastNoiseLite.cs", StringComparison.Ordinal))
                    continue;

                var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
                var tree = CSharpSyntaxTree.ParseText(text, cancellationToken: TestContext.Current.CancellationToken);
                var rootNode = await tree.GetRootAsync(TestContext.Current.CancellationToken);
                foreach (var node in rootNode.DescendantNodes(descendIntoTrivia: false))
                {
                    if (node is BaseTypeDeclarationSyntax type
                        && IsDocumentedApiNode(type)
                        && HasSummaryOrInheritDoc(type))
                        documentedTypes.Add(GetTypeKey(type));
                }
            }
        }

        foreach (var root in _productionRoots)
        {
            var sourceRoot = Path.Combine(repoRoot, root);
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                if (relative.Contains("/obj/", StringComparison.Ordinal)
                    || relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/.godot/", StringComparison.Ordinal)
                    || relative.Contains("FastNoiseLite.cs", StringComparison.Ordinal))
                    continue;

                var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
                var tree = CSharpSyntaxTree.ParseText(text, cancellationToken: TestContext.Current.CancellationToken);
                var rootNode = await tree.GetRootAsync(TestContext.Current.CancellationToken);
                foreach (var node in rootNode.DescendantNodes(descendIntoTrivia: false))
                {
                    if (!IsDocumentedApiNode(node))
                        continue;

                    if (HasSummaryOrInheritDoc(node))
                    {
                        if (node is BaseTypeDeclarationSyntax type)
                            documentedTypes.Add(GetTypeKey(type));
                        continue;
                    }

                    if (node is BaseTypeDeclarationSyntax partialType
                        && partialType.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
                        && documentedTypes.Contains(GetTypeKey(partialType)))
                        continue;

                    var line = tree.GetText(TestContext.Current.CancellationToken).Lines.GetLinePosition(node.SpanStart).Line + 1;
                    violations.Add($"{relative}:{line}: {node.Kind()} {GetDeclarationName(node)}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Public/protected API declarations without summary or <inheritdoc />:\n" +
            string.Join('\n', violations.OrderBy(v => v, StringComparer.Ordinal)));
    }

    private static bool IsDocumentedApiNode(SyntaxNode node)
    {
        switch (node)
        {
            case BaseTypeDeclarationSyntax type:
                return IsPublicOrProtected(type.Modifiers)
                    && HasPublicOrProtectedContainingTypes(type);

            case MemberDeclarationSyntax member:
                {
                    var containingType = member.Parent as BaseTypeDeclarationSyntax
                        ?? member.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
                    if (containingType is null || !IsEffectivelyPublicApi(containingType))
                        return false;

                    // Interface members are implicitly public even without the
                    // modifier token.
                    return containingType is InterfaceDeclarationSyntax
                        || IsPublicOrProtected(member.Modifiers);
                }

            default:
                return false;
        }
    }

    private static bool HasPublicOrProtectedContainingTypes(BaseTypeDeclarationSyntax type)
    {
        var current = type;
        while (true)
        {
            if (!IsPublicOrProtected(current.Modifiers))
                return false;

            var parent = current.Parent as BaseTypeDeclarationSyntax
                ?? current.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
            if (parent is null)
                return true;

            current = parent;
        }
    }

    private static bool IsEffectivelyPublicApi(BaseTypeDeclarationSyntax type) =>
        HasPublicOrProtectedContainingTypes(type);

    private static bool IsPublicOrProtected(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)
                           || m.IsKind(SyntaxKind.ProtectedKeyword));

    private static bool HasSummaryOrInheritDoc(SyntaxNode node)
    {
        var docComments = new List<DocumentationCommentTriviaSyntax>();
        foreach (var trivia in node.GetLeadingTrivia())
            CollectDocComments(trivia, docComments);

        if (node is MemberDeclarationSyntax member)
        {
            foreach (var attributeList in member.AttributeLists)
            {
                foreach (var trivia in attributeList.GetLeadingTrivia())
                    CollectDocComments(trivia, docComments);
            }
        }
        else if (node is BaseTypeDeclarationSyntax type)
        {
            foreach (var attributeList in type.AttributeLists)
            {
                foreach (var trivia in attributeList.GetLeadingTrivia())
                    CollectDocComments(trivia, docComments);
            }
        }

        return docComments.Any(d =>
        {
            var xml = d.ToString();
            return xml.Contains("<summary", StringComparison.Ordinal)
                || xml.Contains("<inheritdoc", StringComparison.Ordinal);
        });
    }

    private static void CollectDocComments(
        SyntaxTrivia trivia,
        List<DocumentationCommentTriviaSyntax> results)
    {
        if (trivia.HasStructure && trivia.GetStructure() is DocumentationCommentTriviaSyntax doc)
            results.Add(doc);
    }

    private static string GetTypeKey(BaseTypeDeclarationSyntax type)
    {
        var containingTypes = type.Ancestors().OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(t => t.Identifier.ValueText);
        var ns = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .LastOrDefault() ?? "";
        return $"{ns}.{string.Join("+", containingTypes)}.{type.Identifier.ValueText}";
    }

    private static string GetDeclarationName(SyntaxNode node)
    {
        return node switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            EventDeclarationSyntax evt => evt.Identifier.ValueText,
            EventFieldDeclarationSyntax evtField => string.Join(", ", evtField.Declaration.Variables.Select(v => v.Identifier.ValueText)),
            FieldDeclarationSyntax field => string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.ValueText)),
            ConstructorDeclarationSyntax ctor => ctor.Identifier.ValueText,
            OperatorDeclarationSyntax op => op.OperatorToken.ValueText,
            ConversionOperatorDeclarationSyntax conversion => conversion.Type.ToString(),
            IndexerDeclarationSyntax indexer => "this[]",
            DelegateDeclarationSyntax del => del.Identifier.ValueText,
            _ => node.Kind().ToString()
        };
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "docs")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Cannot find the repository root.");
    }
}
