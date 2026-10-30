using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ApiInventoryTool.Tests;

public class InventoryBuilderTests
{
    [Fact]
    public void Build_IsDeterministicAndSorted()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        var secondDirectory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var first = TestAssemblyEmitter.Emit(directory, "Sample.Api",
                """
                namespace Sample { public interface IZ { int Z { get; } } public class A { public void M() { } } }
                """);
            var second = TestAssemblyEmitter.Emit(secondDirectory, "Sample.Api",
                """
                namespace Sample { public class A { public void M() { } } public interface IZ { int Z { get; } } }
                """);

            var firstResult = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Api", first)],
                [directory],
                []);
            var secondResult = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Api", second)],
                [secondDirectory],
                []);

            Assert.True(firstResult.Succeeded, string.Join('\n', firstResult.Errors));
            Assert.True(secondResult.Succeeded, string.Join('\n', secondResult.Errors));
            // The same assembly compiled with the declarations in the opposite
            // order must serialize byte-identically: the inventory is ordered by
            // assembly name and ordinal API lines, not by declaration order.
            Assert.Equal(
                InventoryJson.Serialize(firstResult.Document!),
                InventoryJson.Serialize(secondResult.Document!));
            Assert.Equal(
                firstResult.Document!.Assemblies[0].Api,
                secondResult.Document!.Assemblies[0].Api);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
            TestAssemblyEmitter.TryDelete(secondDirectory);
        }
    }

    [Fact]
    public void Build_MultipleAssemblies_SortsByName()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var beta = TestAssemblyEmitter.Emit(directory, "Beta.Api",
                "namespace Beta { public class B { public void M() { } } }");
            var alpha = TestAssemblyEmitter.Emit(directory, "Alpha.Api",
                "namespace Alpha { public class A { public void M() { } } }");

            var result = InventoryBuilder.Build(
                [
                    new AssemblyInput("Beta.Api", beta),
                    new AssemblyInput("Alpha.Api", alpha)
                ],
                [directory],
                []);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            Assert.Equal(["Alpha.Api", "Beta.Api"],
                result.Document!.Assemblies.Select(assembly => assembly.Name));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_RejectsMissingAssemblyExplicitly()
    {
        var result = InventoryBuilder.Build(
            [new AssemblyInput("Missing.Api", "/does/not/exist/Missing.Api.dll")],
            [],
            []);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("was not found", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_RejectsForbiddenKernelReference()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var kernel = TestAssemblyEmitter.Emit(directory, "Origo.Core.Kernel",
                "namespace Kernel { public class Hidden { } }");
            var shell = TestAssemblyEmitter.Emit(directory, "Origo.Shell",
                "namespace Shell { public class Visible { public Kernel.Hidden Create() => null!; } }",
                kernel);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Origo.Shell", shell)],
                [directory],
                ["Origo.Core.Kernel"]);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Contains("exposes kernel type", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Verify_ReportsAddedRemovedAndChangedApi()
    {
        var baselineDirectory = TestAssemblyEmitter.CreateDirectory();
        var currentDirectory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var baselinePath = TestAssemblyEmitter.Emit(baselineDirectory, "Sample",
                "namespace Sample { public class A { public void M() { } } }");
            var currentPath = TestAssemblyEmitter.Emit(currentDirectory, "Sample",
                "namespace Sample { public class A { public void M(int value) { } public void N() { } } }");

            var baseline = InventoryBuilder.Build(
                [new AssemblyInput("Sample", baselinePath)], [baselineDirectory], []).Document!;
            var current = InventoryBuilder.Build(
                [new AssemblyInput("Sample", currentPath)], [currentDirectory], []).Document!;

            var changes = InventoryComparer.Verify(baseline, current);

            Assert.Contains(changes, change => change.Contains("removed or changed API", StringComparison.Ordinal));
            Assert.Contains(changes, change => change.Contains("added API", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(baselineDirectory);
            TestAssemblyEmitter.TryDelete(currentDirectory);
        }
    }

    [Fact]
    public void ComparePrevious_AllowsAdditionsAndRejectsRemovals()
    {
        var previous = new InventoryDocument
        {
            Assemblies =
            [
                new AssemblyInventory { Name = "A", Api = ["M:A.Run()"] },
            ],
        };
        var additionsOnly = new InventoryDocument
        {
            Assemblies =
            [
                new AssemblyInventory { Name = "A", Api = ["M:A.Run()", "M:A.RunFast()"] },
            ],
        };
        var removal = new InventoryDocument
        {
            Assemblies =
            [
                new AssemblyInventory { Name = "A", Api = [] },
            ],
        };

        Assert.Empty(InventoryComparer.ComparePrevious(previous, additionsOnly));
        Assert.Contains(InventoryComparer.ComparePrevious(previous, removal),
            failure => failure.Contains("removed or changed API", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_CollectsShellMemberKindsAndConstraints()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = TestAssemblyEmitter.Emit(directory, "Sample.Rich",
                """
                using System;
                namespace Sample
                {
                    public enum Color { Red, Blue }
                    public delegate void Handler(string value);
                    public struct Point { public int X; public int Y { get; set; } }
                    public record Pair<T> where T : class, new()
                    {
                        public T? Item { get; init; }
                        public event Handler? Changed;
                        public const int Max = 3;
                        protected void OnChanged(string value) { }
                    }
                    public interface IThing { event Handler? Changed; int Value { get; } }
                    public class Base { public virtual void Run() { } }
                    public class Derived : Base, IThing
                    {
                        public int Value => 1;
                        public event Handler? Changed;
                        public static int Count;
                        public class Nested { public void Touch() { } }
                    }
                    public static class Helpers { public static T Create<T>() where T : class, new() => new(); }
                }
                """);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Rich", path)],
                [directory],
                []);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            var api = result.Document!.Assemblies.Single().Api;
            Assert.Contains(api, line => line.StartsWith("T:public Enum Sample.Color", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public Delegate Sample.Handler", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public Struct Sample.Point", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public Record Sample.Pair", StringComparison.Ordinal));
            Assert.Contains(api, line => line.Contains("where T : class, new()", StringComparison.Ordinal));
            Assert.Contains(api, line => line.Contains("Sample.Derived.Nested", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("P:Sample.Point.public int Y", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("E:Sample.Derived.public Sample.Handler?", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("F:Sample.Derived.public static int Count", StringComparison.Ordinal));
            Assert.Contains(api, line => line.Contains("protected void OnChanged", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_ReportsForbiddenMemberKinds()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var kernel = TestAssemblyEmitter.Emit(directory, "Origo.Core.Kernel",
                "namespace Kernel { public class Hidden { } public class Generic<T> { } }");
            var shell = TestAssemblyEmitter.Emit(directory, "Origo.Shell",
                """
                using System;
                namespace Shell
                {
                    public class Visible
                    {
                        public Kernel.Hidden Method() => null!;
                        public Kernel.Hidden Property { get; set; } = null!;
                        public Kernel.Hidden Field = null!;
                        public event Action<Kernel.Hidden>? Event;
                        public Kernel.Generic<Kernel.Hidden> Generic() => null!;
                    }
                }
                """,
                kernel);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Origo.Shell", shell)],
                [directory],
                ["Origo.Core.Kernel"]);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Contains("Method", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("Property", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("Field", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("Event", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_ReportsEmptyNameAndMissingReferenceDirectory()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = TestAssemblyEmitter.Emit(directory, "Sample.Api",
                "namespace Sample { public class A { } }");

            var emptyName = InventoryBuilder.Build(
                [new AssemblyInput(string.Empty, path)], [directory], []);
            Assert.Contains(emptyName.Errors, error => error.Contains("cannot be empty", StringComparison.Ordinal));

            var missingReference = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Api", path)],
                [Path.Combine(directory, "missing")],
                []);
            Assert.Contains(missingReference.Errors,
                error => error.Contains("reference directory was not found", StringComparison.Ordinal));

            var mismatchedName = InventoryBuilder.Build(
                [new AssemblyInput("OtherName", path)], [directory], []);
            Assert.Contains(mismatchedName.Errors,
                error => error.Contains("metadata reference", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }
    [Fact]
    public void Build_ResolvesTypeKindsWhenTheAssemblyReferencesSystemRuntime()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var systemRuntimePath = Path.Combine(runtimeDirectory, "System.Runtime.dll");
            Assert.True(File.Exists(systemRuntimePath), $"System.Runtime.dll not found at '{systemRuntimePath}'.");

            var path = TestAssemblyEmitter.Emit(directory, "Sample.RuntimeFacade",
                """
                namespace Sample
                {
                    public enum Color { Red }
                    public readonly struct Point { }
                    public readonly record struct Pair(int X, int Y);
                }
                """,
                systemRuntimePath);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Sample.RuntimeFacade", path)],
                [directory],
                []);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            var api = result.Document!.Assemblies.Single().Api;
            Assert.Contains(api, line => line.StartsWith("T:public Enum Sample.Color", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public readonly Struct Sample.Point", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public readonly Struct Sample.Pair", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_RecordsTypeModifiersInitAccessorsAndExtensionThis()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = TestAssemblyEmitter.Emit(directory, "Sample.Modifiers",
                """
                namespace Sample
                {
                    public static class Helpers
                    {
                        public static int Read(this Point point) => point.X;
                    }

                    public abstract class AbstractBase { }
                    public sealed class SealedType { }
                    public readonly struct ReadOnlyPoint { }
                    public struct Point
                    {
                        public int X { get; init; }
                        public int Y { get; set; }
                    }
                }
                """);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Modifiers", path)],
                [directory],
                []);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            var api = result.Document!.Assemblies.Single().Api;
            Assert.Contains(api, line => line.StartsWith("T:public static Class Sample.Helpers", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public abstract Class Sample.AbstractBase", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public sealed Class Sample.SealedType", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public readonly Struct Sample.ReadOnlyPoint", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public Struct Sample.Point", StringComparison.Ordinal));
            Assert.Contains(api, line => line.Contains("public static int Read(this Sample.Point point)", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("P:Sample.Point.public int X", StringComparison.Ordinal)
                && line.Contains("init:public", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("P:Sample.Point.public int Y", StringComparison.Ordinal)
                && line.Contains("set:public", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_ReportsForbiddenArrayIndexerAndMethodConstraintReferences()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var kernel = TestAssemblyEmitter.Emit(directory, "Origo.Core.Kernel",
                "namespace Kernel { public class Hidden { } }");
            var shell = TestAssemblyEmitter.Emit(directory, "Origo.Shell",
                """
                namespace Shell
                {
                    public class Visible
                    {
                        public Kernel.Hidden[] ArrayMethod() => null!;
                        public Kernel.Hidden[] ArrayProperty { get; set; } = null!;
                        public string this[Kernel.Hidden key] => "";
                        public void Generic<T>() where T : Kernel.Hidden { }
                    }
                }
                """,
                kernel);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Origo.Shell", shell)],
                [directory],
                ["Origo.Core.Kernel"]);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Contains("ArrayMethod", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("ArrayProperty", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("this[", StringComparison.Ordinal));
            Assert.Contains(result.Errors, error => error.Contains("Generic", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_RendersGenericVarianceInDeclarationsAndMemberPrefixes()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var path = TestAssemblyEmitter.Emit(directory, "Sample.Variance",
                """
                namespace Sample
                {
                    public interface IProducer<out T>
                    {
                        T Read();
                        System.Collections.Generic.IReadOnlyList<T> Items { get; }
                    }

                    public interface IConsumer<in T>
                    {
                        void Consume(T value);
                    }
                }
                """);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Variance", path)],
                [directory],
                []);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            var api = result.Document!.Assemblies.Single().Api;

            // Variance belongs to the generic parameter declaration; it must
            // appear on the type line and on member prefixes (which use the
            // same canonical type display).
            Assert.Contains(api, line => line.StartsWith("T:public Interface Sample.IProducer<out T>", StringComparison.Ordinal));
            Assert.Contains(api, line => line.StartsWith("T:public Interface Sample.IConsumer<in T>", StringComparison.Ordinal));
            Assert.Contains(api, line => line.Contains("Sample.IProducer<out T>.T Read()", StringComparison.Ordinal));
            Assert.Contains(api, line => line.Contains("Sample.IConsumer<in T>.void Consume(T value)", StringComparison.Ordinal));

            // Constructed uses (the IReadOnlyList<T> property type here) must
            // not inherit the variance marker of the declaration.
            Assert.True(
                api.Any(line => line.Contains("IReadOnlyList<T> Items", StringComparison.Ordinal)),
                "API lines:\n" + string.Join("\n", api));
            Assert.DoesNotContain(api, line => line.Contains("IReadOnlyList<out T>", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_RecordsNullableFlowAttributesInCanonicalOrder()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var systemRuntimePath = Path.Combine(runtimeDirectory, "System.Runtime.dll");
            Assert.True(File.Exists(systemRuntimePath), $"System.Runtime.dll not found at '{systemRuntimePath}'.");

            var path = TestAssemblyEmitter.Emit(directory, "Sample.Flow",
                """
                #nullable enable
                using System.Diagnostics.CodeAnalysis;

                namespace Sample
                {
                    public class Flow
                    {
                        private string? _field;

                        public bool TryRead([NotNullWhen(true)] out string? value)
                        {
                            value = null;
                            return false;
                        }

                        [return: MaybeNull]
                        public string Read() => null!;

                        [MemberNotNull(nameof(_field))]
                        [return: MaybeNull]
                        public string Create()
                        {
                            _field = "x";
                            return null!;
                        }
                    }
                }
                """,
                systemRuntimePath);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Flow", path)],
                [directory],
                []);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            var api = result.Document!.Assemblies.Single().Api;

            // Parameter attributes are rendered inline with canonical
            // fully-qualified names so the line stays a readable signature.
            Assert.Contains(api, line => line.Contains(
                "bool TryRead([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)", StringComparison.Ordinal));

            // Return-only attributes are grouped canonically; member-level
            // MemberNotNull attributes precede the return group.
            Assert.Contains(api, line => line.Contains(
                "[return: System.Diagnostics.CodeAnalysis.MaybeNull] public string Read()", StringComparison.Ordinal));
            Assert.Contains(api, line => line.Contains(
                "[System.Diagnostics.CodeAnalysis.MemberNotNull(\"_field\")] [return: System.Diagnostics.CodeAnalysis.MaybeNull] public string Create()",
                StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_AppliesParameterAttributesByPositionForOverlappingDisplays()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var systemRuntimePath = Path.Combine(runtimeDirectory, "System.Runtime.dll");
            Assert.True(File.Exists(systemRuntimePath), $"System.Runtime.dll not found at '{systemRuntimePath}'.");

            // "out string? val" is a prefix of the earlier "out string? value":
            // an isolated IndexOf would insert the later parameter's attribute in
            // front of the earlier parameter.
            var path = TestAssemblyEmitter.Emit(directory, "Sample.ParameterOrder",
                """
                #nullable enable
                using System.Diagnostics.CodeAnalysis;

                namespace Sample
                {
                    public class Reader
                    {
                        public bool TryRead(
                            out string? value,
                            [NotNullWhen(true)] out string? val)
                        {
                            value = null;
                            val = null;
                            return false;
                        }
                    }
                }
                """,
                systemRuntimePath);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Sample.ParameterOrder", path)],
                [directory],
                []);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            var line = Assert.Single(result.Document!.Assemblies.Single().Api,
                entry => entry.Contains("TryRead(", StringComparison.Ordinal));

            Assert.Contains(
                "out string? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? val",
                line,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "[System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value",
                line,
                StringComparison.Ordinal);
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void Build_RecordsFieldTargetedNullableFlowAttribute()
    {
        var directory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var systemRuntimePath = Path.Combine(runtimeDirectory, "System.Runtime.dll");
            var path = TestAssemblyEmitter.Emit(directory, "Sample.FieldFlow",
                """
                #nullable enable
                using System.Diagnostics.CodeAnalysis;

                namespace Sample
                {
                    public class Holder
                    {
                        [MaybeNull]
                        public string Field = null!;
                    }
                }
                """,
                systemRuntimePath);

            var result = InventoryBuilder.Build(
                [new AssemblyInput("Sample.FieldFlow", path)], [directory], []);

            Assert.True(result.Succeeded, string.Join('\n', result.Errors));
            var api = result.Document!.Assemblies.Single().Api;
            Assert.Contains(api, line => line.Contains(
                "[System.Diagnostics.CodeAnalysis.MaybeNull] public string Field", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(directory);
        }
    }

    [Fact]
    public void CreateFrameworkReferences_ReturnsOnlyManagedMetadata()
    {
        // On Windows the shared framework directory also contains native
        // libraries; creating metadata references for them throws
        // BadImageFormatException, so the API gate would fail only on that OS.
        // Production keeps this helper private; tests may inspect internals via
        // reflection instead of widening the production surface for convenience.
        var method = typeof(InventoryBuilder).GetMethod(
            "CreateFrameworkReferences",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var references = (Microsoft.CodeAnalysis.MetadataReference[])method.Invoke(null, null)!;

        Assert.NotEmpty(references);
        Assert.All(references, reference =>
            Assert.EndsWith(".dll", reference.Display, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_RejectsUnresolvedReferenceExplicitly()
    {
        var libraryDirectory = TestAssemblyEmitter.CreateDirectory();
        var shellDirectory = TestAssemblyEmitter.CreateDirectory();
        try
        {
            var library = TestAssemblyEmitter.Emit(libraryDirectory, "Sample.Library",
                "namespace Sample.Library { public class Hidden { } }");
            var shell = TestAssemblyEmitter.Emit(shellDirectory, "Sample.Shell",
                "namespace Sample.Shell { public class Visible { public Sample.Library.Hidden Create() => null!; } }",
                library);

            // The reference directory contains the shell assembly only, so the
            // library dependency cannot be resolved.
            var result = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Shell", shell)],
                [shellDirectory],
                []);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error =>
                error.Contains("unresolved type", StringComparison.Ordinal));
        }
        finally
        {
            TestAssemblyEmitter.TryDelete(libraryDirectory);
            TestAssemblyEmitter.TryDelete(shellDirectory);
        }
    }

    [Fact]
    public void ComparePrevious_NormalizesVersionOneAndKeepsRemovalDetection()
    {
        var previous = new InventoryDocument
        {
            SchemaVersion = 1,
            Assemblies =
            [
                new AssemblyInventory
                {
                    Name = "A",
                    Api =
                    [
                        "T:public Interface A.IProducer<T> : ",
                        "M:A.IProducer<T>.bool TryRead(out string? value)",
                    ],
                },
            ],
        };
        var enrichedCompatible = new InventoryDocument
        {
            SchemaVersion = 2,
            Assemblies =
            [
                new AssemblyInventory
                {
                    Name = "A",
                    Api =
                    [
                        "T:public Interface A.IProducer<out T> : ",
                        "M:A.IProducer<out T>.bool TryRead([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)",
                    ],
                },
            ],
        };

        Assert.Empty(InventoryComparer.ComparePrevious(previous, enrichedCompatible));

        var removed = new InventoryDocument
        {
            SchemaVersion = 2,
            Assemblies = [new AssemblyInventory { Name = "A", Api = [] }],
        };
        Assert.Contains(
            InventoryComparer.ComparePrevious(previous, removed),
            failure => failure.Contains("removed or changed API", StringComparison.Ordinal));
    }

    [Fact]
    public void ComparePrevious_SameSchemaDetectsVarianceAndAttributeChanges()
    {
        var previous = new InventoryDocument
        {
            Assemblies =
            [
                new AssemblyInventory
                {
                    Name = "A",
                    Api =
                    [
                        "T:public Interface A.IProducer<out T> : ",
                        "M:A.IProducer<out T>.bool TryRead([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)",
                    ],
                },
            ],
        };
        var varianceRemoved = new InventoryDocument
        {
            Assemblies =
            [
                new AssemblyInventory
                {
                    Name = "A",
                    Api =
                    [
                        "T:public Interface A.IProducer<T> : ",
                        "M:A.IProducer<out T>.bool TryRead([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)",
                    ],
                },
            ],
        };
        var attributeRemoved = new InventoryDocument
        {
            Assemblies =
            [
                new AssemblyInventory
                {
                    Name = "A",
                    Api =
                    [
                        "T:public Interface A.IProducer<out T> : ",
                        "M:A.IProducer<out T>.bool TryRead(out string? value)",
                    ],
                },
            ],
        };

        Assert.NotEmpty(InventoryComparer.ComparePrevious(previous, varianceRemoved));
        Assert.NotEmpty(InventoryComparer.ComparePrevious(previous, attributeRemoved));
    }

    [Fact]
    public void ProjectLineToSchemaOne_StripsOnlyEnrichedSegments()
    {
        Assert.Equal(
            "M:A.IProducer<T>.bool TryRead(out string? value)",
            InventoryComparer.ProjectLineToSchemaOne(
                "M:A.IProducer<out T>.bool TryRead([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)"));
        Assert.Equal(
            "M:A.Producer.bool TryRead(out string? value)",
            InventoryComparer.ProjectLineToSchemaOne(
                "M:A.Producer.bool TryRead([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)"));
    }

}
