using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Origo.EvolutionDemo.Analysis.IO;
using Origo.EvolutionDemo.Logic;

namespace Origo.EvolutionDemo.Analysis.Genome;

/// <summary>
///     Reusable spatial index over one save's positioned food entities. Spatial
///     metrics use Euclidean distance in pond world units; food without finite
///     coordinates is counted by the save summary but excluded here.
/// </summary>
internal sealed class FoodAccessIndex
{
    /// <summary>Nearest-food distance is capped at this value when no food exists within range.</summary>
    internal const double MaxDistance = 600d;

    private const double CellSize = 200d;
    private readonly Dictionary<(int X, int Y), List<FoodRecord>> _cells = [];

    internal FoodAccessIndex(IReadOnlyList<FoodRecord> foods)
    {
        ArgumentNullException.ThrowIfNull(foods);
        foreach (var food in foods)
        {
            if (!food.HasPosition)
                continue;
            var key = Cell(food.X, food.Y);
            if (!_cells.TryGetValue(key, out var bucket))
            {
                bucket = [];
                _cells[key] = bucket;
            }

            bucket.Add(food);
        }
    }

    /// <summary>Counts positioned food within <paramref name="radius" /> of a point.</summary>
    internal int CountWithin(double x, double y, double radius)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || radius <= 0d || _cells.Count == 0)
            return 0;
        var span = (int)System.Math.Ceiling(radius / CellSize);
        var (centerX, centerY) = Cell(x, y);
        var radiusSquared = radius * radius;
        var count = 0;
        for (var cellX = centerX - span; cellX <= centerX + span; cellX++)
            for (var cellY = centerY - span; cellY <= centerY + span; cellY++)
            {
                if (!_cells.TryGetValue((cellX, cellY), out var bucket))
                    continue;
                foreach (var food in bucket)
                {
                    var dx = food.X - x;
                    var dy = food.Y - y;
                    if ((dx * dx) + (dy * dy) <= radiusSquared)
                        count++;
                }
            }

        return count;
    }

    /// <summary>
    ///     Returns the nearest positioned-food distance, or <see cref="MaxDistance" />
    ///     when no food exists within that cap. Distances beyond the cap are not
    ///     distinguished.
    /// </summary>
    internal double NearestDistance(double x, double y, double maxDistance)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || _cells.Count == 0)
            return maxDistance;
        var limit = System.Math.Min(maxDistance, MaxDistance);
        var span = (int)System.Math.Ceiling(limit / CellSize);
        var (centerX, centerY) = Cell(x, y);
        var bestSquared = limit * limit;
        for (var cellX = centerX - span; cellX <= centerX + span; cellX++)
            for (var cellY = centerY - span; cellY <= centerY + span; cellY++)
            {
                if (!_cells.TryGetValue((cellX, cellY), out var bucket))
                    continue;
                foreach (var food in bucket)
                {
                    var dx = food.X - x;
                    var dy = food.Y - y;
                    var distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared < bestSquared)
                        bestSquared = distanceSquared;
                }
            }

        return System.Math.Sqrt(bestSquared);
    }

    private static (int X, int Y) Cell(double x, double y) =>
        ((int)System.Math.Floor(x / CellSize), (int)System.Math.Floor(y / CellSize));
}

/// <summary>Food inventory by type and corpse origin for one save.</summary>
/// <param name="Soft">Soft food entities.</param>
/// <param name="Tough">Tough food entities.</param>
/// <param name="Toxic">Toxic food entities.</param>
/// <param name="Corpse">Food entities released by corpses (a soft-food subset).</param>
public sealed record FoodTypeCounts(int Soft, int Tough, int Toxic, int Corpse)
{
    /// <summary>Total food entities, including food without a finite position.</summary>
    public int Total => Soft + Tough + Toxic;
}

/// <summary>Per-creature diet composition for one save.</summary>
/// <param name="Soft">Soft-food share of total meals (includes corpse food).</param>
/// <param name="Tough">Tough-food share of total meals.</param>
/// <param name="Toxic">Toxic-food share of total meals.</param>
/// <param name="CorpseOfSoft">Corpse-food share of soft meals; 0 when no soft meal was eaten.</param>
/// <param name="TotalMeals">Total soft+tough+toxic meals represented.</param>
public sealed record DietShares(double Soft, double Tough, double Toxic, double CorpseOfSoft, int TotalMeals);

/// <summary>Accessibility of positioned food to the creatures of one save.</summary>
/// <param name="Creatures">Parsed creatures in the save.</param>
/// <param name="FractionInPickup">Share with at least one food inside its pickup radius.</param>
/// <param name="FractionInSense">Share with at least one food inside its sense range.</param>
/// <param name="FractionIn300">Share with at least one food within 300 world units.</param>
/// <param name="MeanFoodInSense">Mean food count inside each creature's sense range.</param>
/// <param name="MeanFoodIn300">Mean food count within 300 world units.</param>
/// <param name="NearestFoodP50">Median nearest-food distance, capped at 600 units; null without creatures.</param>
/// <param name="NearestSourceP50">Median nearest food-source distance; null without sources.</param>
public sealed record FoodAccessSummary(
    int Creatures,
    double FractionInPickup,
    double FractionInSense,
    double FractionIn300,
    double MeanFoodInSense,
    double MeanFoodIn300,
    double? NearestFoodP50,
    double? NearestSourceP50);

/// <summary>World settings relevant to food production and patch coverage.</summary>
/// <param name="PondRadius">Pond radius from the serialized settings, or null.</param>
/// <param name="PatchRadius">Food-patch radius from the serialized settings, or null.</param>
/// <param name="ConfiguredPatchCount">Food-patch count serialized in world.config, or null.</param>
public sealed record WorldFoodConfig(
    double? PondRadius,
    double? PatchRadius,
    int? ConfiguredPatchCount);

/// <summary>
///     Ecology, access, origin, and diet metrics for one save. These metrics are
///     computed from every parsed creature, not from the optional --max sample, so
///     they describe the save rather than the clustering subset.
/// </summary>
/// <param name="SaveId">Short save identifier.</param>
/// <param name="Path">Scene file path.</param>
/// <param name="Time">Simulation time.</param>
/// <param name="Tick">Simulation tick.</param>
/// <param name="Food">Food inventory by type.</param>
/// <param name="LivePatchCount">Live controller <c>world.foodPatchCount</c>, or null.</param>
/// <param name="PatchRadius">Patch radius from settings, or null.</param>
/// <param name="PondRadius">Pond radius from settings, or null.</param>
/// <param name="PatchCoverage">Approximate patch area share of the pond, or null.</param>
/// <param name="Access">Food accessibility summary.</param>
/// <param name="FounderShare">Share of parsed creatures at generation 0.</param>
/// <param name="MeanGeneration">Mean generation of parsed creatures.</param>
/// <param name="MaxGeneration">Maximum generation observed on living creatures or the controller.</param>
/// <param name="LivingDiet">Diet composition summed over all parsed living creatures.</param>
/// <param name="ExperiencedDiet">Diet composition over creatures with at least 10 meals.</param>
/// <param name="ControllerDiet">Cumulative controller meal counters, or null.</param>
/// <param name="ExperiencedCreatures">Creatures with at least 10 meals.</param>
/// <param name="SpecialistShare60">Share of experienced creatures whose dominant food share exceeds 0.60.</param>
/// <param name="MeanExperiencedEntropy">Mean normalized diet entropy over experienced creatures; null without any.</param>
/// <param name="Warnings">Ecology-specific diagnostics (for example a stale serialized patch count).</param>
public sealed record SaveEcology(
    string SaveId,
    string Path,
    double Time,
    double Tick,
    FoodTypeCounts Food,
    int? LivePatchCount,
    double? PatchRadius,
    double? PondRadius,
    double? PatchCoverage,
    FoodAccessSummary Access,
    double FounderShare,
    double MeanGeneration,
    int MaxGeneration,
    DietShares LivingDiet,
    DietShares ExperiencedDiet,
    DietShares? ControllerDiet,
    int ExperiencedCreatures,
    double SpecialistShare60,
    double? MeanExperiencedEntropy,
    IReadOnlyList<string> Warnings);

/// <summary>Derives save-level ecology metrics from parsed v7 saves.</summary>
public static class SaveEcologyAnalyzer
{
    /// <summary>A creature needs at least this many meals to enter diet-specialization summaries.</summary>
    public const int MinimumDietMeals = 10;

    private const double DietEntropyLog3 = 1.0986122886681098d;

    /// <summary>analyzes every save in the supplied order.</summary>
    /// <exception cref="ArgumentNullException">The save list is null.</exception>
    public static IReadOnlyList<SaveEcology> Analyze(IReadOnlyList<SaveSnapshot> saves)
    {
        ArgumentNullException.ThrowIfNull(saves);
        var result = new List<SaveEcology>(saves.Count);
        foreach (var save in saves)
            result.Add(AnalyzeOne(save));
        return result;
    }

    private static SaveEcology AnalyzeOne(SaveSnapshot save)
    {
        var warnings = new List<string>();
        var config = ReadConfig(save, warnings);
        var livePatchCount = TryNumber(save.ControllerNumbers, "world.foodPatchCount", out var live)
            ? (int)System.Math.Max(0, live)
            : (int?)null;
        if (livePatchCount is { } liveCount
            && config.ConfiguredPatchCount is { } configured
            && configured != liveCount)
        {
            warnings.Add(string.Create(CultureInfo.InvariantCulture,
                $"live world.foodPatchCount={liveCount} differs from world.config "
                + $"FoodPatchCount={configured}; the live controller value is used for coverage."));
        }

        var patchCount = livePatchCount ?? config.ConfiguredPatchCount;
        var patchCoverage = patchCount is { } count
            && config.PatchRadius is { } radius
            && config.PondRadius is { } pondRadius
            && pondRadius > 0d
                ? count * radius * radius / (pondRadius * pondRadius)
                : (double?)null;

        var food = new FoodTypeCounts(
            save.Foods.Count(static item => item.Type == FoodType.Soft),
            save.Foods.Count(static item => item.Type == FoodType.Tough),
            save.Foods.Count(static item => item.Type == FoodType.Toxic),
            save.Foods.Count(static item => item.FromCorpse));
        var access = BuildAccess(save);
        var creatures = save.Creatures;
        var founders = creatures.Count(static creature => creature.Generation == 0);
        var meanGeneration = creatures.Count == 0 ? 0d : creatures.Average(static creature => (double)creature.Generation);
        var maxGeneration = TryNumber(save.ControllerNumbers, "evo.maxGeneration", out var controllerMax)
            ? (int)controllerMax
            : creatures.Count == 0 ? 0 : creatures.Max(static creature => creature.Generation);
        var livingDiet = SumDiet(creatures);
        var experienced = creatures
            .Where(static creature => TotalMeals(creature) >= MinimumDietMeals)
            .ToArray();
        var experiencedDiet = SumDiet(experienced);
        var specialistShare = experienced.Length == 0
            ? 0d
            : experienced.Count(creature => DominantShare(creature) > 0.60d) / (double)experienced.Length;
        double? meanEntropy = experienced.Length == 0
            ? null
            : experienced.Average(static creature => DietEntropy(creature));
        var controllerDiet = ReadControllerDiet(save);
        return new SaveEcology(
            save.Id,
            save.Path,
            save.Time,
            save.Tick,
            food,
            livePatchCount,
            config.PatchRadius,
            config.PondRadius,
            patchCoverage,
            access,
            creatures.Count == 0 ? 0d : founders / (double)creatures.Count,
            meanGeneration,
            maxGeneration,
            livingDiet,
            experiencedDiet,
            controllerDiet,
            experienced.Length,
            specialistShare,
            meanEntropy,
            warnings);
    }

    private static FoodAccessSummary BuildAccess(SaveSnapshot save)
    {
        var index = new FoodAccessIndex(save.Foods);
        var creatures = save.Creatures;
        if (creatures.Count == 0)
            return new FoodAccessSummary(0, 0d, 0d, 0d, 0d, 0d, null, null);
        var pickup = 0;
        var sense = 0;
        var within300 = 0;
        var foodInSense = 0d;
        var foodIn300 = 0d;
        var nearestFood = new List<double>(creatures.Count);
        var nearestSource = new List<double>(creatures.Count);
        foreach (var creature in creatures)
        {
            var pickupCount = index.CountWithin(creature.X, creature.Y, creature.FoodPickupRadius);
            var senseCount = index.CountWithin(creature.X, creature.Y, creature.SenseRange);
            var count300 = index.CountWithin(creature.X, creature.Y, 300d);
            pickup += pickupCount > 0 ? 1 : 0;
            sense += senseCount > 0 ? 1 : 0;
            within300 += count300 > 0 ? 1 : 0;
            foodInSense += senseCount;
            foodIn300 += count300;
            nearestFood.Add(index.NearestDistance(creature.X, creature.Y, FoodAccessIndex.MaxDistance));
            if (save.FoodSourceEntities.Count > 0)
            {
                var nearest = double.PositiveInfinity;
                foreach (var source in save.FoodSourceEntities)
                {
                    if (!source.HasPosition)
                        continue;
                    var dx = source.X - creature.X;
                    var dy = source.Y - creature.Y;
                    var distance = System.Math.Sqrt((dx * dx) + (dy * dy));
                    if (distance < nearest)
                        nearest = distance;
                }

                if (double.IsFinite(nearest))
                    nearestSource.Add(nearest);
            }
        }

        return new FoodAccessSummary(
            creatures.Count,
            pickup / (double)creatures.Count,
            sense / (double)creatures.Count,
            within300 / (double)creatures.Count,
            foodInSense / creatures.Count,
            foodIn300 / creatures.Count,
            Percentile(nearestFood, 0.5d),
            Percentile(nearestSource, 0.5d));
    }

    private static WorldFoodConfig ReadConfig(SaveSnapshot save, List<string> warnings)
    {
        if (!save.ControllerStrings.TryGetValue("world.config", out var json)
            || string.IsNullOrWhiteSpace(json))
            return new WorldFoodConfig(null, null, null);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new WorldFoodConfig(
                ReadDouble(root, "PondRadius"),
                ReadDouble(root, "FoodPatchRadius"),
                ReadInt(root, "FoodPatchCount"));
        }
        catch (JsonException)
        {
            warnings.Add("world.config is unparseable; patch coverage is unavailable.");
            return new WorldFoodConfig(null, null, null);
        }
    }

    private static double? ReadDouble(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && double.IsFinite(number)
            ? number
            : null;

    private static int? ReadInt(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : null;

    private static bool TryNumber(
        IReadOnlyDictionary<string, double> values, string key, out double value) =>
        values.TryGetValue(key, out value) && double.IsFinite(value);

    private static DietShares SumDiet(IReadOnlyList<CreatureRecord> creatures)
    {
        var soft = 0;
        var tough = 0;
        var toxic = 0;
        var corpse = 0;
        foreach (var creature in creatures)
        {
            soft += creature.SoftMeals;
            tough += creature.ToughMeals;
            toxic += creature.ToxicMeals;
            corpse += creature.CorpseMeals;
        }

        var total = soft + tough + toxic;
        return new DietShares(
            total == 0 ? 0d : soft / (double)total,
            total == 0 ? 0d : tough / (double)total,
            total == 0 ? 0d : toxic / (double)total,
            soft == 0 ? 0d : System.Math.Clamp(corpse / (double)soft, 0d, 1d),
            total);
    }

    private static DietShares? ReadControllerDiet(SaveSnapshot save)
    {
        if (!TryNumber(save.ControllerNumbers, "evo.foodSoftMeals", out var soft)
            || !TryNumber(save.ControllerNumbers, "evo.foodToughMeals", out var tough)
            || !TryNumber(save.ControllerNumbers, "evo.foodToxicMeals", out var toxic))
            return null;
        var corpse = TryNumber(save.ControllerNumbers, "evo.corpseMeals", out var corpseMeals)
            ? corpseMeals
            : 0d;
        var total = soft + tough + toxic;
        return new DietShares(
            total <= 0d ? 0d : soft / total,
            total <= 0d ? 0d : tough / total,
            total <= 0d ? 0d : toxic / total,
            soft <= 0d ? 0d : System.Math.Clamp(corpse / soft, 0d, 1d),
            (int)System.Math.Round(total));
    }

    private static int TotalMeals(CreatureRecord creature) =>
        creature.SoftMeals + creature.ToughMeals + creature.ToxicMeals;

    private static double DominantShare(CreatureRecord creature)
    {
        var total = TotalMeals(creature);
        if (total == 0)
            return 0d;
        return System.Math.Max(creature.SoftMeals, System.Math.Max(creature.ToughMeals, creature.ToxicMeals))
            / (double)total;
    }

    private static double DietEntropy(CreatureRecord creature)
    {
        var total = TotalMeals(creature);
        if (total == 0)
            return 0d;
        var entropy = 0d;
        foreach (var count in new[] { creature.SoftMeals, creature.ToughMeals, creature.ToxicMeals })
        {
            if (count <= 0)
                continue;
            var share = count / (double)total;
            entropy -= share * System.Math.Log(share);
        }

        return entropy / DietEntropyLog3;
    }

    private static double? Percentile(List<double> values, double fraction)
    {
        if (values.Count == 0)
            return null;
        var ordered = values.OrderBy(static value => value).ToArray();
        var position = (ordered.Length - 1) * fraction;
        var lower = (int)System.Math.Floor(position);
        var upper = System.Math.Min(lower + 1, ordered.Length - 1);
        return ordered[lower] + (position - lower) * (ordered[upper] - ordered[lower]);
    }
}
