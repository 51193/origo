namespace Origo.GodotAdapter.Integration.Tests.Runner;

public interface IDeferredTestFixture
{
    bool IsComplete { get; }
    void Setup();
    void AdvanceFrame();
}
