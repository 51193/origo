using System;
using System.Collections.Generic;
using Godot;
using Origo.Core.Abstractions.Node;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests.TestSupport;

public sealed class StubNodeFactory(Node? parent = null) : INodeFactory
{
    private readonly Node? _parent = parent;

    public INodeHandle Create(string logicalName, string resourceId)
    {
        var node = new Node { Name = logicalName };
        _parent?.AddChild(node);
        return new GodotNodeHandle(node);
    }
}
