using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Save.Meta;
using Origo.Core.Snd.Metadata;
using Xunit;
using MemoryBlackboard = Origo.Core.Blackboard.Blackboard;

namespace Origo.Core.Tests;

public class SaveMetaMergerTests
{
    private static SaveMetaBuildContext DummyContext()
    {
        var progress = new MemoryBlackboard();
        var session = new MemoryBlackboard();
        return new SaveMetaBuildContext("s1", "lvl", progress, session, new NullSceneHost());
    }

    [Fact]
    public void Merge_ContributorsThenOverrides_OverridesWin()
    {
        var contributors = new ISaveMetaContributor[]
        {
            new FuncContributor((_, d) => d["a"] = "1"),
            new FuncContributor((_, d) => d["b"] = "2")
        };
        var overrides = new Dictionary<string, string> { ["a"] = "override", ["c"] = "3" };
        var merged = SaveMetaMerger.Merge(contributors, DummyContext(), overrides);
        Assert.NotNull(merged);
        Assert.Equal("override", merged!["a"]);
        Assert.Equal("2", merged["b"]);
        Assert.Equal("3", merged["c"]);
    }

    [Fact]
    public void Merge_LaterContributorOverwritesEarlierSameKey()
    {
        var contributors = new ISaveMetaContributor[]
        {
            new FuncContributor((_, d) => d["k"] = "first"),
            new FuncContributor((_, d) => d["k"] = "second")
        };
        var merged = SaveMetaMerger.Merge(contributors, DummyContext(), null);
        Assert.NotNull(merged);
        Assert.Equal("second", merged!["k"]);
    }

    [Fact]
    public void Merge_NoContributorsNoOverrides_ReturnsNull()
    {
        var merged = SaveMetaMerger.Merge(Array.Empty<ISaveMetaContributor>(), DummyContext(), null);
        Assert.Null(merged);
    }

    [Fact]
    public void Merge_SkipsNullOverrideValues()
    {
        var contributors = new ISaveMetaContributor[] { new FuncContributor((_, d) => d["x"] = "keep") };
        var overrides = new Dictionary<string, string> { ["x"] = null! };
        var merged = SaveMetaMerger.Merge(contributors, DummyContext(), overrides);
        Assert.NotNull(merged);
        Assert.Equal("keep", merged!["x"]);
    }

    private sealed class FuncContributor : ISaveMetaContributor
    {
        private readonly Action<SaveMetaBuildContext, IDictionary<string, string>> _action;

        public FuncContributor(Action<SaveMetaBuildContext, IDictionary<string, string>> action)
        {
            _action = action;
        }

        public void Contribute(in SaveMetaBuildContext context, IDictionary<string, string> target) =>
            _action(context, target);
    }

    private sealed class NullSceneHost : ISndSceneHost
    {
        public IReadOnlyList<SndMetaData> SerializeMetaList() => Array.Empty<SndMetaData>();

        public void LoadFromMetaList(IEnumerable<SndMetaData> metaList)
        {
        }

        public void ClearAll()
        {
        }

        public ISndEntity Spawn(SndMetaData metaData) => throw new NotSupportedException();

        public IReadOnlyCollection<ISndEntity> GetEntities() => Array.Empty<ISndEntity>();

        public ISndEntity? FindByName(string name) => null;

        public void ProcessAll(double delta)
        {
        }

        public void DeadByName(string name)
        {
        }
    }
}
