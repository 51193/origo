using System.Numerics;
using Origo.EvolutionDemo.Logic.V4;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class V4ActionEffectTests
{
    [Fact]
    public void MoveEffect_ChangesPositionAlongHeading()
    {
        var state = new DemoActionEffectState
        {
            Position = Vector2.Zero,
            Heading = 0f,
            MoveSpeed = 100f,
            TurnSpeed = 2f,
            DeltaSeconds = 0.1f,
        };
        DemoActionEffectRegistry.Apply(
            GenomeV4Compiler.Compile(GenomeV4Factory.CreateSimpleReflex(), ExpressionContextV4.Neutral),
            CreateActions(1f, 0f),
            state);
        Assert.Equal(10f, state.Position.X, 3);
        Assert.Equal(0f, state.Position.Y, 3);
    }

    [Fact]
    public void TurnEffect_ChangesHeading()
    {
        var state = new DemoActionEffectState
        {
            Position = Vector2.Zero,
            Heading = 0f,
            MoveSpeed = 100f,
            TurnSpeed = 2f,
            DeltaSeconds = 0.1f,
        };
        DemoActionEffectRegistry.Apply(
            GenomeV4Compiler.Compile(GenomeV4Factory.CreateSimpleReflex(), ExpressionContextV4.Neutral),
            CreateActions(0f, 1f),
            state);
        Assert.Equal(0.2f, state.Heading, 3);
    }

    private static float[] CreateActions(float move, float turn)
    {
        var actions = new float[64];
        actions[(int)ActionKindV4.Move] = move;
        actions[(int)ActionKindV4.Turn] = turn;
        return actions;
    }
}
