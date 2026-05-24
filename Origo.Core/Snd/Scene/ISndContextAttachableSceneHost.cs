namespace Origo.Core.Snd.Scene;

/// <summary>
///     可在运行时切换策略上下文的场景宿主。
///     SessionRun 创建后会把会话上下文绑定到宿主，确保实体策略在正确会话内执行。
/// </summary>
public interface ISndContextAttachableSceneHost
{
    void BindContext(ISndContext context);
}
