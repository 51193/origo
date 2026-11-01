using System;
using Origo.EvolutionDemo.Logic;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class CollisionTests
{
    [Fact]
    public void Resolve_SeparatesOverlappingBodies()
    {
        var bodies = new[]
        {
            new CollisionBody { X = 0f, Y = 0f },
            new CollisionBody { X = 8f, Y = 0f },
        };

        var overlaps = CreatureCollision.Resolve(bodies, 9f, 500f, iterations: 2);

        Assert.True(overlaps >= 0);
        var dx = bodies[1].X - bodies[0].X;
        var dy = bodies[1].Y - bodies[0].Y;
        Assert.True((dx * dx) + (dy * dy) >= (18f * 18f) - 0.01f);
    }

    [Fact]
    public void ResolveSweep_SeparatesLargeCrowdAndClampsPond()
    {
        var random = new Random(31);
        var bodies = new CollisionBody[300];
        for (var i = 0; i < bodies.Length; i++)
        {
            bodies[i] = new CollisionBody
            {
                X = (float)((random.NextDouble() * 2d) - 1d) * 120f,
                Y = (float)((random.NextDouble() * 2d) - 1d) * 120f,
            };
        }

        CreatureCollision.ResolveSweep(bodies, 9f, 500f);

        foreach (var body in bodies)
            Assert.True((body.X * body.X) + (body.Y * body.Y) <= 491f * 491f);
    }

    [Fact]
    public void Resolve_LeavesNonOverlappingBodiesAlone()
    {
        var bodies = new[]
        {
            new CollisionBody { X = -100f, Y = 10f },
            new CollisionBody { X = 100f, Y = 10f },
        };

        CreatureCollision.Resolve(bodies, 9f, 500f, iterations: 2);

        Assert.Equal(-100f, bodies[0].X, 3);
        Assert.Equal(10f, bodies[0].Y, 3);
        Assert.Equal(100f, bodies[1].X, 3);
        Assert.Equal(10f, bodies[1].Y, 3);
    }

    [Fact]
    public void Resolve_KeepsBodiesInsidePond()
    {
        var bodies = new[]
        {
            new CollisionBody { X = 490f, Y = 0f },
            new CollisionBody { X = 488f, Y = 1f },
        };

        CreatureCollision.Resolve(bodies, 9f, 500f, iterations: 3);

        foreach (var body in bodies)
            Assert.True((body.X * body.X) + (body.Y * body.Y) <= 491f * 491f);
    }

    [Fact]
    public void Resolve_HandlesExactOverlapDeterministically()
    {
        var first = new[]
        {
            new CollisionBody { X = 20f, Y = 20f },
            new CollisionBody { X = 20f, Y = 20f },
        };
        var second = new[]
        {
            new CollisionBody { X = 20f, Y = 20f },
            new CollisionBody { X = 20f, Y = 20f },
        };

        CreatureCollision.Resolve(first, 9f, 500f, iterations: 2);
        CreatureCollision.Resolve(second, 9f, 500f, iterations: 2);

        Assert.Equal(first[0].X, second[0].X, 4);
        Assert.Equal(first[0].Y, second[0].Y, 4);
        Assert.NotEqual(first[0].X, first[1].X);
    }
}
