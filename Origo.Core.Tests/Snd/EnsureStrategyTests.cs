using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Node;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class EnsureStrategyTests
{
    [Fact]
    public void EnsureStrategy_DataKeyMissing_SetsDataAndReturnsTrue()
    {
        var entity = new DummySndEntity("test");
        var result = entity.EnsureStrategy("character.path_impl", "character.path.astar");

        Assert.True(result);
        var (found, val) = entity.TryGetData<string>("character.path_impl");
        Assert.True(found);
        Assert.Equal("character.path.astar", val);
    }

    [Fact]
    public void EnsureStrategy_DataKeyExistsWithValue_ReturnsFalse()
    {
        var entity = new DummySndEntity("test");
        entity.SetData("character.path_impl", "character.path.direct");

        var result = entity.EnsureStrategy("character.path_impl", "character.path.astar");

        Assert.False(result);
        var (_, val) = entity.TryGetData<string>("character.path_impl");
        Assert.Equal("character.path.direct", val);
    }

    [Fact]
    public void EnsureStrategy_DataKeyExistsButEmpty_StillSetsAndReturnsTrue()
    {
        var entity = new DummySndEntity("test");
        entity.SetData("character.path_impl", "");

        var result = entity.EnsureStrategy("character.path_impl", "character.path.astar");

        Assert.True(result);
        var (_, val) = entity.TryGetData<string>("character.path_impl");
        Assert.Equal("character.path.astar", val);
    }

    [Fact]
    public void EnsureStrategy_AddStrategyThrows_DoesNotWriteDataKey()
    {
        // The marker must only be written after the strategy is successfully
        // mounted; a failed mount must not poison the idempotency marker.
        var entity = new ThrowingAddSndEntity("test");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            entity.EnsureStrategy("character.path_impl", "character.path.astar"));

        Assert.Equal("AddStrategy boom", ex.Message);
        var (found, _) = entity.TryGetData<string>("character.path_impl");
        Assert.False(found);
    }

    private sealed class ThrowingAddSndEntity(string entityName) : ISndEntity
    {
        private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal)
        {
            ["name"] = entityName
        };

        public string Name => entityName;
        public bool IsPendingKill => false;
        public ISessionRun OwningSession => throw new NotSupportedException();

        public void SetData<T>(string name, T value) => _data[name] = value;
        public T GetData<T>(string name) where T : notnull => throw new NotSupportedException();
        public (bool found, T? value) TryGetData<T>(string name)
        {
            if (_data.TryGetValue(name, out var stored) && stored is T typed)
                return (true, typed);
            return (false, default);
        }
        public bool TryGetData<T>(string name, out T? value)
        {
            if (_data.TryGetValue(name, out var stored) && stored is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }
        public void MountObserverStrategy(string targetName, string observerIndex) => throw new NotSupportedException();
        public void UnmountObserverStrategy(string targetName, string observerIndex) => throw new NotSupportedException();
        public void MountObserverStrategy(ISndEntity target, string observerIndex) => throw new NotSupportedException();
        public void UnmountObserverStrategy(ISndEntity target, string observerIndex) => throw new NotSupportedException();
        public INodeHandle GetNode(string name) => throw new NotSupportedException();
        public IReadOnlyCollection<string> GetNodeNames() => [];
        public void AddStrategy(string index) => throw new InvalidOperationException("AddStrategy boom");
        public void RemoveStrategy(string index) => throw new NotSupportedException();
        public void AddActiveStrategy(string index) => throw new NotSupportedException();
        public void RemoveActiveStrategy(string index) => throw new NotSupportedException();
        public object? InvokeStrategy(string strategyIndex, object? input = null) => throw new NotSupportedException();
    }
}
