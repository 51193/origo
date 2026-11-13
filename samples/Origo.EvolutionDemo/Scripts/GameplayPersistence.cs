using System;
using Origo.Core.Abstractions.Entity;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>
///     Single persistence boundary for the two progression scopes:
///     the account-wide meta profile and the per-world controller mirror.
/// </summary>
public sealed class GameplayPersistence
{
    private readonly EvolutionGameSession _session;

    public GameplayPersistence(EvolutionGameSession session)
    {
        _session = session;
    }

    public bool TryLoadProfile() => GameplayProfileStore.TryLoad(_session.Runtime);

    public void Flush(ISndEntity? controller)
    {
        if (controller is null)
        {
            GameplayProfileStore.Save(_session.Runtime.Progression);
            return;
        }

        GameplayStateSync.Save(controller, _session.Runtime);
    }

    public void FlushProfile() => GameplayProfileStore.Save(_session.Runtime.Progression);
}
