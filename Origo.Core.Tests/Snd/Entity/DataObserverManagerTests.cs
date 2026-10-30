using System;
using System.Collections.Generic;
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
        void cb(TypedData oldVal, TypedData newVal) => callCount++;
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
    public void Notify_WhenCallbackThrows_AbortsRemainingCallbacksAndPropagates()
    {
        // Contract: notifications are delivered in subscription order and a
        // throwing callback aborts the remaining notifications for the same
        // key while the exception propagates (fail-fast). The setter has
        // already committed the value at this point.
        var mgr = new DataObserverManager();
        var remainingCalled = false;
        mgr.Subscribe("hp", (_, _) => throw new InvalidOperationException("observer failure"));
        mgr.Subscribe("hp", (_, _) => remainingCalled = true);

        var ex = Assert.Throws<InvalidOperationException>(
            () => mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50));
        Assert.Equal("observer failure", ex.Message);
        Assert.False(remainingCalled, "A throwing callback must abort the remaining callbacks.");
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

        void cb1(TypedData oldVal, TypedData newVal) => cb1Called = true;
        void cb2(TypedData oldVal, TypedData newVal) => cb2Called = true;

        mgr.Subscribe("hp", cb1);
        mgr.Subscribe("hp", cb2);
        mgr.Unsubscribe("hp", cb1);
        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.False(cb1Called);
        Assert.True(cb2Called);
    }

    [Fact]
    public void Notify_WhenEarlierCallbackUnsubscribesLaterCallback_LaterCallbackNotInvoked()
    {
        // An observer hook may unmount a sibling binding while the target's
        // notification pass is running. The snapshot iteration must skip the
        // subscription that was removed in the same pass: running OnDataChanged
        // after the binding's OnUnmounted cleanup would act on released state.
        var mgr = new DataObserverManager();
        var invoked = new List<string>();

        void later(TypedData _1, TypedData _2) => invoked.Add("later");
        void earlier(TypedData _1, TypedData _2)
        {
            invoked.Add("earlier");
            mgr.Unsubscribe("hp", later);
        }

        mgr.Subscribe("hp", earlier);
        mgr.Subscribe("hp", later);
        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.Equal(["earlier"], invoked);
    }

    [Fact]
    public void Notify_WhenEarlierCallbackClearsSubscriptions_RemainingCallbacksNotInvoked()
    {
        // Clear() is used when the owning entity is torn down. Its subscriptions
        // must be marked removed before the dictionary is dropped so a
        // notification pass already in flight does not call a callback after the
        // entity's cleanup ran.
        var mgr = new DataObserverManager();
        var invoked = new List<string>();

        void first(TypedData _1, TypedData _2)
        {
            invoked.Add("first");
            mgr.Clear();
        }

        void second(TypedData _1, TypedData _2) => invoked.Add("second");

        mgr.Subscribe("hp", first);
        mgr.Subscribe("hp", second);
        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.Equal(["first"], invoked);

        // The cleared subscriptions stay gone for later notifications.
        mgr.NotifyObservers("hp", (TypedData)50, (TypedData)10);
        Assert.Equal(["first"], invoked);
    }

    [Fact]
    public void Notify_UnsubscribeAndResubscribeSameCallbackInOnePass_OnlyFiresOnLaterNotification()
    {
        // A pass that unsubscribes and re-subscribes the same delegate must not
        // deliver the current change through the fresh subscription: it was not
        // part of the snapshot, and firing it would double-deliver the change.
        var mgr = new DataObserverManager();
        var calls = 0;
        var cycled = false;
        void cb(TypedData _1, TypedData _2) => calls++;
        void driver(TypedData _1, TypedData _2)
        {
            if (cycled)
                return;

            cycled = true;
            mgr.Unsubscribe("hp", cb);
            mgr.Subscribe("hp", cb);
        }

        mgr.Subscribe("hp", cb);
        mgr.Subscribe("hp", driver);
        mgr.NotifyObservers("hp", (TypedData)100, (TypedData)50);

        Assert.Equal(1, calls);

        mgr.NotifyObservers("hp", (TypedData)50, (TypedData)10);
        Assert.Equal(2, calls);
    }
}
