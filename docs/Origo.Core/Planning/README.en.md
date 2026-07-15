<!-- docsync-pair: Origo.Core/Planning/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->
# Planning

> [↑ Back to Origo.Core](../README.en.md)

The Planning subsystem provides intent-driven entity-level plan execution infrastructure.

## Interface

### `PlanExecutionStrategyBase : LifecycleStrategyBase`

Intent-driven plan execution base class. Encapsulates the complete lifecycle from intent generation to plan decomposition, action mount/unmount, and plan advancement.

**Design philosophy:** The framework manages wiring (subscription pairing, Action strategy insertion/removal, state machine control flow); users only provide two domain mapping functions. Any step type (including idle, patrol, etc.) should be implemented as an independent `LifecycleStrategyBase` Action strategy, registered via `StepToActionIndex`, treated no differently from other actions in the framework.

#### Abstract Members Users Must Implement

| Member | Type | Description |
|--------|------|-------------|
| `IntentKey` | `abstract string` | Entity data key storing the current intent string |
| `IntentStatusKey` | `abstract string` | Entity data key storing intent execution status |
| `PlanStepKey` | `abstract string` | Entity data key storing the current plan step type |
| `ActionKey` | `abstract string` | Entity data key storing the current action descriptor |
| `ActionStatusKey` | `abstract string` | Entity data key storing action execution status |
| `ResolveNextStep(intent, currentStep, failed, entity)` | `abstract string?` | Returns next step based on intent and current step; `null`/empty terminates the plan |
| `StepToActionIndex(stepType)` | `abstract string?` | Step type → Action strategy index; `null`/empty means no strategy needs to be mounted for this step |

#### Virtual Members Users May Override

| Member | Default | Description |
|--------|---------|-------------|
| `IntentStatusActive` | `"active"` | Intent active status value |
| `IntentStatusCompleted` | `"completed"` | Intent completed status value |
| `ActionStatusExecuting` | `"executing"` | Action executing status value |
| `ActionStatusCompleted` | `"completed"` | Action completed status value |
| `ActionStatusFailed` | `"failed"` | Action failed status value |

#### Sealed Lifecycle Hooks (Not Overridable)

The base class `sealed`s all 8 of `LifecycleStrategyBase`'s lifecycle hooks, auto-managing:

- `AfterSpawn` / `AfterAdd`: Subscribe signals + restart plan if intent already exists → call `OnAfterSpawn` / `OnAfterAdd`
- `AfterLoad`: Only subscribe signals (plan state is already persisted on save recovery; do not restart) → call `OnAfterLoad`
- `BeforeRemove` / `BeforeQuit` / `BeforeDead`: Remove current Action strategy → call corresponding `OnBefore*` → unsubscribe
- `BeforeSave`: Directly delegate to `OnBeforeSave` (no plan state manipulation needed on save)
- `Process`: Directly call `OnProcess`

#### User Extension Hooks (Virtual Methods)

| Hook | Trigger |
|------|---------|
| `OnAfterSpawn(entity, ctx)` | After AfterSpawn wiring completes |
| `OnAfterLoad(entity, ctx)` | After AfterLoad wiring completes |
| `OnAfterAdd(entity, ctx)` | After AfterAdd wiring completes |
| `OnBeforeRemove(entity, ctx)` | After removing Action, before unsubscribing |
| `OnBeforeQuit(entity, ctx)` | After removing Action, before unsubscribing |
| `OnBeforeDead(entity, ctx)` | After removing Action, before unsubscribing |
| `OnBeforeSave(entity, ctx)` | In the BeforeSave sealed hook |
| `OnProcess(entity, delta, ctx)` | In the Process frame |
| `OnIntentStarted(entity, intent)` | When a new intent starts executing |
| `OnStepStarted(entity, stepType)` | When a new step starts executing |
| `OnPlanCompleted(entity)` | When the plan is fully completed |
| `OnPlanFailed(entity)` | When the plan is terminated due to failure |

#### Built-in Behavior

1. **Auto subscription management**: In `AfterSpawn/AfterLoad/AfterAdd`, subscribe to data change notifications for `IntentKey` and `ActionStatusKey`; in `BeforeRemove/BeforeQuit/BeforeDead`, unsubscribe. The RAII closed loop is guaranteed by the base class.
2. **Plan advancement**: Intent change → restart plan; action completion/failure → advance to next step or terminate plan.
3. **Action strategy lifecycle**: Each step auto `AddStrategy(StepToActionIndex(step))`; auto `RemoveStrategy` on advancement/termination.
4. **Plan termination**: When all steps are complete, clear intent, plan_step, action; set intent_status to completed.

## Helper Extensions

### `ISndEntity.EnsureReplaceableStrategy(implKey, defaultStrategyIndex)` (Extension Method)

Located in `Origo.Core.Snd.Strategy.EntityStrategyExtensions`.

Ensures a certain resident strategy is mounted on the entity, supporting template-level overrides (`*_impl` pattern):

```csharp
entity.EnsureReplaceableStrategy("character.path_impl", "character.pathfind.astar");
```

- Reads the current value of `implKey` as a configuration override; falls back to `defaultStrategyIndex` when not set
- Uses `implKey` as a dedup marker; repeated calls have no side effects
- Corresponds to the "Replaceable Implementation" pattern in the design patterns documentation

## Design Decisions

1. **Why `sealed` lifecycle hooks?**
   Prevents users from forgetting to call the base class when overriding lifecycle hooks, which would cause wiring failures. Extension points are provided through `virtual On*` hooks.

2. **Why doesn't `ResolveNextStep` include `ISndContext`?**
   Plan decomposition should be a pure function of entity state. World queries should be done via `FindByName` + `InvokeStrategy`; injecting context into the plan advancement chain is discouraged — it causes non-determinism and testing difficulty.

3. **Why a separate namespace `Origo.Core.Planning`?**
   Follows the precedent of `Origo.Core.StateMachine`. `Planning` is an independent behavioral subsystem; it does not mix into the `Snd.Strategy` namespace.

4. **Why no built-in idle / timer steps in the base class?**
   Step types like idle, patrol, standby are game design-level concepts and should not enter the framework abstraction. If idle were built in, patrol would be equally justified, causing unbounded framework expansion. The correct approach: the user implements idle as an ordinary `LifecycleStrategyBase` Action strategy, mapped to the corresponding strategy index via `StepToActionIndex("idle")`. The framework only does scheduling orchestration, not specific behavior.

5. **Why doesn't `AfterLoad` restart the plan?**
   Action strategies (e.g., `character.action.nav_to`), as dynamically added strategies, participate in entity serialization through `SndEntity.BuildMetaData()` → `SndStrategyManager.GetStrategyIndices()`. On load, `SndEntity.RecoverForLifecycle()` → `SndStrategyManager.RecoverStrategiesOnly()` fully restores all strategies. The restored Action strategy continues executing in the next frame's `Process`; the plan naturally advances from the breakpoint without `PlanExecutionStrategyBase` explicitly restarting in `AfterLoad`. `AfterLoad` only needs to re-establish data subscription connections for `IntentKey` and `ActionStatusKey` — this is the RAII recovery of runtime non-persistent resources.

## File List

| File | Responsibility |
|------|---------------|
| `PlanExecutionStrategyBase.cs` | Plan execution base class, complete implementation |

---

[↑ Back to Origo.Core](../README.en.md)
