using System;
using Xunit;

namespace Origo.Core.Tests;

public class TypeStringMappingTests
{
    [Fact]
    public void RegisterType_ThrowsOnConflictingMapping()
    {
        var mapping = new TypeStringMapping();
        mapping.RegisterType<DateTime>("MyDateTime");

        Assert.Throws<InvalidOperationException>(() => mapping.RegisterType<long>("MyDateTime"));
        Assert.Throws<InvalidOperationException>(() => mapping.RegisterType<DateTime>("OtherName"));
    }
}
