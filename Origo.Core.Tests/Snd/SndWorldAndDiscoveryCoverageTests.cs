using System;
using System.Collections.Generic;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class SndWorldAndDiscoveryCoverageTests
{
    [Fact]
    public void SndWorld_RegisterTypeMappings_NullCallback_Throws()
    {
        var world = TestFactory.CreateSndWorld();
        Assert.Throws<ArgumentNullException>(() => world.RegisterTypeMappings(null!));
    }

    [Fact]
    public void SndWorld_WriteMetaListNode_NonListEnumerable_UsesToListPath()
    {
        var world = TestFactory.CreateSndWorld();
        var lazy = MetaYield();
        using var node = world.WriteMetaListNode(lazy);
        var json = TestFactory.JsonFromNode(node);
        Assert.Contains("E1", json, StringComparison.Ordinal);

        static IEnumerable<SndMetaData> MetaYield()
        {
            yield return new SndMetaData
            {
                Name = "E1",
                NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(),
                DataMetaData = new DataMetaData()
            };
        }
    }

    [Fact]
    public void DiscoverAndRegisterStrategies_SkippingTestAssembly_DoesNotRegisterFromSkippedAssembly()
    {
        var logger = new TestLogger();
        var world = TestFactory.CreateSndWorld(logger: logger);
        var n = OrigoAutoInitializer.DiscoverAndRegisterStrategies(world, logger, new[] { "Origo.Core.Tests" });
        Assert.Equal(0, n);
    }

    [Fact]
    public void JsonCodec_DecodeJsonArrayRoot_ReadsElements()
    {
        var codec = TestFactory.CreateJsonCodec();
        using var root = codec.Decode("""[1, true, "hi"]""");
        Assert.Equal(DataSourceNodeKind.Array, root.Kind);
        Assert.Equal(1, root[0].AsInt());
        Assert.True(root[1].AsBool());
        Assert.Equal("hi", root[2].AsString());
    }
}
