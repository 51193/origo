namespace Origo.GodotAdapter.Integration.Tests.Runner;

public sealed class TestResult
{
    public string Name { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string? Error { get; init; }
    public double DurationMs { get; init; }
}
