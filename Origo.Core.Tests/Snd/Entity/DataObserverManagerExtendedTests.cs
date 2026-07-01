using System;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ── BlackboardSerializer ───────────────────────────────────────────

public class DataObserverManagerExtendedTests
{
    [Fact]
    public void DataObserverManager_Subscribe_And_Notify()
    {
        var mgr = new DataObserverManager();
        TypedData receivedOld = default, receivedNew = default;
        mgr.Subscribe("hp", (o, n) =>
        {
            receivedOld = o;
            receivedNew = n;
        });

        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)80);
        Assert.Equal(100, receivedOld.AsInt32());
        Assert.Equal(80, receivedNew.AsInt32());
    }

    [Fact]
    public void DataObserverManager_Unsubscribe_StopsNotification()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        void callback(TypedData oldVal, TypedData newVal) => callCount++;

        mgr.Subscribe("hp", callback);
        mgr.NotifyObservers("hp", default, default);
        Assert.Equal(1, callCount);

        mgr.Unsubscribe("hp", callback);
        mgr.NotifyObservers("hp", default, default);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void DataObserverManager_Subscribe_WithFilter_SkipsFiltered()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        mgr.Subscribe("hp", (_, _) => callCount++, (o, n) => n.AsInt32() > 50);

        mgr.NotifyObservers("hp", (TypedData)0, (TypedData)80);
        Assert.Equal(1, callCount);

        mgr.NotifyObservers("hp", (TypedData)0, (TypedData)30);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void DataObserverManager_Clear_RemovesAllSubscriptions()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        mgr.Subscribe("hp", (_, _) => callCount++);
        mgr.Clear();
        mgr.NotifyObservers("hp", default, default);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void DataObserverManager_MultipleSubscribers_AllNotified()
    {
        var mgr = new DataObserverManager();
        var count1 = 0;
        var count2 = 0;
        mgr.Subscribe("hp", (_, _) => count1++);
        mgr.Subscribe("hp", (_, _) => count2++);

        mgr.NotifyObservers("hp", default, default);
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }
}

// ── SndSceneSerializer ─────────────────────────────────────────────
