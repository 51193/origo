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
            var second = TestAssemblyEmitter.Emit(secondDirectory, "Sample.Api2",
                """
                namespace Sample { public class A { public void M() { } } public interface IZ { int Z { get; } } }
                """);

            var firstResult = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Api", first)],
                [directory],
                []);
            var secondResult = InventoryBuilder.Build(
                [new AssemblyInput("Sample.Api2", second)],
                [secondDirectory],
                []);

            Assert.True(firstResult.Succeeded, string.Join('\n', firstResult.Errors));
            Assert.True(secondResult.Succeeded, string.Join('\n', secondResult.Errors));
            Assert.Equal(
                InventoryJson.Serialize(firstResult.Document!),
                InventoryJson.Serialize(firstResult.Document!));
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
}
