# 数据观察者 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Snd/Entity](../Origo.Core/Snd/Entity/README.md)
> [↔ 被测行为: usage/snd-entity-model](../usage/snd-entity-model.md)

## 被测行为概览

验证 DataObserverManager 的订阅/通知系统：Subscribe/Unsubscribe/Notify 基本链路、
old/new 值正确传递、key 隔离（只通知订阅的 key）、多订阅者全部触发、
通知中自取消订阅的安全性（重入安全）、Clear 全部移除。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `DataObserverManagerTests.cs` | 基础链路 + 重入安全 + key 隔离 + 多订多通知 |
| `DataObserverManagerExtendedTests.cs` | 带 filter 的订阅 + Clear + 扩展边缘 |

## DataObserverManagerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Subscribe_ThenNotify_CallbackInvoked` | 订阅后通知回调被调用 | snd-entity-model: 数据观察者 |
| `Notify_PassesCorrectOldAndNewValues` | 回调收到正确的 old/new 参数 | snd-entity-model |
| `MultipleSubscribers_SameKey_AllNotified` | 同一 key 多个订阅者全部收到通知 | snd-entity-model |
| `DifferentKeys_HaveIsolatedSubscribers` | hp 通知不影响 mp 订阅者 | snd-entity-model |
| `MultipleNotify_SameKey_AllTriggerCallback` | 连续多次通知全部触发 | snd-entity-model |
| `Unsubscribe_DifferentCallback_SameKey_OnlyTargetRemoved` | 只取消目标回调不影响其他 | snd-entity-model |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Notify_ForNeverSubscribedKey_DoesNotThrow` | 通知从未订阅的 key | 不抛异常 |
| `Notify_OnlyForSubscribedKey_OtherKeysIgnored` | mp 通知不触发 hp 订阅者 | hp 回调不被调用 |
| `Unsubscribe_ThenNotify_NotCalled` | 取消订阅后通知 | 回调不被调用 |
| `Notify_InsideCallback_UnsubscribesItself_DoesNotThrow` | 通知回调中自取消 | 不抛异常，通知不被再次调用 |
| `Notify_InsideCallback_SubscribesNewKey_CurrentNotificationUnaffected` | 通知中订阅新 key | 当前通知周期新订阅不触发 |

## DataObserverManagerExtendedTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Subscribe_And_Notify` | 基础通知链路 | snd-entity-model |
| `Unsubscribe_StopsNotification` | 取消后不再通知 | snd-entity-model |
| `Subscribe_WithFilter_SkipsFiltered` | filter 返回 false 时跳过 | snd-entity-model |
| `Clear_RemovesAllSubscriptions` | Clear 后全部通知不触发 | snd-entity-model |
| `MultipleSubscribers_AllNotified` | 多订多通知 | snd-entity-model |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| DataObserverManager 的 Dispose 后行为 | Dispose 后 Subscribe/Unsubscribe/Notify 应抛 ObjectDisposedException | IDisposable 模式 |

---

[↑ 回到 Origo.Core.Tests](README.md)
