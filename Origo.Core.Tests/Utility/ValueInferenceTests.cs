using System;
using Origo.Core.Utility;
using Xunit;

namespace Origo.Core.Tests;

public class ValueInferenceTests
{
    [Theory]
    [InlineData("42", typeof(int), 42)]
    [InlineData("-7", typeof(int), -7)]
    [InlineData("3000000000", typeof(long), 3000000000L)]
    [InlineData("-3000000000", typeof(long), -3000000000L)]
    [InlineData("3.14", typeof(float), 3.14f)]
    [InlineData("1e3", typeof(float), 1000.0f)]
    [InlineData("true", typeof(bool), true)]
    [InlineData("FALSE", typeof(bool), false)]
    [InlineData("hello", typeof(string), "hello")]
    [InlineData("12abc", typeof(string), "12abc")]
    [InlineData("", typeof(string), "")]
    public void Infer_ReturnsFirstMatchingTypedValue(string raw, Type expectedType, object expected)
    {
        var result = ValueInference.Infer(raw);

        Assert.IsType(expectedType, result);
        Assert.Equal(expected, result);
    }
}
