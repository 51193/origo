using System;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;
using Origo.GodotAdapter.Snd;
using Origo.Core.Snd.Scene;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotSndManagerInitializationTests
{
    private static IntegrationTestHarness CreateHarness()
    {
        var h = new IntegrationTestHarness();
        h.BindRuntimeDependencies();
        h.BindContext();
        return h;
    }

    [IntegrationTest(Description = "BindRuntimeDependencies with null world throws ArgumentNullException")]
    public void BindRuntimeDependencies_NullWorld_Throws()
    {
        using var harness = CreateHarness();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => new GodotSndManager().BindRuntimeDependencies(null!, harness.Logger),
            "null world should throw");
    }

    [IntegrationTest(Description = "BindRuntimeDependencies with null logger throws ArgumentNullException")]
    public void BindRuntimeDependencies_NullLogger_Throws()
    {
        using var harness = CreateHarness();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => new GodotSndManager().BindRuntimeDependencies(harness.SndWorld, null!),
            "null logger should throw");
    }

    [IntegrationTest(Description = "BindContext with null context throws ArgumentNullException")]
    public void BindContext_NullContext_Throws()
    {
        using var harness = CreateHarness();
        var manager = new GodotSndManager();
        manager.BindRuntimeDependencies(harness.SndWorld, harness.Logger);
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => ((ISndContextAttachableSceneHost)manager).BindContext(null!),
            "null context should throw");
    }

    [IntegrationTest(Description = "Chained BindRuntimeDependencies + BindContext with valid args does not throw")]
    public void BindDependenciesAndContext_ValidArgs_DoesNotThrow()
    {
        using var harness = CreateHarness();

        var freshManager = new GodotSndManager();
        freshManager.BindRuntimeDependencies(harness.SndWorld, harness.Logger);
        ((ISndContextAttachableSceneHost)freshManager).BindContext(harness.SndManager.Context!);

        IntegrationTestRunner.Assert(true, "chained BindRuntimeDependencies + BindContext should not throw with valid args.");
    }

    [IntegrationTest(Description = "CreateEntity before bind throws NotReady, not NullReferenceException")]
    public void CreateEntity_BeforeBind_ThrowsNotReadyNotNRE()
    {
        using var harness = CreateHarness();

        var manager = new GodotSndManager();
        var meta = new SndMetaData
        {
            Name = "unbound",
            NodeMetaData = new NodeMetaData(),
            DataMetaData = new DataMetaData(),
            StrategyMetaData = new StrategyMetaData()
        };

        IntegrationTestRunner.AssertThrows<InvalidOperationException>(
            () => ((ISndSceneHost)manager).CreateEntity(meta),
            "CreateEntity before bind must surface the NotReady contract error, not a NullReferenceException");
    }

    [IntegrationTest(Description = "RecoverFromMetaList before bind throws NotReady, not NullReferenceException")]
    public void RecoverFromMetaList_BeforeBind_ThrowsNotReadyNotNRE()
    {
        using var harness = CreateHarness();

        var manager = new GodotSndManager();
        var meta = new SndMetaData
        {
            Name = "unbound",
            NodeMetaData = new NodeMetaData(),
            DataMetaData = new DataMetaData(),
            StrategyMetaData = new StrategyMetaData()
        };

        IntegrationTestRunner.AssertThrows<InvalidOperationException>(
            () => ((ISndSceneAccess)manager).RecoverFromMetaList([meta]),
            "RecoverFromMetaList before bind must surface the NotReady contract error, not a NullReferenceException");
    }
}
