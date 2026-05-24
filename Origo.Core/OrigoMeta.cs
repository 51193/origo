namespace Origo.Core;

public sealed record OrigoMeta(string Name, string Version, string Banner)
{
    public const string DefaultBanner = """
                                        :3
                                        """;
}
