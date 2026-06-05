using System;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

public class DataObserverManagerTests
{
    [Fact]
    public void Subscribe_ThenNotify_CallbackInvoked()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        mgr.Subscribe("hp", (_, _) => callCount++);
        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Notify_PassesCorrectOldAndNewValues()
    {
        var mgr = new DataObserverManager();
        TypedData capturedOld = default;
        TypedData capturedNew = default;

        mgr.Subscribe("hp", (old, @new) =>
        {
            capturedOld = old;
            capturedNew = @new;
        });

        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.Equal(100, capturedOld.AsInt32());
        Assert.Equal(50, capturedNew.AsInt32());
    }

    [Fact]
    public void Notify_OnlyForSubscribedKey_OtherKeysIgnored()
    {
        var mgr = new DataObserverManager();
        var hpCallCount = 0;
        mgr.Subscribe("hp", (_, _) => hpCallCount++);
        mgr.NotifyObservers("mp", (TypedData)50, (TypedData)40);

        Assert.Equal(0, hpCallCount);
    }

    [Fact]
    public void Unsubscribe_ThenNotify_NotCalled()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        Action<TypedData, TypedData> cb = (_, _) => callCount++;
        mgr.Subscribe("hp", cb);
        mgr.Unsubscribe("hp", cb);
        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void MultipleSubscribers_SameKey_AllNotified()
    {
        var mgr = new DataObserverManager();
        var callCount1 = 0;
        var callCount2 = 0;
        var callCount3 = 0;

        mgr.Subscribe("hp", (_, _) => callCount1++);
        mgr.Subscribe("hp", (_, _) => callCount2++);
        mgr.Subscribe("hp", (_, _) => callCount3++);

        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.Equal(1, callCount1);
        Assert.Equal(1, callCount2);
        Assert.Equal(1, callCount3);
    }

    [Fact]
    public void DifferentKeys_HaveIsolatedSubscribers()
    {
        var mgr = new DataObserverManager();
        var hpCalled = false;
        var mpCalled = false;

        mgr.Subscribe("hp", (_, _) => hpCalled = true);
        mgr.Subscribe("mp", (_, _) => mpCalled = true);

        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.True(hpCalled);
        Assert.False(mpCalled);
    }

    [Fact]
    public void Notify_ForNeverSubscribedKey_DoesNotThrow()
    {
        var mgr = new DataObserverManager();

        var ex = Record.Exception(() => mgr.NotifyObservers("nonexistent", (TypedData)1, (TypedData)2));
        Assert.Null(ex);
    }

    [Fact]
    public void Notify_InsideCallback_UnsubscribesItself_DoesNotThrow()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        Action<TypedData, TypedData> cb = null!;
        cb = (_, _) =>
        {
            callCount++;
            mgr.Unsubscribe("key", cb);
        };
        mgr.Subscribe("key", cb);
        mgr.NotifyObservers("key", (TypedData)1, (TypedData)2);
        mgr.NotifyObservers("key", (TypedData)2, (TypedData)3);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Notify_InsideCallback_SubscribesNewKey_CurrentNotificationUnaffected()
    {
        var mgr = new DataObserverManager();
        var newSubCalled = false;

        mgr.Subscribe("a", (_, _) => { mgr.Subscribe("b", (_, _) => newSubCalled = true); });

        mgr.NotifyObservers("a", (TypedData)1, (TypedData)2);

        Assert.False(newSubCalled);
    }

    [Fact]
    public void MultipleNotify_SameKey_AllTriggerCallback()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        mgr.Subscribe("hp", (_, _) => callCount++);

        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)75);
        mgr.NotifyObservers("hp", (TypedData)75, (TypedData)50);
        mgr.NotifyObservers("hp", (TypedData)50, (TypedData)0);

        Assert.Equal(3, callCount);
    }

    [Fact]
    public void Unsubscribe_DifferentCallback_SameKey_OnlyTargetRemoved()
    {
        var mgr = new DataObserverManager();
        var cb1Called = false;
        var cb2Called = false;

        Action<TypedData, TypedData> cb1 = (_, _) => cb1Called = true;
        Action<TypedData, TypedData> cb2 = (_, _) => cb2Called = true;

        mgr.Subscribe("hp", cb1);
        mgr.Subscribe("hp", cb2);
        mgr.Unsubscribe("hp", cb1);
        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.False(cb1Called);
        Assert.True(cb2Called);
    }
}
