using Origo.Core.Abstractions.Node;

namespace Origo.TestSupport;

public sealed class TestNodeHandle(string name) : INodeHandle
{
    public bool IsVisible { get; private set; } = true;
    public int FreeCount { get; private set; }

    public string Name { get; } = name;

    public void Free() => FreeCount++;

    public void SetVisible(bool visible) => IsVisible = visible;
}
