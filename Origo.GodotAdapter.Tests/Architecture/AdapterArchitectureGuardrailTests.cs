using System.Linq;
using System.Reflection;
using Origo.GodotAdapter.Bootstrap;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class AdapterArchitectureGuardrailTests
{
    [Fact]
    public void OrigoDefaultEntry_ShouldNotExposeSaveFacadeApis()
    {
        var methodNames = typeof(OrigoDefaultEntry)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(m => m.Name)
            .ToArray();

        Assert.DoesNotContain("RequestSaveGame", methodNames);
        Assert.DoesNotContain("RequestSaveGameAuto", methodNames);
        Assert.DoesNotContain("RequestLoadGame", methodNames);
        Assert.DoesNotContain("RequestContinueGame", methodNames);
        Assert.DoesNotContain("ListSaves", methodNames);
        Assert.DoesNotContain("ListSavesWithMetaData", methodNames);
    }
}
