using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Origo.EvolutionDemo.Logic.V4;

/// <summary>A candidate target for one spike salvo.</summary>
public readonly record struct SpikeTargetV4(
    int Id,
    Vector2 Position,
    float Energy,
    float BodyRadius,
    bool PendingKill);

/// <summary>Inputs for planning one directional spike salvo.</summary>
public sealed record SpikePlanInputV4(
    int SelfId,
    Vector2 Position,
    float Heading,
    float AimDrive,
    float Range,
    bool Battery,
    int Count,
    float Spread,
    float Jitter,
    IReadOnlyList<SpikeTargetV4> Targets,
    Random Random);

/// <summary>Planned salvo: target ids, distance cost, fan endpoints.</summary>
public sealed record SpikePlanV4(
    int PelletCount,
    IReadOnlyList<int> TargetIds,
    float Cost,
    float MaxDistance,
    string Fan);

/// <summary>
///     Pure planning for the spike action command. Target selection, fan
///     randomness, and distance cost live here; the simulation applies the
///     energy, kills, corpses, and cooldowns.
/// </summary>
public static class DemoSpikeActionCommand
{
    public static SpikePlanV4 Plan(SpikePlanInputV4 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Targets);
        ArgumentNullException.ThrowIfNull(input.Random);
        var count = input.Battery ? Math.Clamp(input.Count, 2, 8) : 1;
        var spread = input.Battery ? MathF.Max(0f, input.Spread) : 0f;
        var jitter = input.Battery ? MathF.Max(0f, input.Jitter) : 0f;
        var aimAngle = input.Heading + (input.AimDrive * Tuning.SpikeAimHalfAngleRadians);
        var targetIds = new int[count];
        var ends = new string[count];
        var hitTargets = new int[count];
        var hitCount = 0;
        var maxDistance = 0f;
        for (var pellet = 0; pellet < count; pellet++)
        {
            var fanOffset = count == 1
                ? 0f
                : -spread * 0.5f + (spread * ((pellet + 0.5f) / count));
            var angle = aimAngle + fanOffset
                + ((float)((input.Random.NextDouble() * 2d) - 1d) * jitter);
            var aimX = MathF.Cos(angle);
            var aimY = MathF.Sin(angle);
            var targetIndex = -1;
            var targetDistance = float.MaxValue;
            foreach (var candidate in input.Targets)
            {
                if (candidate.Id == input.SelfId || candidate.PendingKill)
                    continue;
                var alreadyHit = false;
                for (var h = 0; h < hitCount; h++)
                {
                    if (hitTargets[h] != candidate.Id)
                        continue;
                    alreadyHit = true;
                    break;
                }

                if (alreadyHit)
                    continue;
                var dx = candidate.Position.X - input.Position.X;
                var dy = candidate.Position.Y - input.Position.Y;
                var along = (dx * aimX) + (dy * aimY);
                if (along <= 0f || along > input.Range || along >= targetDistance)
                    continue;
                var perpendicularSquared = (dx * dx) + (dy * dy) - (along * along);
                var hitRadius = candidate.BodyRadius + Tuning.SpikeBeamRadius;
                if (perpendicularSquared > hitRadius * hitRadius)
                    continue;
                targetIndex = candidate.Id;
                targetDistance = along;
            }

            var end = new Vector2(
                input.Position.X + (aimX * input.Range),
                input.Position.Y + (aimY * input.Range));
            if (targetIndex >= 0)
            {
                targetIds[pellet] = targetIndex;
                hitTargets[hitCount++] = targetIndex;
                maxDistance = MathF.Max(maxDistance, targetDistance);
                end = new Vector2(
                    input.Position.X + (aimX * targetDistance),
                    input.Position.Y + (aimY * targetDistance));
            }
            else
            {
                targetIds[pellet] = -1;
            }

            ends[pellet] = string.Create(
                CultureInfo.InvariantCulture,
                $"{end.X:0.###},{end.Y:0.###}");
        }

        var shotDistance = maxDistance > 0f ? maxDistance : input.Range;
        var cost = Tuning.SpikeEnergyCost
            + (Tuning.SpikePerPelletCost * count)
            + (Tuning.SpikeEnergyPerUnit * shotDistance);
        return new SpikePlanV4(count, targetIds, cost, shotDistance, string.Join(';', ends));
    }
}
