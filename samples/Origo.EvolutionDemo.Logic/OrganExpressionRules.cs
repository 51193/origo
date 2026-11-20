using System;
using System.Collections.Generic;
using System.Linq;
using Origo.EvolutionDemo.Logic.V4;

namespace Origo.EvolutionDemo.Logic;

/// <summary>
///     Expression rules that need more than one gene copy to decide what the
///     body looks like. These rules run after regulatory expression has selected
///     the active organs and before the energy budget is compiled.
/// </summary>
internal static class OrganExpressionRules
{
    /// <summary>
    ///     A body expresses at most one digestive specialization. When both the
    ///     hard-digest and toxin-resistance organs are active, the specialization
    ///     with the larger total organ size wins; equal totals prefer the hard
    ///     digest organ. The losing organs are removed from the expressed body,
    ///     so digesting both food types at full value requires a real
    ///     specialization choice instead of an unconditional upgrade.
    /// </summary>
    public static void ApplyDigestiveSpecialization(List<OrganInstanceV4> organs)
    {
        ArgumentNullException.ThrowIfNull(organs);
        var hardDigest = organs.Where(static organ => organ.Kind == OrganKindV4.HardDigest).ToList();
        var toxinResistance = organs.Where(static organ => organ.Kind == OrganKindV4.ToxinResistance).ToList();
        if (hardDigest.Count == 0 || toxinResistance.Count == 0)
            return;

        var hardDigestSize = hardDigest.Sum(static organ => organ.Size);
        var toxinResistanceSize = toxinResistance.Sum(static organ => organ.Size);
        var keepHardDigest = hardDigestSize >= toxinResistanceSize;
        var loser = keepHardDigest ? OrganKindV4.ToxinResistance : OrganKindV4.HardDigest;
        organs.RemoveAll(organ => organ.Kind == loser);
    }
}
