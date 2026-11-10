using System;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4DemoAdapterTests
{
    [Fact]
    public void DemoFounder_AdaptsToBodyPhenotype()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var genome = GenomeV4Factory.CreateDemoFounder(new Random(seed));
            var body = DemoPhenotypeAdapter.ToBodyPhenotype(genome);
            Assert.True(body.SizeScale > 0f);
            Assert.True(float.IsFinite(body.MoveSpeed));
            Assert.True(body.SenseRange > 0f);
            Assert.True(body.VisionOrgan);
            Assert.True(float.IsFinite(body.MaxAge));
        }
    }

    [Fact]
    public void Adapter_RoundTripsThroughCodec()
    {
        var genome = GenomeV4Factory.CreateDemoFounder(new Random(42));
        var restored = GenomeV4Codec.Deserialize(GenomeV4Codec.Serialize(genome));
        var a = DemoPhenotypeAdapter.ToBodyPhenotype(genome);
        var b = DemoPhenotypeAdapter.ToBodyPhenotype(restored);
        Assert.Equal(a, b);
    }
}
