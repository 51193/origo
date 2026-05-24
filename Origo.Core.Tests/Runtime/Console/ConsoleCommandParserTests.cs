using Origo.Core.Runtime.Console;
using Xunit;

namespace Origo.Core.Tests;

public class ConsoleCommandParserTests
{
    [Fact]
    public void ConsoleCommandParser_TryParse_EmptyLine_Fails()
    {
        var ok = ConsoleCommandParser.TryParse("", out var inv, out var err);
        Assert.False(ok);
        Assert.Null(inv);
        Assert.NotNull(err);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_WhitespaceLine_Fails()
    {
        var ok = ConsoleCommandParser.TryParse("   ", out _, out var err);
        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_SingleCommand()
    {
        var ok = ConsoleCommandParser.TryParse("help", out var inv, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.NotNull(inv);
        Assert.Equal("help", inv!.Command);
        Assert.Empty(inv.PositionalArgs);
        Assert.Empty(inv.NamedArgs);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_PositionalArgs()
    {
        var ok = ConsoleCommandParser.TryParse("spawn myName myTemplate", out var inv, out _);
        Assert.True(ok);
        Assert.Equal("spawn", inv!.Command);
        Assert.Equal(2, inv.PositionalArgs.Count);
        Assert.Equal("myName", inv.PositionalArgs[0]);
        Assert.Equal("myTemplate", inv.PositionalArgs[1]);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_NamedArgs()
    {
        var ok = ConsoleCommandParser.TryParse("spawn name=myName template=myTpl", out var inv, out _);
        Assert.True(ok);
        Assert.Equal("spawn", inv!.Command);
        Assert.Empty(inv.PositionalArgs);
        Assert.Equal("myName", inv.NamedArgs["name"]);
        Assert.Equal("myTpl", inv.NamedArgs["template"]);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_InvalidNamedArg_Fails()
    {
        var ok = ConsoleCommandParser.TryParse("cmd =value", out _, out var err);
        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_NamedArgMissingValue_Fails()
    {
        var ok = ConsoleCommandParser.TryParse("cmd key=", out _, out var err);
        Assert.False(ok);
        Assert.NotNull(err);
    }
}

// ── ConsoleCommandRouter ───────────────────────────────────────────────
