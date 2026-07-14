using System;
using Godot;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotNodeHandleIntegrationTests
{
    [IntegrationTest(Description = "Constructor caches the node name")]
    public void Constructor_CachesNodeName()
    {
        var node = new Node
        {
            Name = "TestNode"
        };

        var handle = new GodotNodeHandle(node);

        IntegrationTestRunner.AssertEqual("TestNode", handle.Name, "handle.Name");
        node.Free();
    }

    [IntegrationTest(Description = "Free when node is valid calls Free and makes instance invalid")]
    public void Free_WhenNodeValid_MakesInstanceInvalid()
    {
        var node = new Node();
        var handle = new GodotNodeHandle(node);

        handle.Free();

        IntegrationTestRunner.Assert(
            !GodotObject.IsInstanceValid(node),
            "Node should not be valid after Free.");
    }

    [IntegrationTest(Description = "Double Free does not throw")]
    public void Free_WhenAlreadyFreed_DoesNotThrow()
    {
        var node = new Node();
        var handle = new GodotNodeHandle(node);
        handle.Free();

        handle.Free();
        IntegrationTestRunner.Assert(true, "Double Free should not throw.");
    }

    [IntegrationTest(Description = "SetVisible on CanvasItem toggles Visible property")]
    public void SetVisible_CanvasItem_SetsVisible()
    {
        var item = new Node2D();
        var handle = new GodotNodeHandle(item);

        handle.SetVisible(false);
        IntegrationTestRunner.Assert(!item.Visible, "CanvasItem should be invisible.");

        handle.SetVisible(true);
        IntegrationTestRunner.Assert(item.Visible, "CanvasItem should be visible.");

        item.Free();
    }

    [IntegrationTest(Description = "SetVisible on Node3D toggles Visible property")]
    public void SetVisible_Node3D_SetsVisible()
    {
        var node3d = new Node3D();
        var handle = new GodotNodeHandle(node3d);

        handle.SetVisible(false);
        IntegrationTestRunner.Assert(!node3d.Visible, "Node3D should be invisible.");

        handle.SetVisible(true);
        IntegrationTestRunner.Assert(node3d.Visible, "Node3D should be visible.");

        node3d.Free();
    }

    [IntegrationTest(Description = "SetVisible after Free throws ObjectDisposedException")]
    public void SetVisible_WhenNodeFreed_ThrowsObjectDisposedException()
    {
        var node = new Node2D();
        var handle = new GodotNodeHandle(node);
        node.Free();

        try
        {
            handle.SetVisible(false);
            IntegrationTestRunner.Assert(false, "Expected ObjectDisposedException was not thrown.");
        }
        catch (ObjectDisposedException)
        {
            IntegrationTestRunner.Assert(true, "SetVisible after Free threw ObjectDisposedException.");
        }
    }

    [IntegrationTest(Description = "UnsafeGetNode returns the original Node reference")]
    public void UnsafeGetNode_ReturnsOriginalReference()
    {
        var node = new Node();
        var handle = new GodotNodeHandle(node);

        var result = handle.UnsafeGetNode();

        IntegrationTestRunner.Assert(ReferenceEquals(node, result), "UnsafeGetNode should return the original Node.");
        node.Free();
    }
}
