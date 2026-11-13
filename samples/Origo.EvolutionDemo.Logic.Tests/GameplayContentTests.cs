using System.Collections.Generic;
using Origo.EvolutionDemo.Logic.Gameplay;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class GameplayContentTests
{
    [Fact]
    public void UnlockService_RequiresPrerequisitesAndMilestones()
    {
        var content = BuildContent();
        var progression = new GameplayProgression();
        var service = new UnlockService(content, progression);
        Assert.False(service.CanUnlock("unlock.spike"));
        Assert.True(service.TryUnlock("unlock.root"));
        Assert.True(service.CanUnlock("unlock.spike"));
        Assert.True(service.TryUnlock("unlock.spike"));
        Assert.True(service.IsContentAvailable("gene.organ.spike"));
    }

    [Fact]
    public void Resolver_SelectsOneVariantByPriorityAndBiome()
    {
        var content = BuildContent();
        var progression = new GameplayProgression();
        var unlocks = new UnlockService(content, progression);
        Assert.True(unlocks.TryUnlock("unlock.root"));
        var resolver = new MechanicResolver(content, unlocks);
        Assert.Equal("death.snow", resolver.Resolve("death.handler", "snow")?.Id);
        Assert.Equal("death.base", resolver.Resolve("death.handler", "normal")?.Id);
    }

    [Fact]
    public void ModuleHost_LoadsDependenciesAndPipeline()
    {
        var host = new GameplayModuleHost();
        host.AddModule(new TestModule("base", []));
        host.AddModule(new TestModule("child", ["base"]));
        var (content, services) = host.Load();
        Assert.NotNull(content);
        Assert.Empty(services.Systems);
    }

    private static GameplayContentSnapshot BuildContent()
    {
        var registry = new GameplayContentRegistry();
        registry.AddHandler(new GameplayHandlerDefinition("base", []));
        registry.AddHandler(new GameplayHandlerDefinition("snow", []));
        registry.AddContent(new GameplayContentEntry("gene.organ.spike", "gene", "unlock.spike"));
        registry.AddContent(new GameplayContentEntry("base.death", "mechanic", "unlock.root"));
        registry.AddUnlock(new UnlockDefinition("unlock.root", "Root", "core", 0, [], ["base.death"], []));
        registry.AddUnlock(new UnlockDefinition("unlock.spike", "Spike", "organs", 10,
            ["unlock.root"], ["gene.organ.spike"], []));
        registry.AddVariant(new MechanicVariant("death.base", "death.handler", "base", null,
            "unlock.root", [], 0, new Dictionary<string, string>()));
        registry.AddVariant(new MechanicVariant("death.snow", "death.handler", "snow", null,
            "unlock.root", ["snow"], 10, new Dictionary<string, string>()));
        return registry.BuildSnapshot();
    }

    private sealed class TestModule(string id, IReadOnlyList<string> dependencies) : IGameplayModule
    {
        public string Id => id;
        public string Version => "1";
        public IReadOnlyList<string> Dependencies => dependencies;
        public void Register(GameplayContentRegistry content, GameplayModuleServices services) { }
    }
}
