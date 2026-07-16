using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     A scene host that holds a per-scene-host observer topology
///     (<see cref="ObserverTopology" />).
///     The topology shares the same lifetime as the scene host: all entities created by the host
///     share the same topology, and observer bindings (who observes whom) are centralized here
///     rather than scattered within individual entities.
///     Only implemented by hosts that create real <see cref="Origo.Core.Snd.Entity.SndEntity" />
///     instances; stub hosts that do not create real entities need not implement this interface.
/// </summary>
internal interface IObserverTopologyHost
{
    ObserverTopology ObserverTopology { get; }
}
