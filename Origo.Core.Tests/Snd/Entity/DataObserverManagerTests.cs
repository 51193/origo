using System;
using Origo.Core.Snd.Entity;
using Xunit;

namespace Origo.Core.Tests;

public class DataObserverManagerTests
{
    [Fact]
    public void NotifyObservers_CallbackUnsubscribes_DoesNotThrow()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        Action<object?, object?> cb = null!;
        cb = (_, _) =>
        {
            callCount++;
            mgr.Unsubscribe("key", cb);
        };
        mgr.Subscribe("key", cb);
        mgr.NotifyObservers("key", 1, 2);
        mgr.NotifyObservers("key", 2, 3);
        Assert.Equal(1, callCount);
    }
}
