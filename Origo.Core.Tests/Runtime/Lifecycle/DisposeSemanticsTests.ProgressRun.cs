using System;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Tests that verify the ProgressRun Dispose contract: directory cleanup,
///     idempotency, post-dispose state, and exception safety.
/// </summary>
[Collection("StrategyStateTests")]
public class DisposeSemanticsTestsProgressRun
{
    [Fact]
    public void ProgressRun_Dispose_DoesNotCallPersistProgress()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        ctx.Blackboard.ProgressBlackboard!.SetValue("test_key", 42);

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        Assert.False(fs.Exists("root/current/progress.json"));
    }

    [Fact]
    public void ProgressRun_Dispose_DeletesCurrentDirectory()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        ctx.Save.RequestSaveGame("temp");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        Assert.True(fs.Exists("root/current/progress.json"));

        progressRun.Dispose();

        Assert.False(fs.Exists("root/current/progress.json"));
        Assert.False(fs.Exists("root/current/progress_state_machines.json"));
    }

    [Fact]
    public void ProgressRun_Dispose_DeletesCurrentDirectory_EvenWhenEmpty()
    {
        var (ctx, fs) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        Assert.False(fs.Exists("root/current/progress.json"));
    }

    [Fact]
    public void ProgressRun_Dispose_Twice_IsIdempotent()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        var ex = Record.Exception(() => progressRun.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void ProgressRun_AfterDispose_ForegroundSession_IsNull()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();
        var progressRun = ctx.EnsureProgressRun();

        Assert.NotNull(progressRun.SessionManager.ForegroundSession);

        progressRun.Dispose();

        Assert.Null(progressRun.SessionManager.ForegroundSession);
    }

    [Fact]
    public void ProgressRun_AfterDispose_SessionManagerKeys_IsEmpty()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();
        ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "bg_level");

        var progressRun = ctx.EnsureProgressRun();
        Assert.NotEmpty(progressRun.SessionManager.Keys);

        progressRun.Dispose();

        Assert.Empty(progressRun.SessionManager.Keys);
    }

    [Fact]
    public void ProgressRun_AfterDispose_ProgressBlackboard_IsCleared()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();
        ctx.Blackboard.ProgressBlackboard!.SetValue("key", "value");

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        var (found, _) = progressRun.ProgressBlackboard.TryGet<string>("key");
        Assert.False(found);
    }

    [Fact]
    public void ProgressRun_Dispose_SafeEvenWhenNoCurrentDirectory()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();
        progressRun.Dispose();

        var ex = Record.Exception(() => progressRun.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void ProgressRun_Dispose_StateMachineContainerClear_DoesNotThrow()
    {
        var (ctx, _) = DisposeSemanticsTestInfrastructure.CreateForegroundContext();

        var progressRun = ctx.EnsureProgressRun();

        var ex = Record.Exception(() => progressRun.Dispose());
        Assert.Null(ex);
    }
}
