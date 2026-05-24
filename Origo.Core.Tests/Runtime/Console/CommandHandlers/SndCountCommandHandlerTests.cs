using System.Collections.Generic;
using Origo.Core.Runtime.Console;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class SndCountCommandHandlerTests
{
    [Fact]
    public void SndCount_PublishesEntityCount()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var input = new ConsoleInputQueue();
        var output = new ConsoleOutputChannel();
        var bb = new Blackboard.Blackboard();
        var tm = new TypeStringMapping();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, input, output);

        // Spawn some entities
        runtime.Snd.Spawn(new SndMetaData
        {
            Name = "e1",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });
        runtime.Snd.Spawn(new SndMetaData
        {
            Name = "e2",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        });

        var received = new List<string>();
        output.Subscribe(line => received.Add(line));

        input.Enqueue("snd_count");
        runtime.Console!.ProcessPending();

        Assert.Contains(received, s => s.Contains("Snd count: 2"));
    }

    [Fact]
    public void SndCount_WithNoEntities_PublishesZero()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var input = new ConsoleInputQueue();
        var output = new ConsoleOutputChannel();
        var bb = new Blackboard.Blackboard();
        var tm = new TypeStringMapping();
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, input, output);

        var received = new List<string>();
        output.Subscribe(line => received.Add(line));

        input.Enqueue("snd_count");
        runtime.Console!.ProcessPending();

        Assert.Contains(received, s => s.Contains("Snd count: 0"));
    }
}
