using System;
using Origo.Core.Abstractions.Lifecycle;
using Origo.EvolutionDemo.Logic.Gameplay;

namespace Origo.EvolutionDemo;

/// <summary>Stores box identity/profile on the session blackboard and controller.</summary>
public static class BoxProfileCodec
{
    private const string IdKey = "box.id";
    private const string ProfileKey = "box.profile";
    private const string UnlocksKey = "box.unlocks";
    private const string SeedKey = "box.seed";
    private const string RootBaseKey = "box.rootBase";

    /// <summary>Persists the box profile on its session and controller.</summary>
    /// <param name="session">Mounted box session.</param>
    /// <param name="box">Box to persist.</param>
    public static void Save(ISessionRun session, EcologyBox box)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(box);
        session.SessionBlackboard.SetValue(IdKey, box.Id);
        session.SessionBlackboard.SetValue(ProfileKey, box.Profile.Id);
        session.SessionBlackboard.SetValue(UnlocksKey, string.Join(';', box.Profile.UnlockIds));
        session.SessionBlackboard.SetValue(SeedKey, box.Seed);
        session.SessionBlackboard.SetValue(RootBaseKey, box.RootBase);
        if (box.Controller is { } controller)
        {
            controller.SetData(DemoKeys.BoxId, box.Id);
            controller.SetData(DemoKeys.BoxProfile, box.Profile.Id);
            controller.SetData(DemoKeys.BoxSeed, box.Seed);
            controller.SetData(DemoKeys.BoxRootBase, box.RootBase);
        }
    }

    /// <summary>Reads a box profile, preferring the session blackboard and falling back to the controller mirror.</summary>
    /// <param name="session">Mounted box session.</param>
    /// <param name="id">Stable box id when found.</param>
    /// <param name="profile">Box unlock profile when found.</param>
    /// <param name="seed">Simulation seed when found.</param>
    /// <param name="rootBase">Root namespace base when found.</param>
    /// <returns><see langword="true" /> when a valid profile was found.</returns>
    public static bool TryRead(
        ISessionRun session,
        out string id,
        out BoxUnlockProfile profile,
        out int seed,
        out int rootBase)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (TryReadBlackboard(session, out id, out profile, out seed, out rootBase))
            return true;
        if (session.FindByName(DemoEntry.ControllerName) is not { } controller)
            return false;
        if (controller.TryGetData<string>(DemoKeys.BoxId) is not { found: true, value: { } boxId }
            || controller.TryGetData<string>(DemoKeys.BoxProfile) is not { found: true, value: { } profileId }
            || controller.TryGetData<string>(DemoKeys.GameplayUnlocked) is not { found: true, value: { } unlocks }
            || controller.TryGetData<int>(DemoKeys.BoxSeed) is not { found: true, value: var boxSeed }
            || controller.TryGetData<int>(DemoKeys.BoxRootBase) is not { found: true, value: var boxRootBase })
            return false;
        id = boxId;
        profile = BoxUnlockProfile.Create(profileId, unlocks.Split(';', StringSplitOptions.RemoveEmptyEntries));
        seed = boxSeed;
        rootBase = boxRootBase;
        return true;
    }

    private static bool TryReadBlackboard(
        ISessionRun session,
        out string id,
        out BoxUnlockProfile profile,
        out int seed,
        out int rootBase)
    {
        var idValue = session.SessionBlackboard.TryGet<string>(IdKey);
        var profileId = session.SessionBlackboard.TryGet<string>(ProfileKey);
        var unlocks = session.SessionBlackboard.TryGet<string>(UnlocksKey);
        var seedValue = session.SessionBlackboard.TryGet<int>(SeedKey);
        var rootBaseValue = session.SessionBlackboard.TryGet<int>(RootBaseKey);
        if (idValue.found && !string.IsNullOrWhiteSpace(idValue.value)
            && profileId.found && !string.IsNullOrWhiteSpace(profileId.value)
            && unlocks.found && !string.IsNullOrWhiteSpace(unlocks.value)
            && seedValue.found && rootBaseValue.found)
        {
            id = idValue.value!;
            profile = BoxUnlockProfile.Create(profileId.value!, unlocks.value!.Split(';', StringSplitOptions.RemoveEmptyEntries));
            seed = seedValue.value;
            rootBase = rootBaseValue.value;
            return true;
        }

        id = string.Empty;
        profile = null!;
        seed = 0;
        rootBase = 0;
        return false;
    }
}
