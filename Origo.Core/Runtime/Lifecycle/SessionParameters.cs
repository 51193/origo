using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;

namespace Origo.Core.Runtime.Lifecycle;

/// <summary>
///     Configuration parameters required for starting a SessionRun.
///     <para>
///         <see cref="IsFrontSession" /> is assigned by <see cref="SessionManager" /> at creation time
///         and indicates whether this session is the foreground session. This flag is pinned into the runtime
///         after SessionRun construction; strategy hooks retrieve it via <see cref="ISessionRun.IsFrontSession" />.
///     </para>
/// </summary>
internal readonly record struct SessionParameters(
    string LevelId,
    IBlackboard SessionBlackboard,
    ISndSceneHost SceneHost,
    bool IsFrontSession = false);
