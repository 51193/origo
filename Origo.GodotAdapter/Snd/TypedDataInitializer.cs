namespace Origo.GodotAdapter.Snd;

/// <summary>
///     Public entry point that forces the Origo.GodotAdapter assembly to load,
///     triggering all <c>[ModuleInitializer]</c> registrations for the TypedData
///     layered kind system.  Tests should call <see cref="IsLoaded"/> in their
///     static constructor before exercising registered adapter types.
/// </summary>
public static class TypedDataInitializer
{
    public static bool IsLoaded => true;
}
