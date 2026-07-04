using System;
using Godot;
using Origo.Core.Snd.Strategy;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotSndEntityIntegrationTests
{
    private static (IntegrationTestHarness harness, GodotSndEntity entity) CreateEntity()
    {
        var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        var entity = new GodotSndEntity(
            harness.SndWorld,
            harness.SndManager.Context!,
            harness.Logger,
            harness.SndManager.GetObserverTopology(),
            _ => new StubNodeFactory());
        return (harness, entity);
    }

    [IntegrationTest(Description = "Constructor with null world throws ArgumentNullException")]
    public void Constructor_NullWorld_Throws()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => new GodotSndEntity(
                null!, harness.SndManager.Context!, harness.Logger,
                harness.SndManager.GetObserverTopology(), _ => new StubNodeFactory()),
            "null world should throw");
    }

    [IntegrationTest(Description = "Constructor with null context throws ArgumentNullException")]
    public void Constructor_NullContext_Throws()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => new GodotSndEntity(
                harness.SndWorld, null!, harness.Logger,
                harness.SndManager.GetObserverTopology(), _ => new StubNodeFactory()),
            "null context should throw");
    }

    [IntegrationTest(Description = "Constructor with null logger throws ArgumentNullException")]
    public void Constructor_NullLogger_Throws()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => new GodotSndEntity(
                harness.SndWorld, harness.SndManager.Context!, null!,
                harness.SndManager.GetObserverTopology(), _ => new StubNodeFactory()),
            "null logger should throw");
    }

    [IntegrationTest(Description = "Constructor with null topology throws ArgumentNullException")]
    public void Constructor_NullTopology_Throws()
    {
        using var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        IntegrationTestRunner.AssertThrows<ArgumentNullException>(
            () => new GodotSndEntity(
                harness.SndWorld, harness.SndManager.Context!, harness.Logger,
                null!, _ => new StubNodeFactory()),
            "null observer topology should throw");
    }

    [IntegrationTest(Description = "Constructor with valid args creates instance")]
    public void Constructor_ValidArgs_CreatesInstance()
    {
        var (harness, entity) = CreateEntity();
        using (harness)
        {
            entity.Name = "test_entity";
            IntegrationTestRunner.Assert(!string.IsNullOrEmpty(entity.Name), "Entity should have a name.");
            entity.Free();
        }
    }

    [IntegrationTest(Description = "SetData and GetData round-trip preserves int value")]
    public void SetData_GetData_RoundTrip()
    {
        var (harness, entity) = CreateEntity();
        using (harness)
        {
            entity.SetData("test_key", 42);
            var result = entity.GetData<int>("test_key");
            IntegrationTestRunner.AssertEqual(42, result, "GetData<int>");
            entity.Free();
        }
    }

    [IntegrationTest(Description = "TryGetData returns false for missing key")]
    public void TryGetData_MissingKey_ReturnsFalse()
    {
        var (harness, entity) = CreateEntity();
        using (harness)
        {
            var (found, _) = entity.TryGetData<int>("nonexistent");
            IntegrationTestRunner.Assert(!found, "TryGetData should return false for missing key.");
            entity.Free();
        }
    }

    [IntegrationTest(Description = "TryGetData returns false for type mismatch")]
    public void TryGetData_WrongType_ReturnsFalse()
    {
        var (harness, entity) = CreateEntity();
        using (harness)
        {
            entity.SetData("typed_key", "hello");
            var (found, _) = entity.TryGetData<int>("typed_key");
            IntegrationTestRunner.Assert(!found, "TryGetData should return false for type mismatch.");
            entity.Free();
        }
    }
}

internal static class GodotSndManagerExtensions
{
    internal static ObserverTopology GetObserverTopology(this GodotSndManager manager) => ((Origo.Core.Snd.Scene.IObserverTopologyHost)manager).ObserverTopology;
}
