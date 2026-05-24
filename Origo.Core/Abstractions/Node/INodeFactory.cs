namespace Origo.Core.Abstractions.Node;

/// <summary>
///     按资源标识创建节点实例并挂载到宿主。
/// </summary>
public interface INodeFactory
{
    INodeHandle Create(string logicalName, string resourceId);
}
