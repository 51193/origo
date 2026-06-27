using Origo.Core.Snd.Strategy;

namespace Origo.Core.Snd.Scene;

/// <summary>
///     持有 per-scene-host 观察者拓扑（<see cref="ObserverTopology" />）的场景宿主。
///     拓扑与 scene host 同生命周期：host 创建的所有实体共享同一个拓扑，
///     观察者绑定（谁观察谁）集中在此，而非分散在各实体内部。
///     仅由创建真实 <see cref="Origo.Core.Snd.Entity.SndEntity" /> 的宿主实现；
///     不创建真实实体的桩宿主无需实现此接口。
/// </summary>
internal interface IObserverTopologyHost
{
    ObserverTopology ObserverTopology { get; }
}
