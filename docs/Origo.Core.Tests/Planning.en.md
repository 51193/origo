<!-- docsync-pair: Origo.Core.Tests/Planning -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Planning Tests

> [↑ Back to Origo.Core.Tests](README.en.md)
> [↔ Module under test: Origo.Core/Planning](../Origo.Core/Planning/README.en.md)

## Behavior Overview

Validates the behavior of `PlanExecutionStrategyBase`: intent-driven plan execution, automatic Action strategy mount/unmount,
plan step advancement (including failure branch), and no subscription leaks from repeated Wire.

All plan execution tests use `FullMemorySndSceneHost` + `TestFactory.CreateRuntime()` to build a complete in-memory runtime,
ensuring Action strategy mount/unmount goes through the real `SndStrategyManager`.

## Test File List

| File | Verification Focus |
|------|-------------------|
| `Planning/PlanExecutionStrategyBaseTests.cs` | Complete plan lifecycle: start/advance/complete/fail, Action strategy mount/unmount, subscription leak prevention (regression: consecutive same steps reset the ActionKey suffix; StartIntent failure rolls back wiring for retry; action completion without an active intent throws and cleans up) |

## PlanExecutionStrategyBaseTests Details

### Happy Path

| Test Method | Verified Behavior | Reference |
|-------------|-----------------|-----------|
| `AfterSpawn_IntentPresent_StartsPlan` | When intent data key exists at AfterSpawn, automatically starts plan (writes PlanStepKey=first step) | Planning |
| `AfterAdd_IntentPresent_StartsPlan` | When intent exists at AfterAdd, also starts plan | Planning |
| `AfterLoad_IntentPresent_DoesNotRestartPlan` | AfterLoad does not reset existing plan steps (save restoration) | Planning |
| `StartIntent_ClearsPreviousPlanState` | Starting new intent clears old step/Action data | Planning |
| `ActionCompletion_InSndEntity_AdvancesToNextStep` | After ActionStatus="completed", advances to next step via data subscription, unmounts old Action and mounts new Action | Planning |
| `ActionCompletion_LastStep_CompletesPlan` | After last step completes, clears intent, intent_status="completed" | Planning |
| `StepWithoutAction_DoesNotAddStrategy` | When StepToActionIndex returns null, does not mount Action strategy but still records step | Planning |
| `BeforeRemove_UnmountsActionStrategy` | BeforeRemove cleans up current Action strategy when plan strategy is removed | Planning |
| `ActionFailed_AdvancesPlan_AndTerminates` | After Action failure (ActionStatus="failed"), calls OnPlanFailed, plan terminates | Planning |
| `OnPlanCompleted_SuccessPath_FiresHook` | OnPlanCompleted triggered after single-step plan completes, OnPlanFailed not triggered | Planning |
| `ResolveNextStep_ReturnsNull_NoPathTerminatesPlan` | Action completes but ResolveNextStep returns null (no viable path), plan cleanly terminates, triggers OnPlanCompleted | Planning |
| `PushAction_ActionAlreadyMountedInLifecycleIndices_ReusesItInsteadOfThrowing` | Action already statically mounted via LifecycleIndices is reused instead of mounted twice, no throw | Planning |
| `PushAction_ConsecutiveSameStep_ResetsActionKeyParamSuffix` | Consecutive same step resets ActionKey to canonical form, no stale ",param" suffix | Planning |

### Error Path

| Test Method | Triggered Error | Expected Behavior |
|-------------|----------------|-------------------|
| `AdvancePlan_ActionCompletesWithoutIntent_ThrowsAndCleansUpAction` | Action marked completed after intent cleared | InvalidOperationException (contains "no intent is active"), Action unmounted and plan step cleared |
| `Wire_SecondPlanStrategyOnSameEntity_Throws` | Mounting a second plan strategy on the same entity | InvalidOperationException (contains "plan strategy") |
| `Wire_StartIntentThrows_RollsBackWiringAndAllowsRetry` | ResolveNextStep throws during StartIntent | InvalidOperationException propagates, subscriptions rolled back to zero, retry succeeds |

### Boundary Path

| Test Method | Boundary Condition | Expected Behavior |
|-------------|-------------------|-------------------|
| `AfterSpawn_NoIntent_DoesNotStartPlan` | No intent data key | Does not write any step data |
| `DefaultHooks_DoNotMutateEntityData` | Default hook implementations called sequentially | Existing entity data unchanged |
| `Wire_MultipleCycles_ManagesSubscriptionsCorrectly` | AfterAdd after AfterSpawn | Subscription count stays 1, returns to 0 after BeforeRemove |

## Test Helper Strategies

| Strategy Class | Defined In | Purpose |
|---------------|-----------|---------|
| `SimplePlanStrategy` | PlanExecutionStrategyBaseTests.cs | Implements ResolveNextStep/StepToActionIndex: three-step plan test→step_a→step_b→complete |
| `FakeActionStrategy` | PlanExecutionStrategyBaseTests.cs | Mocks Action strategy, collects call events via AfterAdd/BeforeRemove |
| `FakeAction2Strategy` | PlanExecutionStrategyBaseTests.cs | Mocks second Action strategy, collects AfterAdd events |
| `FailingPlanStrategy` | PlanExecutionStrategyBaseTests.cs | Mocks failing plan: after step_a completes, ResolveNextStep returns null triggering OnPlanFailed |
| `CompletingPlanStrategy` | PlanExecutionStrategyBaseTests.cs | Mocks completing plan: single-step plan complete_test→step_a→complete, overrides OnPlanCompleted/OnPlanFailed to record calls |
| `LoopPlanStrategy` | PlanExecutionStrategyBaseTests.cs | Mocks a looping plan: re-entering the same step_a repeatedly, verifying ActionKey param-suffix reset and already-mounted action reuse |
| `FlakyStartPlanStrategy` | PlanExecutionStrategyBaseTests.cs | Plan whose ResolveNextStep can be configured to throw, verifying wiring rollback on StartIntent failure and re-wireability |
| `SubscriptionTrackingEntity` | PlanExecutionStrategyBaseTests.cs | ISndEntity + ISndEntityRawSubscription wrapper around StubSndEntity tracking subscribe/unsubscribe counts per key, verifying Wire failure leaves no orphaned callbacks |

## Known Coverage Gaps

| Gap Description | Impact | Reference |
|----------------|--------|-----------|

---

[↑ Back to Origo.Core.Tests](README.en.md)
