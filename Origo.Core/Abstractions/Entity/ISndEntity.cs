using Origo.Core.Abstractions.Lifecycle;

namespace Origo.Core.Abstractions.Entity;

public interface ISndEntity : ISndDataAccess, ISndNodeAccess, ISndStrategyAccess, ISndActiveStrategyAccess,
    ISndObserverStrategyAccess
{
    string Name { get; }

    bool IsPendingKill { get; }

    /// <summary>
    ///     该实体所属的会话。实体由 <see cref="ISessionRun" /> 的宿主创建时注入，
    ///     运行期间必非空；策略经此访问同会话实体与会话级能力。
    /// </summary>
    ISessionRun OwningSession { get; }
}
