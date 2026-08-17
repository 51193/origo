using Origo.SourceGeneration;
using Origo.TestSupport;
using Xunit;

namespace Origo.SourceGeneration.Tests;

public class PrivateFieldNamingTests
{
    [Fact]
    public void PrivateFields_FollowUnderscoreCamelCase()
    {
        var violations = PrivateFieldNamingConvention.FindViolations(typeof(TypedDataGenerator).Assembly);
        Assert.Empty(violations);
    }
}
