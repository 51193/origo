namespace Origo.Core;

/// <summary>
///     Immutable runtime metadata record: application name, version,
///     and banner text displayed at startup.
/// </summary>
public sealed record OrigoMeta(string Name, string Version, string Banner)
{
    /// <summary>The default banner text used when no custom banner is provided.</summary>
    public const string DefaultBanner = """
                                        :3
                                        """;
}
