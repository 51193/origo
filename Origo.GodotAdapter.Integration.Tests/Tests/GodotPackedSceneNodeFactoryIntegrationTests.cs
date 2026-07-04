using Godot;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotPackedSceneNodeFactoryIntegrationTests : IDeferredTestFixture
{
    private GodotPackedSceneNodeFactory? _factory;
    private Node _parent = null!;
    private int _frame;

    public bool IsComplete => _frame >= 1;
    public void Setup() => _frame = 0;
    public void AdvanceFrame() => _frame++;

    [DeferredTest(Description = "Create loads a scene and returns a valid node handle")]
    public void Create_ValidScene_ReturnsNodeHandle()
    {
        _parent = new Node();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_parent);
        _factory = new GodotPackedSceneNodeFactory(_parent);

        var handle = _factory.Create("test_node", "res://TestScenes/test_empty_node.tscn");
        IntegrationTestRunner.AssertNotNull(handle, "node handle");
        IntegrationTestRunner.AssertEqual("test_node", handle.Name, "handle.Name");
        handle.Free();
    }

    [DeferredTest(Description = "Create with nonexistent resource throws InvalidOperationException")]
    public void Create_InvalidSceneId_Throws()
    {
        _parent = new Node();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_parent);
        _factory = new GodotPackedSceneNodeFactory(_parent);

        IntegrationTestRunner.AssertThrows<System.InvalidOperationException>(
            () => _factory.Create("bad", "res://nonexistent_scene.tscn"),
            "nonexistent scene should throw");
    }

    [DeferredTest(Description = "Create adds child node to parent")]
    public void Create_AddsChildToParent()
    {
        _parent = new Node();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_parent);
        _factory = new GodotPackedSceneNodeFactory(_parent);

        _factory.Create("child_node", "res://TestScenes/test_empty_node.tscn");
        IntegrationTestRunner.Assert(
            _parent.GetChildCount() > 0,
            "Parent should have at least one child after Create.");
    }

    [DeferredTest(Description = "Create with the same resource twice reuses cached scene")]
    public void Create_SameSceneId_CreatesMultipleNodes()
    {
        _parent = new Node();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_parent);
        _factory = new GodotPackedSceneNodeFactory(_parent);

        _factory.Create("first", "res://TestScenes/test_empty_node.tscn");
        _factory.Create("second", "res://TestScenes/test_empty_node.tscn");
        IntegrationTestRunner.AssertEqual(2, _parent.GetChildCount(), "child count for two creates");
    }
}
