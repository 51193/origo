using System;
using Origo.GodotAdapter.Bootstrap;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotSndBootstrapIntegrationTests
{
    private static IntegrationTestHarness CreateHarness()
    {
        var h = new IntegrationTestHarness();
        h.BindRuntimeDependencies();
        h.BindContext();
        return h;
    }

    [IntegrationTest(Description = "BindRuntimeAndContext with null manager throws ArgumentNullException")]
    public void BindRuntimeAndContext_NullManager_Throws()
    {
        using var harness = CreateHarness();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => GodotSndBootstrap.BindRuntimeAndContext(
                null!, harness.SndWorld, harness.Logger, harness.SndManager.Context!),
            "null manager should throw");
    }

    [IntegrationTest(Description = "BindRuntimeAndContext with null world throws ArgumentNullException")]
    public void BindRuntimeAndContext_NullWorld_Throws()
    {
        using var harness = new IntegrationTestHarness();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => GodotSndBootstrap.BindRuntimeAndContext(
                harness.SndManager, null!, harness.Logger, null!),
            "null world should throw");
    }

    [IntegrationTest(Description = "BindRuntimeAndContext with valid args does not throw")]
    public void BindRuntimeAndContext_ValidArgs_DoesNotThrow()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();

        // Use a fresh manager with no prior bindings.
        var freshManager = new Origo.GodotAdapter.Snd.GodotSndManager();
        GodotSndBootstrap.BindRuntimeAndContext(
            freshManager,
            harness.SndWorld,
            harness.Logger,
            harness.SndManager.Context!);

        IntegrationTestRunner.Assert(true, "BindRuntimeAndContext should not throw with valid args.");
    }
}
