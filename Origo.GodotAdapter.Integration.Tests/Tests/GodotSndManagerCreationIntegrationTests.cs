using Godot;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotSndManagerCreationIntegrationTests : IDeferredTestFixture
{
    private IntegrationTestHarness? _harness;
    private int _frame;

    public bool IsComplete => _frame >= 1;
    public void Setup() => _frame = 0;
    public void AdvanceFrame() => _frame++;

    private static SndMetaData CreateMeta(string name) => new()
    {
        Name = name,
        NodeMetaData = new NodeMetaData(),
        StrategyMetaData = new StrategyMetaData()
    };

    [DeferredTest(Description = "CreateEntity adds entity to list and scene tree")]
    public void CreateEntity_AfterBind_EntityInList()
    {
        _harness = new IntegrationTestHarness();
        _harness.BindRuntimeDependencies();
        _harness.BindContext();

        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(_harness.SndManager);

        var meta = CreateMeta("test_create");
        var entity = _harness.SndManager.CreateEntity(meta);

        IntegrationTestRunner.AssertNotNull(entity, "created entity");
        IntegrationTestRunner.AssertEqual("test_create", entity.Name, "entity.Name");
        IntegrationTestRunner.Assert(((Godot.Node)entity).IsInsideTree(), "Entity should be in scene tree.");

        var found = _harness.SndManager.FindByName("test_create");
        IntegrationTestRunner.AssertNotNull(found, "FindByName should find created entity.");
        IntegrationTestRunner.Assert(ReferenceEquals(entity, found), "Same entity instance");
    }

    [DeferredTest(Description = "CreateEntity then RemoveEntity removes from list")]
    public void RemoveEntity_RemovesFromList()
    {
        _harness = new IntegrationTestHarness();
        _harness.BindRuntimeDependencies();
        _harness.BindContext();

        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(_harness.SndManager);

        var meta = CreateMeta("test_remove");
        _harness.SndManager.CreateEntity(meta);
        _harness.SndManager.RemoveEntity("test_remove");

        IntegrationTestRunner.Assert(
            _harness.SndManager.FindByName("test_remove") is null,
            "Entity should not be found after removal.");
    }

    [DeferredTest(Description = "BuildMetaList includes created entities")]
    public void BuildMetaList_IncludesCreatedEntities()
    {
        _harness = new IntegrationTestHarness();
        _harness.BindRuntimeDependencies();
        _harness.BindContext();

        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(_harness.SndManager);

        _harness.SndManager.CreateEntity(CreateMeta("meta_a"));
        _harness.SndManager.CreateEntity(CreateMeta("meta_b"));

        var metaList = _harness.SndManager.BuildMetaList();
        IntegrationTestRunner.Assert(metaList.Count >= 2, "MetaList should contain at least 2 entries.");
    }

    [DeferredTest(Description = "RequestKillEntity marks entity pending kill")]
    public void RequestKillEntity_MarksPendingKill()
    {
        _harness = new IntegrationTestHarness();
        _harness.BindRuntimeDependencies();
        _harness.BindContext();

        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(_harness.SndManager);

        var meta = CreateMeta("test_kill");
        _harness.SndManager.CreateEntity(meta);
        _harness.SndManager.RequestKillEntity("test_kill");

        var entity = _harness.SndManager.FindByName("test_kill");
        IntegrationTestRunner.AssertNotNull(entity, "entity should still be findable after kill request.");
        IntegrationTestRunner.Assert(entity!.IsPendingKill, "Entity should be marked pending kill.");
    }

    [DeferredTest(Description = "GetEntities count reflects created entities")]
    public void GetEntities_CountReflectsCreated()
    {
        _harness = new IntegrationTestHarness();
        _harness.BindRuntimeDependencies();
        _harness.BindContext();

        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(_harness.SndManager);

        var countBefore = _harness.SndManager.GetEntities().Count;
        _harness.SndManager.CreateEntity(CreateMeta("count_test"));

        IntegrationTestRunner.Assert(
            _harness.SndManager.GetEntities().Count > countBefore,
            "GetEntities count should increase after CreateEntity.");
    }
}
