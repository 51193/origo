using System;
using Origo.Core.Runtime.Lifecycle;
using Xunit;

namespace Origo.Core.Tests;

public class EmptySessionManagerTests
{
    [Fact]
    public void EmptySessionManager_CreateBackgroundSession_Throws()
    {
        var m = EmptySessionManager.Instance;
        var ex = Assert.Throws<InvalidOperationException>(() =>
            m.CreateBackgroundSession("k", "level"));
        Assert.Contains("ProgressRun", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptySessionManager_QueryAndNoOps()
    {
        var m = EmptySessionManager.Instance;
        Assert.Null(m.ForegroundSession);
        Assert.Empty(m.Keys);
        Assert.Null(m.TryGet("any"));
        Assert.False(m.Contains("any"));
        m.DestroySession("any");
        m.ProcessAllSessions(0.016);
    }
}
