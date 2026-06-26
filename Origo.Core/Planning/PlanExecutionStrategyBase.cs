using System;
using System.Runtime.CompilerServices;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Planning;

/// <summary>
///     Base class for strategies that manage intent-driven plan execution.
///     Handles subscription wiring, action strategy plug/unplug, and plan advancement.
///     The derived class provides the domain-specific plan structure
///     via <see cref="ResolveNextStep" /> and <see cref="StepToActionIndex" />.
/// </summary>
public abstract class PlanExecutionStrategyBase : LifecycleStrategyBase
{
    private static readonly ConditionalWeakTable<ISndEntity, WireCallbacks> WiredCallbacks = new();

    /// <summary>Entity data key for the current intent (e.g. "combat", "forage", "wander").</summary>
    protected abstract string IntentKey { get; }

    /// <summary>Entity data key for the intent execution status.</summary>
    protected abstract string IntentStatusKey { get; }

    /// <summary>Entity data key for the current plan step type.</summary>
    protected abstract string PlanStepKey { get; }

    /// <summary>Entity data key for the current action descriptor.</summary>
    protected abstract string ActionKey { get; }

    /// <summary>Entity data key for the action execution status.</summary>
    protected abstract string ActionStatusKey { get; }

    /// <summary>
    ///     Given an intent name, the current step, and whether the previous step failed,
    ///     return the next step type, or <c>null</c>/empty to end the plan.
    /// </summary>
    protected abstract string? ResolveNextStep(string intent, string currentStep, bool failed, ISndEntity entity);

    /// <summary>
    ///     Maps a step type to the strategy index of the action strategy that executes it.
    ///     Return <c>null</c>/empty if the step does not require an action strategy.
    /// </summary>
    protected abstract string? StepToActionIndex(string stepType);

    /// <summary>Status value written when an intent first becomes active.</summary>
    protected virtual string IntentStatusActive => "active";

    /// <summary>Status value written when a plan completes successfully.</summary>
    protected virtual string IntentStatusCompleted => "completed";

    /// <summary>Status value written when an action step begins execution.</summary>
    protected virtual string ActionStatusExecuting => "executing";

    /// <summary>Status value written when an action step completes successfully.</summary>
    protected virtual string ActionStatusCompleted => "completed";

    /// <summary>Status value written when an action step fails.</summary>
    protected virtual string ActionStatusFailed => "failed";

    // ── Sealed lifecycle hooks ────────────────────────────────────────────

    /// <inheritdoc />
    public sealed override void AfterSpawn(ISndEntity entity, ISndContext ctx)
    {
        Wire(entity, true);
        OnAfterSpawn(entity, ctx);
    }

    /// <inheritdoc />
    public sealed override void AfterLoad(ISndEntity entity, ISndContext ctx)
    {
        Wire(entity, false);
        OnAfterLoad(entity, ctx);
    }

    /// <inheritdoc />
    public sealed override void AfterAdd(ISndEntity entity, ISndContext ctx)
    {
        Wire(entity, true);
        OnAfterAdd(entity, ctx);
    }

    /// <inheritdoc />
    public sealed override void BeforeRemove(ISndEntity entity, ISndContext ctx)
    {
        RemoveCurrentAction(entity);
        OnBeforeRemove(entity, ctx);
        Unwire(entity);
    }

    /// <inheritdoc />
    public sealed override void BeforeQuit(ISndEntity entity, ISndContext ctx)
    {
        RemoveCurrentAction(entity);
        OnBeforeQuit(entity, ctx);
        Unwire(entity);
    }

    /// <inheritdoc />
    public sealed override void BeforeDead(ISndEntity entity, ISndContext ctx)
    {
        RemoveCurrentAction(entity);
        OnBeforeDead(entity, ctx);
        Unwire(entity);
    }

    /// <inheritdoc />
    public sealed override void BeforeSave(ISndEntity entity, ISndContext ctx)
    {
        OnBeforeSave(entity, ctx);
    }

    /// <inheritdoc />
    public sealed override void Process(ISndEntity entity, double delta, ISndContext ctx)
    {
        OnProcess(entity, delta, ctx);
    }

    // ── Virtual extension hooks ────────────────────────────────────────────

    /// <summary>Called after the sealed <see cref="AfterSpawn" /> wiring is complete.</summary>
    protected virtual void OnAfterSpawn(ISndEntity entity, ISndContext ctx) { }

    /// <summary>Called after the sealed <see cref="AfterLoad" /> wiring is complete.</summary>
    protected virtual void OnAfterLoad(ISndEntity entity, ISndContext ctx) { }

    /// <summary>Called after the sealed <see cref="AfterAdd" /> wiring is complete.</summary>
    protected virtual void OnAfterAdd(ISndEntity entity, ISndContext ctx) { }

    /// <summary>Called before the sealed <see cref="BeforeRemove" /> unwiring begins.</summary>
    protected virtual void OnBeforeRemove(ISndEntity entity, ISndContext ctx) { }

    /// <summary>Called before the sealed <see cref="BeforeQuit" /> unwiring begins.</summary>
    protected virtual void OnBeforeQuit(ISndEntity entity, ISndContext ctx) { }

    /// <summary>Called before the sealed <see cref="BeforeDead" /> unwiring begins.</summary>
    protected virtual void OnBeforeDead(ISndEntity entity, ISndContext ctx) { }

    /// <summary>Called from the sealed <see cref="BeforeSave" />.</summary>
    protected virtual void OnBeforeSave(ISndEntity entity, ISndContext ctx) { }

    /// <summary>Called after the sealed <see cref="Process" /> is complete.</summary>
    protected virtual void OnProcess(ISndEntity entity, double delta, ISndContext ctx) { }

    /// <summary>Called when a new intent begins execution.</summary>
    protected virtual void OnIntentStarted(ISndEntity entity, string intent) { }

    /// <summary>Called when a plan step begins execution.</summary>
    protected virtual void OnStepStarted(ISndEntity entity, string stepType) { }

    /// <summary>Called when a plan completes successfully (all steps finished).</summary>
    protected virtual void OnPlanCompleted(ISndEntity entity) { }

    /// <summary>Called when a plan fails (no recovery step available).</summary>
    protected virtual void OnPlanFailed(ISndEntity entity) { }

    // ── Private: signal wiring ────────────────────────────────────────────

    private void Wire(ISndEntity entity, bool initialize)
    {
        Unwire(entity);

        var raw = (ISndEntityRawSubscription)entity;

        var intentCb = new Action<ISndEntity, TypedData, TypedData>((t, o, n) => OnIntentChanged(t, entity, o, n));
        var actionCb = new Action<ISndEntity, TypedData, TypedData>((t, o, n) => OnActionStatusChanged(t, entity, o, n));

        raw.SubscribeDataRaw(IntentKey, intentCb, null);
        raw.SubscribeDataRaw(ActionStatusKey, actionCb, null);

        WiredCallbacks.AddOrUpdate(entity, new WireCallbacks { IntentCb = intentCb, ActionCb = actionCb });

        if (!initialize)
            return;

        var (found, intent) = entity.TryGetData<string>(IntentKey);
        if (found && !string.IsNullOrEmpty(intent))
            StartIntent(entity, intent);
    }

    private void Unwire(ISndEntity entity)
    {
        if (!WiredCallbacks.TryGetValue(entity, out var cbs))
            return;

        var raw = (ISndEntityRawSubscription)entity;
        raw.UnsubscribeDataRaw(IntentKey, cbs.IntentCb);
        raw.UnsubscribeDataRaw(ActionStatusKey, cbs.ActionCb);

        WiredCallbacks.Remove(entity);
    }

    // ── Private: signal handlers ─────────────────────────────────────────

    private void OnIntentChanged(ISndEntity target, ISndEntity observer, TypedData oldValue, TypedData newValue)
    {
        if (!newValue.TryGetString(out var intent) || string.IsNullOrEmpty(intent))
            return;
        StartIntent(target, intent);
    }

    private void OnActionStatusChanged(ISndEntity target, ISndEntity observer, TypedData oldValue, TypedData newValue)
    {
        if (!newValue.TryGetString(out var status))
            return;
        if (status != ActionStatusCompleted && status != ActionStatusFailed)
            return;

        target.SetData(ActionStatusKey, "");

        var failed = status == ActionStatusFailed;
        AdvancePlan(target, failed);
    }

    // ── Private: plan engine ─────────────────────────────────────────────

    private void StartIntent(ISndEntity entity, string intent)
    {
        RemoveCurrentAction(entity);
        entity.SetData(PlanStepKey, "");

        OnIntentStarted(entity, intent);

        var step = ResolveNextStep(intent, "", false, entity);
        if (!string.IsNullOrEmpty(step))
            PushAction(entity, step);
    }

    private void AdvancePlan(ISndEntity entity, bool failed)
    {
        var (foundIntent, intent) = entity.TryGetData<string>(IntentKey);
        if (!foundIntent || string.IsNullOrEmpty(intent))
            return;

        var (foundStep, step) = entity.TryGetData<string>(PlanStepKey);
        var currentStep = foundStep && step is not null ? step : "";

        var nextStep = ResolveNextStep(intent, currentStep, failed, entity);

        if (string.IsNullOrEmpty(nextStep))
        {
            RemoveCurrentAction(entity);
            entity.SetData(PlanStepKey, "");
            entity.SetData(IntentKey, "");
            entity.SetData(IntentStatusKey, IntentStatusCompleted);
            if (failed)
                OnPlanFailed(entity);
            else
                OnPlanCompleted(entity);
            return;
        }

        PushAction(entity, nextStep);
    }

    private void PushAction(ISndEntity entity, string stepType)
    {
        RemoveCurrentAction(entity);

        entity.SetData(PlanStepKey, stepType);

        var (foundAction, currentAction) = entity.TryGetData<string>(ActionKey);
        var currentPrefix = foundAction && currentAction is not null ? currentAction.Split(',')[0] : "";

        if (currentPrefix != stepType)
            entity.SetData(ActionKey, stepType);

        entity.SetData(ActionStatusKey, ActionStatusExecuting);

        OnStepStarted(entity, stepType);

        var strategyIndex = StepToActionIndex(stepType);
        if (!string.IsNullOrEmpty(strategyIndex))
            entity.AddStrategy(strategyIndex);
    }

    private void RemoveCurrentAction(ISndEntity entity)
    {
        var (foundStep, step) = entity.TryGetData<string>(PlanStepKey);
        if (!foundStep || string.IsNullOrEmpty(step))
            return;

        var strategyIndex = StepToActionIndex(step);
        if (!string.IsNullOrEmpty(strategyIndex))
            entity.RemoveStrategy(strategyIndex);
    }

    private sealed class WireCallbacks
    {
        public required Action<ISndEntity, TypedData, TypedData> IntentCb { get; init; }
        public required Action<ISndEntity, TypedData, TypedData> ActionCb { get; init; }
    }
}
