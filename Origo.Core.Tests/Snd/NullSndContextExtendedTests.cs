using System;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class NullSndContextExtendedTests
{
    [Fact]
    public void ListSaves_ReturnsEmpty() => Assert.Empty(NullSndContext.Instance.ListSaves());

    [Fact]
    public void RequestSaveGameAuto_Throws() =>
        Assert.Throws<InvalidOperationException>(() => NullSndContext.Instance.RequestSaveGameAuto());

    [Fact]
    public void RequestSaveGameAuto_WithValue_Throws() =>
        Assert.Throws<InvalidOperationException>(() => NullSndContext.Instance.RequestSaveGameAuto("my_save"));

    [Fact]
    public void HasContinueData_ReturnsFalse() => Assert.False(NullSndContext.Instance.HasContinueData());

    [Fact]
    public void RequestContinueGame_ReturnsFalse() => Assert.False(NullSndContext.Instance.RequestContinueGame());

    [Fact]
    public void MutationOperations_ThrowInvalidOperationException()
    {
        var ctx = NullSndContext.Instance;

        Assert.Throws<InvalidOperationException>(() => ctx.RequestLoadGame("any"));
        Assert.Throws<InvalidOperationException>(() => ctx.RequestSaveGame("any"));
        Assert.Throws<InvalidOperationException>(() => ctx.SetContinueTarget("any"));
        Assert.Throws<InvalidOperationException>(() => ctx.RequestSwitchForegroundLevel("level"));
        Assert.Throws<InvalidOperationException>(() => ctx.RequestLoadInitialSave());
        Assert.Throws<InvalidOperationException>(() => ctx.RequestLoadMainMenuEntrySave());

        // Read-only operations still safe
        Assert.False(ctx.HasContinueData());
        Assert.Empty(ctx.ListSaves());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. SessionSndContext — pass-through delegation
// ─────────────────────────────────────────────────────────────────────────────
