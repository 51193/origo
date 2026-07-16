namespace Origo.Core;

/// <summary>
///     Immutable runtime metadata record: application name, version,
///     and banner text displayed at startup.
/// </summary>
public sealed record OrigoMeta(string Name, string Version, string Banner)
{
    public const string DefaultBanner = """
                                        :3
                                        """;
}
