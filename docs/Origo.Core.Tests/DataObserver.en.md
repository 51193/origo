<!-- docsync-pair: Origo.Core.Tests/DataObserver -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — managed automatically by DocSyncTool; DO NOT EDIT. -->
# Data Observer Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Snd/Entity](../Origo.Core/Snd/Entity/README.en.md)
> [↔ Behavior under test: usage/snd-entity-model](../usage/snd-entity-model.en.md)

## Behavior Overview

Validates the DataObserverManager subscription/notification system: the basic Subscribe/Unsubscribe/Notify chain,
correct old/new value passing, key isolation (only subscribers for the subscribed key are notified), all multiple subscribers triggered,
safety of self-unsubscribing within notifications (re-entrancy safety), and Clear removing everything.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `DataObserverManagerTests.cs` | Basic chain + re-entrancy safety + key isolation + multiple subscribers and notifications |
| `DataObserverManagerExtendedTests.cs` | Subscription with filter + Clear + extended edge cases |

## DataObserverManagerTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `Subscribe_ThenNotify_CallbackInvoked` | After subscribing, callback is invoked on notification | snd-entity-model: Data Observer |
| `Notify_PassesCorrectOldAndNewValues` | Callback receives correct old/new parameters | snd-entity-model |
| `MultipleSubscribers_SameKey_AllNotified` | All subscribers for the same key receive notifications | snd-entity-model |
| `DifferentKeys_HaveIsolatedSubscribers` | hp notifications do not affect mp subscribers | snd-entity-model |
| `MultipleNotify_SameKey_AllTriggerCallback` | Consecutive notifications all trigger | snd-entity-model |
| `Unsubscribe_DifferentCallback_SameKey_OnlyTargetRemoved` | Only the target callback is removed, others unaffected | snd-entity-model |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `Notify_ForNeverSubscribedKey_DoesNotThrow` | Notify for a never-subscribed key | Does not throw |
| `Notify_OnlyForSubscribedKey_OtherKeysIgnored` | mp notification does not trigger hp subscriber | hp callback not invoked |
| `Unsubscribe_ThenNotify_NotCalled` | Notify after unsubscribe | Callback not invoked |
| `Notify_InsideCallback_UnsubscribesItself_DoesNotThrow` | Self-unsubscribe within notification callback | Does not throw, notification not called again |
| `Notify_InsideCallback_SubscribesNewKey_CurrentNotificationUnaffected` | Subscribe new key within notification | New subscription not triggered in current notification cycle |

## DataObserverManagerExtendedTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `DataObserverManager_Subscribe_And_Notify` | Basic notification chain | snd-entity-model |
| `DataObserverManager_Unsubscribe_StopsNotification` | No notification after unsubscribe | snd-entity-model |
| `DataObserverManager_Subscribe_WithFilter_SkipsFiltered` | Skipped when filter returns false | snd-entity-model |
| `DataObserverManager_Clear_RemovesAllSubscriptions` | No notifications triggered after Clear | snd-entity-model |
| `DataObserverManager_MultipleSubscribers_AllNotified` | Multiple subscribers all notified | snd-entity-model |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|
| DataObserverManager behavior after Dispose | Subscribe/Unsubscribe/Notify should throw ObjectDisposedException after Dispose | IDisposable pattern |

---

[↑ Back to Origo.Core.Tests](README.en.md)
