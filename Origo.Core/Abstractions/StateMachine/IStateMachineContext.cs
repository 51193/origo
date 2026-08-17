using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Snd;

namespace Origo.Core.Abstractions.StateMachine;

/// <summary>
///     Minimum runtime context interface required by state machine strategy hooks.
///     Contains no foreground / background semantics; all members have equivalent
///     meaning in both session types.
///     <para>
///         <strong>Implementation notes:</strong>
///         <list type="bullet">
///             <item>
///                 <description>
///                     <see cref="Snd.SndContext" /> provides the global / progress-level default
///                     implementation, where <see cref="SessionBlackboard" /> and
///                     <see cref="SceneAccess" /> point to the foreground session.
///                     This implementation is only used as the context entry for progress-level
///                     state machines; session-level state machines do not use it directly.
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     <see cref="SessionStateMachineContext" /> is a session-level adapter
///                     that binds <see cref="SessionBlackboard" /> and <see cref="SceneAccess" />
///                     to the current session, ensuring that state machine hooks in both
///                     foreground and background sessions point to their respective session
///                     data — no semantic divergence between foreground and background.
///                 </description>
///             </item>
///         </list>
///     </para>
///     Background or test scenarios can supply alternative implementations,
///     completely decoupling from the specific foreground context.
///     <para>
///         <strong>Interface inheritance:</strong>
///         Inherits <see cref="ISndBlackboardAccess" /> for system/progress blackboard access,
///         and <see cref="ISndDeferredActions" /> for the deferred action queue.
///     </para>
/// </summary>
public interface IStateMachineContext : ISndBlackboardAccess, ISndDeferredActions
{
    /// <summary>Current session blackboard; null when no session is active.</summary>
    IBlackboard? SessionBlackboard { get; }

    /// <summary>Read-only SND scene access for the current session; foreground and background sessions each return their own scene host.</summary>
    ISndSceneReadAccess SceneAccess { get; }
}
