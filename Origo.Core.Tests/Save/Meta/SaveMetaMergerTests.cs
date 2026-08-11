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
    public void Merge_LaterContributorOverwritesEarlierSameKey()
    {
        var contributors = new ISaveMetaContributor[]
        {
            new FuncContributor(_ => new Dictionary<string, string> { ["k"] = "first" }),
            new FuncContributor(_ => new Dictionary<string, string> { ["k"] = "second" })
        };
        var merged = SaveMetaMerger.Merge(contributors, DummyContext());
        Assert.NotNull(merged);
        Assert.Equal("second", merged!["k"]);
    }

    [Fact]
    public void Merge_NoContributors_ReturnsNull()
    {
        var merged = SaveMetaMerger.Merge([], DummyContext());
        Assert.Null(merged);
    }

    private sealed class FuncContributor(Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> func) : ISaveMetaContributor
    {
        private readonly Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> _func = func;

        public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context) =>
            _func(context);
    }

    private sealed class NullSceneHost : ISndSceneHost
    {
        public IReadOnlyList<SndMetaData> BuildMetaList() => [];

        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
        {
        }

        public void RemoveAllEntities()
        {
        }

        public ISndEntity CreateEntity(SndMetaData metaData) => throw new NotSupportedException();

        public IReadOnlyCollection<ISndEntity> GetEntities() => [];

        public ISndEntity? FindByName(string name) => null;

        public void ProcessAll(double delta)
        {
        }

        public void RemoveEntity(string name)
        {
        }

        public void RequestKillEntity(string name)
        {
        }
    }
}
