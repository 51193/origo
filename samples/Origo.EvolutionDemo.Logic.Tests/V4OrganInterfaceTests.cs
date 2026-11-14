using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4OrganInterfaceTests
{
    [Fact]
    public void Registry_ProducesOrderedPortsForOrgans()
    {
        var organs = new List<OrganInstanceV4>
        {
            new(1, OrganKindV4.Sprint, 0, 1f),
            new(2, OrganKindV4.SpikeOrgan, 0, 1f),
            new(3, OrganKindV4.Vision, 0, 1f),
            new(4, OrganKindV4.CarrionSense, 0, 1f),
        };
        var sensors = OrganInterfaceRegistryV4.BuildSensorPorts(organs);
        var actions = OrganInterfaceRegistryV4.BuildActionPorts(organs);
        Assert.Contains((ushort)SensorKindV4.FoodAhead, sensors);
        Assert.Contains((ushort)SensorKindV4.CorpseAhead, sensors);
        Assert.Contains((ushort)ActionKindV4.Sprint, actions);
        Assert.Contains((ushort)ActionKindV4.Spike, actions);
        Assert.True(actions.IndexOf((ushort)ActionKindV4.Sprint)
            < actions.IndexOf((ushort)ActionKindV4.Move));
    }
}
