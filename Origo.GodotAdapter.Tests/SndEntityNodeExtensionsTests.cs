using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.GodotAdapter;
using Origo.GodotAdapter.Snd;
using Xunit;

namespace Origo.GodotAdapter.Tests;

internal sealed class FakeNodeHandle : INodeHandle
{
    public string Name => "fake";
    public void Free() { }
    public void SetVisible(bool visible) { }
}

public class SndEntityNodeExtensionsTests
{
    [Fact]
    public void GetNativeNode_NonGodotHandle_ReturnsNull()
    {
        INodeHandle handle = new FakeNodeHandle();
        Assert.Null(handle.GetNativeNode());
    }

    [Fact]
    public void GetNodeFromSnd_NonGodotEntityWithoutRegisteredNode_Throws()
    {
        var entity = new InMemorySndEntity("Dummy");
        Assert.Throws<InvalidOperationException>(() => entity.GetNodeFromSnd<Godot.Node>("sprite"));
    }

    [Fact]
    public void GetNativeNode_NullHandle_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ((INodeHandle)null!).GetNativeNode());

    [Fact]
    public void GetNodeFromSnd_NullEntity_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ((ISndEntity)null!).GetNodeFromSnd<Godot.Node>("sprite"));
}
