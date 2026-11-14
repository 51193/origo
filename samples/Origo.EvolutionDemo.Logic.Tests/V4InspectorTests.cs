using System;
using System.Linq;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4InspectorTests
{
    [Fact]
    public void Inspection_ListsExpressedPortsAndOrgans()
    {
        PhenotypeV4? withCarrion = null;
        for (var seed = 0; seed < 100; seed++)
        {
            var compiled = GenomeV4Compiler.Compile(
                GenomeV4Factory.CreateDemoFounder(new Random(seed)),
                ExpressionContextV4.Neutral);
            if (compiled.SensorPorts.Contains((ushort)SensorKindV4.CorpseAhead))
            {
                withCarrion = compiled;
                break;
            }
        }

        Assert.NotNull(withCarrion);
        var inspection = PhenotypeInspectorV4.Inspect(withCarrion);
        Assert.Contains(inspection.Sensors, port => port.Id == (ushort)SensorKindV4.CorpseAhead);
        Assert.Contains(inspection.Actions, port => port.Id == (ushort)ActionKindV4.Move);
        Assert.True(inspection.NeuralNodes > 0);
        Assert.Contains("Carrion ahead", inspection.Describe());
    }
}
