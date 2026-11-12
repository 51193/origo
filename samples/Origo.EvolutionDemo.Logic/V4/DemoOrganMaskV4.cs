using System;
using System.Collections.Generic;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>Stable bit mask for organ presence expressed by a V4 body.</summary>
[Flags]
public enum DemoOrganMaskV4
{
    None = 0,
    Spike = 1 << 0,
    Rhythm = 1 << 1,
    Vision = 1 << 2,
    CreatureSense = 1 << 3,
    SpikeBattery = 1 << 4,
    HardDigest = 1 << 5,
    ToxinResistance = 1 << 6,
    CarrionSense = 1 << 7,
    Sprint = 1 << 8,
}

/// <summary>Maps V4 organ instances to the demo's compact organ mask.</summary>
public static class DemoOrganMaskBuilder
{
    public static int FromOrgans(IReadOnlyList<OrganInstanceV4> organs)
    {
        ArgumentNullException.ThrowIfNull(organs);
        var mask = DemoOrganMaskV4.None;
        foreach (var organ in organs)
        {
            mask |= organ.Kind switch
            {
                OrganKindV4.SpikeOrgan => DemoOrganMaskV4.Spike,
                OrganKindV4.Rhythm => DemoOrganMaskV4.Rhythm,
                OrganKindV4.Vision => DemoOrganMaskV4.Vision,
                OrganKindV4.CreatureSense => DemoOrganMaskV4.CreatureSense,
                OrganKindV4.SpikeBattery => DemoOrganMaskV4.SpikeBattery,
                OrganKindV4.HardDigest => DemoOrganMaskV4.HardDigest,
                OrganKindV4.ToxinResistance => DemoOrganMaskV4.ToxinResistance,
                OrganKindV4.CarrionSense => DemoOrganMaskV4.CarrionSense,
                OrganKindV4.Sprint => DemoOrganMaskV4.Sprint,
                _ => DemoOrganMaskV4.None,
            };
        }

        return (int)mask;
    }
}
