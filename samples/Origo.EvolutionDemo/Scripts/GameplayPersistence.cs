using System;
using Origo.Core.Abstractions.Entity;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>Single persistence boundary for a box's controller progression mirror.</summary>
public sealed class GameplayPersistence
{
    private readonly EvolutionGameSession _session;

    public GameplayPersistence(EvolutionGameSession session)
    {
        _session = session;
    }

    public void Flush(ISndEntity? controller)
    {
        if (controller is not null)
            GameplayStateSync.Save(controller, _session.Runtime);
    }
}
