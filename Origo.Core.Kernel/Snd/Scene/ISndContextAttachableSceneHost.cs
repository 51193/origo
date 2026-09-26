namespace Origo.Core.Snd.Scene;

/// <summary>
///     A scene host that can bind the strategy context at runtime.
///     After SessionRun is created, it binds the session context to the host,
///     ensuring entity strategies execute within the correct session.
/// </summary>
internal interface ISndContextAttachableSceneHost
{
    /// <summary>Binds the strategy context to the host so entity strategies execute within the correct session.</summary>
    void BindContext(ISndContext context);
}
