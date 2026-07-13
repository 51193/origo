using System;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Xunit;

namespace Origo.Core.Tests;

public class TopologyInvariantTests
{
    private static IBlackboard CreateBlackboard() => new Origo.Core.Blackboard.Blackboard();

    [Fact]
    public void EnsureActiveLevel_ValidTopology_DoesNotThrow()
    {
        var bb = CreateBlackboard();
        var raw = SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, "level_a", false);
        bb.SetValue(WellKnownKeys.SessionTopology, raw);

        TopologyInvariant.EnsureActiveLevel(bb, "level_a", "unit test");
    }

    [Fact]
    public void EnsureActiveLevel_MissingTopology_Throws()
    {
        var bb = CreateBlackboard();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TopologyInvariant.EnsureActiveLevel(bb, "level_a", "save id: '001'"));
        Assert.Contains(WellKnownKeys.SessionTopology, ex.Message, StringComparison.Ordinal);
        Assert.Contains("001", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureActiveLevel_EmptyTopology_Throws()
    {
        var bb = CreateBlackboard();
        bb.SetValue(WellKnownKeys.SessionTopology, "");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TopologyInvariant.EnsureActiveLevel(bb, "level_a", "unit test"));
        Assert.Contains(WellKnownKeys.SessionTopology, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureActiveLevel_WhitespaceTopology_Throws()
    {
        var bb = CreateBlackboard();
        bb.SetValue(WellKnownKeys.SessionTopology, "   ");

        Assert.Throws<InvalidOperationException>(() =>
            TopologyInvariant.EnsureActiveLevel(bb, "level_a", "unit test"));
    }

    [Fact]
    public void EnsureActiveLevel_MismatchedLevelId_Throws()
    {
        var bb = CreateBlackboard();
        var raw = SessionTopologyCodec.Serialize(ISessionManager.ForegroundKey, "level_a", false);
        bb.SetValue(WellKnownKeys.SessionTopology, raw);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TopologyInvariant.EnsureActiveLevel(bb, "different_level", "save id: '002'"));
        Assert.Contains(WellKnownKeys.SessionTopology, ex.Message, StringComparison.Ordinal);
        Assert.Contains("level_a", ex.Message, StringComparison.Ordinal);
        Assert.Contains("different_level", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureActiveLevel_NullBlackboard_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TopologyInvariant.EnsureActiveLevel(null!, "level_a", "unit test"));
    }

    [Fact]
    public void EnsureActiveLevel_EmptyExpectedLevelId_Throws()
    {
        var bb = CreateBlackboard();

        Assert.Throws<ArgumentException>(() =>
            TopologyInvariant.EnsureActiveLevel(bb, "", "unit test"));
        Assert.Throws<ArgumentException>(() =>
            TopologyInvariant.EnsureActiveLevel(bb, "   ", "unit test"));
    }

    [Fact]
    public void EnsureActiveLevel_CorruptedTopology_Throws()
    {
        var bb = CreateBlackboard();
        bb.SetValue(WellKnownKeys.SessionTopology, "not_a_valid_topology_string");

        Assert.Throws<InvalidOperationException>(() =>
            TopologyInvariant.EnsureActiveLevel(bb, "level_a", "unit test"));
    }
}
