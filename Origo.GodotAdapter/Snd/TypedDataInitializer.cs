namespace Origo.GodotAdapter.Snd;

/// <summary>
///     Forces the Origo.GodotAdapter assembly to load, triggering all
///     <c>[ModuleInitializer]</c> registrations for the TypedData layered
///     kind system. Framework-internal: test projects reach it via
///     <c>InternalsVisibleTo</c>.
/// </summary>
internal static class TypedDataInitializer
{
    /// <summary>
    ///     Referencing this member forces the adapter assembly to load so its
    ///     module initializers run before TypedData adapter kinds are used.
    /// </summary>
    internal static void EnsureLoaded()
    {
    }
}
