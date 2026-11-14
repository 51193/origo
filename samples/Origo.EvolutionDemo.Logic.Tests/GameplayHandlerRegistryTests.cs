using System.Linq;
using System;
using Origo.EvolutionDemo.Logic.Gameplay;
using Xunit;

namespace Origo.EvolutionDemo.Logic.Tests;

public sealed class GameplayHandlerRegistryTests
{
    [Fact]
    public void Registry_ResolvesTypedHandlerAndRejectsDuplicate()
    {
        var registry = new GameplayHandlerRegistry<string>();
        registry.Register(new TestHandler("a", static value => value.ToString()));
        Assert.Equal("a", registry.HandlerIds.Single());
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new TestHandler("a", static _ => { })));
        Assert.Throws<InvalidOperationException>(() => registry.Require("missing"));
    }

    private sealed class TestHandler(string id, Action<string> execute) : IGameplayHandler<string>
    {
        public string Id => id;
        public void Execute(string context) => execute(context);
    }
}
