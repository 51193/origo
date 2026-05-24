namespace Origo.Core.Abstractions.Node;

/// <summary>
///     抽象引擎节点句柄，Core 仅通过该接口触发基础节点行为。
/// </summary>
public interface INodeHandle
{
    string Name { get; }

    object Native { get; }

    void Free();

    void SetVisible(bool visible);
}
