using System;
using Origo.Core.Utility;
using Xunit;

namespace Origo.Core.Tests;

public class PathUtilityTests
{
    [Theory]
    [InlineData("/path/to/dir/", "/path/to/dir")]
    [InlineData("/path/to/dir", "/path/to/dir")]
    [InlineData("no-trailing-slash", "no-trailing-slash")]
    [InlineData("", "")]
    public void NormalizeDirectoryPath_StripsTrailingSlashes(string input, string expected)
    {
        Assert.Equal(expected, PathUtility.NormalizeDirectoryPath(input));
    }

    [Theory]
    [InlineData("*.json", ".json")]
    [InlineData("*.cs", ".cs")]
    [InlineData("*", "")]
    public void ExtractGlobSuffix_ReturnsSuffix(string pattern, string expected)
    {
        Assert.Equal(expected, PathUtility.ExtractGlobSuffix(pattern));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("file.json")]
    [InlineData("some*thing")]
    public void ExtractGlobSuffix_ReturnsNull_WhenNoGlob(string? pattern)
    {
        Assert.Null(PathUtility.ExtractGlobSuffix(pattern!));
    }

    [Fact]
    public void Combine_NullOrEmptyBase_ReturnsRelative()
    {
        Assert.Equal("relative", PathUtility.Combine("", "relative"));
        Assert.Equal("relative", PathUtility.Combine(null!, "relative"));
    }

    [Fact]
    public void Combine_NullOrEmptyRelative_ReturnsBase()
    {
        Assert.Equal("/base", PathUtility.Combine("/base", ""));
        Assert.Equal("/base", PathUtility.Combine("/base", null!));
    }

    [Fact]
    public void Combine_JoinsPaths()
    {
        Assert.Equal("/base/sub", PathUtility.Combine("/base", "sub"));
        Assert.Equal("/base/sub", PathUtility.Combine("/base/", "/sub"));
        Assert.Equal("/base/sub", PathUtility.Combine("/base/", "sub"));
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\some")]
    [InlineData("foo/../bar")]
    public void Combine_RejectsPathTraversal(string relative)
    {
        Assert.Throws<ArgumentException>(() => PathUtility.Combine("/base", relative));
    }

    [Fact]
    public void GetParentDirectory_ReturnsParent()
    {
        Assert.Equal("/base", PathUtility.GetParentDirectory("/base/sub"));
        Assert.Equal("/base", PathUtility.GetParentDirectory("/base/sub/"));
        Assert.Equal("/a/b", PathUtility.GetParentDirectory("/a/b/c"));
    }

    [Fact]
    public void GetParentDirectory_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PathUtility.GetParentDirectory(""));
        Assert.Equal(string.Empty, PathUtility.GetParentDirectory(null!));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("C:")]
    [InlineData("/root")]
    public void GetParentDirectory_AtRoot_Throws(string path)
    {
        Assert.Throws<InvalidOperationException>(() => PathUtility.GetParentDirectory(path));
    }

    [Fact]
    public void GetParentDirectory_SingleSegment_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PathUtility.GetParentDirectory("file.txt"));
    }
}
