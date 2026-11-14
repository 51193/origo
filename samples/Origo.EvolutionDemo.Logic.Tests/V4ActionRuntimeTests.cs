using System;
using Origo.EvolutionDemo.Logic;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4ActionRuntimeTests
{
    [Fact]
    public void ReflexBrain_ProducesExpressedMoveAction()
    {
        var phenotype = GenomeV4Compiler.Compile(
            GenomeV4Factory.CreateSimpleReflex(), ExpressionContextV4.Neutral);
        var brain = GenomeV4Compiler.CompileBrain(phenotype);
        var sensors = new float[64];
        sensors[(int)SensorKindV4.FoodAhead] = 0.8f;
        var actions = new float[64];
        DemoActionRuntime.Evaluate(phenotype, brain, sensors, actions);
        Assert.Equal(0.8f, actions[(int)ActionKindV4.Move], 3);
        Assert.Equal(0f, actions[(int)ActionKindV4.Spike]);
    }

    [Fact]
    public void UnexpressedActions_AreZeroed()
    {
        var phenotype = GenomeV4Compiler.Compile(
            GenomeV4Factory.CreateLogicTaskReflex(), ExpressionContextV4.Neutral);
        var brain = GenomeV4Compiler.CompileBrain(phenotype);
        var actions = new float[64];
        DemoActionRuntime.Evaluate(phenotype, brain, new float[64], actions);
        Assert.Equal(0f, actions[(int)ActionKindV4.Spike]);
        Assert.Equal(0f, actions[(int)ActionKindV4.Aim]);
    }
}
