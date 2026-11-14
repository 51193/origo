using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4InterfaceRegistryTests
{
    [Fact]
    public void CarrionSensor_IsRegistered()
    {
        Assert.True(InterfaceRegistryV4.SensorDefinitions.ContainsKey((ushort)SensorKindV4.CorpseAhead));
        Assert.True(InterfaceRegistryV4.ActionDefinitions.ContainsKey((ushort)ActionKindV4.Spike));
    }

    [Fact]
    public void CustomSensor_CanBeRegisteredWithoutChangingGenomeFormat()
    {
        const ushort id = 5000;
        InterfaceRegistryV4.RegisterSensor(new SensorDefinitionV4(id, "test.sensor", "Test sensor"));
        Assert.True(InterfaceRegistryV4.SensorDefinitions.TryGetValue(id, out var definition));
        Assert.Equal("test.sensor", definition.Key);
    }
}
