using System;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;
using Origo.Core.Snd.Scene;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotSndManagerIntegrationTests
{
    [IntegrationTest(Description = "BindRuntimeDependencies called twice throws InvalidOperationException")]
    public void BindRuntimeDependencies_DoubleCall_Throws()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        IntegrationTestRunner.AssertThrows<InvalidOperationException>(
            () => harness.BindRuntimeDependencies(),
            "Double BindRuntimeDependencies should throw");
    }

    [IntegrationTest(Description = "BindContext before BindRuntimeDependencies throws InvalidOperationException")]
    public void BindContext_BeforeDeps_Throws()
    {
        using var harness = new IntegrationTestHarness();
        IntegrationTestRunner.AssertThrows<InvalidOperationException>(
            () => harness.BindContext(),
            "BindContext before BindRuntimeDependencies should throw");
    }

    [IntegrationTest(Description = "BindRuntimeDependencies with null world throws ArgumentNullException")]
    public void BindRuntimeDependencies_NullWorld_Throws()
    {
        using var harness = new IntegrationTestHarness();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => harness.SndManager.BindRuntimeDependencies(null!, harness.Logger),
            "null world should throw");
    }

    [IntegrationTest(Description = "BindRuntimeDependencies with null logger throws ArgumentNullException")]
    public void BindRuntimeDependencies_NullLogger_Throws()
    {
        using var harness = new IntegrationTestHarness();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => harness.SndManager.BindRuntimeDependencies(harness.SndWorld, null!),
            "null logger should throw");
    }

    [IntegrationTest(Description = "BindContext with null context throws ArgumentNullException")]
    public void BindContext_NullContext_Throws()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => ((ISndContextAttachableSceneHost)harness.SndManager).BindContext(null!),
            "null context should throw");
    }

    [IntegrationTest(Description = "ProcessAll with empty entity list does not throw")]
    public void ProcessAll_EmptyList_DoesNotThrow()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        ((ISndSceneHost)harness.SndManager).ProcessAll(0.016);
        IntegrationTestRunner.Assert(true, "ProcessAll on empty list should not throw.");
    }

    [IntegrationTest(Description = "ProcessAll drives entity Process for spawned entities")]
    public void ProcessAll_DrivesEntityProcess()
    {
        using var harness = new IntegrationTestHarness();
        harness.SndWorld.RegisterStrategy(() => new ProcessRecordingStrategy());
        harness.BindRuntimeDependencies();
        harness.BindContext();

        var meta = new SndMetaData
        {
            Name = "proc_entity",
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [ProcessRecordingStrategy.Index] }
        };
        ((ISndSceneHost)harness.SndManager).CreateEntity(meta);

        ProcessRecordingStrategy.Bind(0);
        ((ISndSceneHost)harness.SndManager).ProcessAll(0.016);

        IntegrationTestRunner.Assert(
            ProcessRecordingStrategy.ProcessCalls == 1,
            "ProcessAll should drive the entity's Process strategy once.");
    }

    private const string _processRecordingIndex = "test.process_recording";

    [StrategyIndex(_processRecordingIndex)]
    private sealed class ProcessRecordingStrategy : LifecycleStrategyBase
    {
        public const string Index = _processRecordingIndex;

        private static readonly AsyncLocal<int> _processCalls = new();

        public static int ProcessCalls => _processCalls.Value;

        public static void Bind(int seed) => _processCalls.Value = seed;

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => _processCalls.Value++;
    }
}
