using System.Collections.Generic;
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
    public void GetNodeFromSnd_NonGodotEntity_ReturnsNull()
    {
        var entity = new InMemorySndEntity("Dummy");
        Assert.Null(entity.GetNodeFromSnd<Godot.Node>("sprite"));
    }
}
