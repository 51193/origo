namespace Origo.Core.Snd.Scene;

/// <summary>
///     A scene host that can bind the strategy context at runtime.
///     After SessionRun is created, it binds the session context to the host,
///     ensuring entity strategies execute within the correct session.
/// </summary>
public interface ISndContextAttachableSceneHost
{
    void BindContext(ISndContext context);
}
