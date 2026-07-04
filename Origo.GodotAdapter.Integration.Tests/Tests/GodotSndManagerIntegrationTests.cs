using System;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;

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
            () => harness.SndManager.BindContext(null!),
            "null context should throw");
    }

    [IntegrationTest(Description = "ProcessAll with empty entity list does not throw")]
    public void ProcessAll_EmptyList_DoesNotThrow()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        harness.SndManager.ProcessAll(0.016);
        IntegrationTestRunner.Assert(true, "ProcessAll on empty list should not throw.");
    }

    [IntegrationTest(Description = "ProcessAll increments tick count")]
    public void ProcessAll_IncrementsTickCount()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        var before = harness.SndManager.ProcessTickCount;
        harness.SndManager.ProcessAll(0.016);
        IntegrationTestRunner.Assert(
            harness.SndManager.ProcessTickCount > before,
            "ProcessTickCount should increment after ProcessAll.");
    }
}
