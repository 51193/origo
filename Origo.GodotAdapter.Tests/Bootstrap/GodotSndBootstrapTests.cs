using System;
using System.Linq;
using Origo.GodotAdapter.Bootstrap;
using Xunit;

namespace Origo.GodotAdapter.Tests.BootstrapTests;

public class GodotSndBootstrapTests
{
    [Fact]
    public void BindRuntimeAndContext_WithNullManager_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GodotSndBootstrap.BindRuntimeAndContext(null!, null!, null!, null!));
    }

    [Fact]
    public void BindRuntimeAndContext_HasExpectedFourParameterContract()
    {
        var method = typeof(GodotSndBootstrap)
            .GetMethods()
            .Single(m => m.Name == nameof(GodotSndBootstrap.BindRuntimeAndContext));
        var parameters = method.GetParameters();

        Assert.Equal(4, parameters.Length);
        Assert.Equal("manager", parameters[0].Name);
        Assert.Equal("world", parameters[1].Name);
        Assert.Equal("logger", parameters[2].Name);
        Assert.Equal("context", parameters[3].Name);
    }
}
