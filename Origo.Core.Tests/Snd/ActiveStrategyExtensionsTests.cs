using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class ActiveStrategyExtensionsTests
{
    [Fact]
    public void InvokeStrategy_GenericWithInput_SerializesAndDeserializes()
    {
        var entity = new StubActiveStrategyEntity(o => "{ \"Result\": 42 }");
        var input = new { Sx = 1, Sz = 2 };

        var result = entity.InvokeStrategy<object, TestResult>(
            "test.find", input);

        Assert.NotNull(result);
        Assert.Equal(42, result.Result);
    }

    [Fact]
    public void InvokeStrategy_GenericNoInput_CallsWithoutInput()
    {
        var entity = new StubActiveStrategyEntity(o => "{ \"Result\": 99 }");

        var result = entity.InvokeStrategy<TestResult>("test.get");

        Assert.NotNull(result);
        Assert.Equal(99, result.Result);
    }

    [Fact]
    public void InvokeStrategy_NullResult_ReturnsDefault()
    {
        var entity = new StubActiveStrategyEntity(_ => null!);

        var result = entity.InvokeStrategy<object, TestResult>(
            "test.null", new { });

        Assert.Null(result);
    }

    [Fact]
    public void InvokeStrategy_GenericWithComplexNestedPayload_RoundTrips()
    {
        string? serializedInput = null;
        var entity = new StubActiveStrategyEntity(input =>
        {
            serializedInput = (string?)input;
            return new ComplexResult
            {
                Name = "boss",
                Stats = new Dictionary<string, int> { ["hp"] = 120, ["armor"] = 7 },
                Items =
                [
                    new ComplexItem { Id = 1, Kind = ComplexItemKind.Weapon },
                    new ComplexItem { Id = 2, Kind = ComplexItemKind.Armor },
                ],
                Kind = ComplexItemKind.Armor,
                Optional = null,
            };
        });

        var result = entity.InvokeStrategy<ComplexInput, ComplexResult>("test.complex", new ComplexInput
        {
            Name = "hero",
            Stats = new Dictionary<string, int> { ["level"] = 3 },
            Items = [new ComplexItem { Id = 9, Kind = ComplexItemKind.Weapon }],
            Kind = ComplexItemKind.Weapon,
            Optional = "kept",
        });

        Assert.NotNull(serializedInput);
        using var document = JsonDocument.Parse(serializedInput);
        var root = document.RootElement;
        Assert.Equal("hero", root.GetProperty("Name").GetString());
        Assert.Equal(3, root.GetProperty("Stats").GetProperty("level").GetInt32());
        Assert.Equal((int)ComplexItemKind.Weapon, root.GetProperty("Kind").GetInt32());
        Assert.Equal(9, root.GetProperty("Items")[0].GetProperty("Id").GetInt32());

        Assert.NotNull(result);
        Assert.Equal("boss", result.Name);
        Assert.Equal(120, result.Stats["hp"]);
        Assert.Equal(ComplexItemKind.Armor, result.Kind);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(ComplexItemKind.Armor, result.Items[1].Kind);
        Assert.Null(result.Optional);
    }

    private enum ComplexItemKind
    {
        Weapon,
        Armor,
    }

    private sealed class ComplexItem
    {
        public int Id { get; set; }
        public ComplexItemKind Kind { get; set; }
    }

    private sealed class ComplexInput
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, int> Stats { get; set; } = [];
        public List<ComplexItem> Items { get; set; } = [];
        public ComplexItemKind Kind { get; set; }
        public string? Optional { get; set; }
    }

    private sealed class ComplexResult
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, int> Stats { get; set; } = [];
        public List<ComplexItem> Items { get; set; } = [];
        public ComplexItemKind Kind { get; set; }
        public string? Optional { get; set; }
    }

    private sealed class TestResult
    {
        public int Result { get; set; }
    }

    private sealed class StubActiveStrategyEntity(System.Func<object?, object?> invokeResult) : Origo.Core.Abstractions.Entity.ISndEntity
    {
        public ISessionRun OwningSession { get; set; } = null!;
        private readonly System.Func<object?, object?> _invokeResult = invokeResult;

        public string Name => "stub";
        public bool IsPendingKill => false;

        public object? InvokeStrategy(string strategyIndex, object? input = null)
            => _invokeResult(input);

        public void SetData<T>(string name, T value) => throw new System.NotImplementedException();
        public T GetData<T>(string name) where T : notnull => throw new System.NotImplementedException();
        public (bool found, T? value) TryGetData<T>(string name) => throw new System.NotImplementedException();
        public void MountObserverStrategy(string targetName, string observerIndex) { }

        public bool TryGetData<T>(string name, out T? value)
        {
            var (found, stored) = TryGetData<T>(name);
            value = stored;
            return found;
        }

        public void UnmountObserverStrategy(string targetName, string observerIndex) { }
        public void MountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
        public void UnmountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
        public Origo.Core.Abstractions.Node.INodeHandle GetNode(string name) => throw new System.NotImplementedException();
        public IReadOnlyCollection<string> GetNodeNames() => throw new System.NotImplementedException();
        public void AddStrategy(string index) => throw new System.NotImplementedException();
        public void RemoveStrategy(string index) => throw new System.NotImplementedException();
        public void AddActiveStrategy(string index) => throw new System.NotImplementedException();
        public void RemoveActiveStrategy(string index) => throw new System.NotImplementedException();

    }
}
