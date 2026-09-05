using System;
using Godot;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotPackedSceneNodeFactoryIntegrationTests : IDeferredTestFixture, System.IDisposable
{
    private GodotPackedSceneNodeFactory? _factory;
    private Node _parent = null!;
    private int _frame;

    public bool IsComplete => _frame >= 1;
    public void Setup() => _frame = 0;
    public void AdvanceFrame() => _frame++;

    public void Dispose()
    {
        IntegrationTestRunner.FreeNode(_parent);
        _parent = null!;
        _factory = null;
        GC.SuppressFinalize(this);
    }

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

    [DeferredTest(Description = "Create with Godot-prohibited node-name characters throws before loading")]
    public void Create_InvalidNodeName_ThrowsBeforeLoading()
    {
        _parent = new Node();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_parent);
        _factory = new GodotPackedSceneNodeFactory(_parent);

        IntegrationTestRunner.AssertThrows<ArgumentException>(
            () => _factory.Create("bad.name", "res://nonexistent_scene.tscn"),
            "prohibited node name should fail before resource loading");
        IntegrationTestRunner.AssertEqual(
            0,
            _parent.GetChildCount(),
            "no child should be added for an invalid node name");
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
