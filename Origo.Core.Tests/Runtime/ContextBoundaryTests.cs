using System;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class ContextBoundaryTests
{
    [Fact]
    public void NullSndContext_AllNoopMembers_AreSafe()
    {
        var ctx = NullSndContext.Instance;

        Assert.NotNull(ctx.SystemBlackboard);
        Assert.Null(ctx.ProgressBlackboard);
        Assert.Null(ctx.SessionManager.ForegroundSession);
        Assert.False(ctx.TrySubmitConsoleCommand("x"));
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
        Assert.Null(ctx.GetProgressStateMachines());
        Assert.NotNull(ctx.SessionManager);
        Assert.Equal(0, ctx.SubscribeConsoleOutput(_ => { }));
        ctx.UnsubscribeConsoleOutput(1);
        ctx.EnqueueBusinessDeferred(() => { });
        ctx.FlushDeferredActionsForCurrentFrame();
        ctx.ProcessConsolePending();
        Assert.Throws<InvalidOperationException>(() => ctx.RequestLoadMainMenuEntrySave());
        Assert.Throws<InvalidOperationException>(() => ctx.CloneTemplate("t"));
    }
}
