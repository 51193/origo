using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Abstractions.Scene;

/// <summary>
///     场景宿主可通过此接口在会话构造时绑定归属会话，
///     以便后续实体创建时自动绑定 <see cref="ISndEntity.OwningSession" />。
/// </summary>
public interface IOwningSessionBindable
{
    void SetOwningSession(ISessionRun session);
}
