using System;
using System.Collections.Generic;
using System.Threading;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Tests;

// ── AsyncLocal-based collector (replaces global static for parallel safety) ──

internal static class Rec
{
    private static readonly AsyncLocal<List<string>> _log = new();

    public static void BeginTest() => _log.Value = [];

    public static IReadOnlyList<string> Log =>
        _log.Value ?? throw new InvalidOperationException("Call Rec.BeginTest() before test");

    public static void Add(string tag) => _log.Value?.Add(tag);

    public static void Reset() => _log.Value?.Clear();
}

// ── Strategy classes (all stateless for pool validation) ──

[StrategyIndex("s.p50", Priority = 50)]
internal sealed class SP50 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p50");
}

[StrategyIndex("s.p100", Priority = 100)]
internal sealed class SP100 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p100");
}

[StrategyIndex("s.p200", Priority = 200)]
internal sealed class SP200 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p200");
}

[StrategyIndex("s.p5", Priority = 5)]
internal sealed class S5 : LifecycleStrategyBase
{
}

[StrategyIndex("s.p10a", Priority = 10)]
internal sealed class S10A : LifecycleStrategyBase
{
}

[StrategyIndex("s.p10b", Priority = 10)]
internal sealed class S10B : LifecycleStrategyBase
{
}

[StrategyIndex("s.p10c", Priority = 10)]
internal sealed class S10C : LifecycleStrategyBase
{
}

[StrategyIndex("s.p15", Priority = 15)]
internal sealed class S15 : LifecycleStrategyBase
{
}

[StrategyIndex("s.p20", Priority = 20)]
internal sealed class S20 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p20");
}

[StrategyIndex("s.p25", Priority = 25)]
internal sealed class S25 : LifecycleStrategyBase
{
}

[StrategyIndex("s.p30", Priority = 30)]
internal sealed class S30 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p30");
}

[StrategyIndex("s.p40", Priority = 40)]
internal sealed class S40 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p40");
}

[StrategyIndex("s.p60", Priority = 60)]
internal sealed class S60 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p60");
}

[StrategyIndex("s.p80", Priority = 80)]
internal sealed class S80 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p80");
}

[StrategyIndex("s.p10", Priority = 10)]
internal sealed class S10 : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.p10");
}

[StrategyIndex("s.demo")]
internal sealed class SDemo : LifecycleStrategyBase
{
}

[StrategyIndex("s.a")]
internal sealed class SA : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.a");
}

[StrategyIndex("s.b")]
internal sealed class SB : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.b");
}

[StrategyIndex("s.c")]
internal sealed class SC : LifecycleStrategyBase
{
    public override void Process(ISndEntity e, double d, ISndContext c) => Rec.Add("s.c");
}

[StrategyIndex("s.n10", Priority = -10)]
internal sealed class SN10 : LifecycleStrategyBase
{
}

[StrategyIndex("s.n5", Priority = -5)]
internal sealed class SN5 : LifecycleStrategyBase
{
}

[StrategyIndex("s.n0", Priority = 0)]
internal sealed class SN0 : LifecycleStrategyBase
{
}

[StrategyIndex("s.zero", Priority = 0)]
internal sealed class S0 : LifecycleStrategyBase
{
}

[StrategyIndex("s.min", Priority = int.MinValue)]
internal sealed class SMin : LifecycleStrategyBase
{
}

[StrategyIndex("s.max", Priority = int.MaxValue)]
internal sealed class SMax : LifecycleStrategyBase
{
}

[StrategyIndex("s.lc10", Priority = 10)]
internal sealed class LC10 : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity e, ISndContext c) => Rec.Add("s.lc10");
}

[StrategyIndex("s.lc20", Priority = 20)]
internal sealed class LC20 : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity e, ISndContext c) => Rec.Add("s.lc20");
}

[StrategyIndex("s.lc30", Priority = 30)]
internal sealed class LC30 : LifecycleStrategyBase
{
    public override void AfterSpawn(ISndEntity e, ISndContext c) => Rec.Add("s.lc30");
}

[StrategyIndex("s.qv10", Priority = 10)]
internal sealed class Q10 : LifecycleStrategyBase
{
    public override void BeforeQuit(ISndEntity e, ISndContext c) => Rec.Add("s.qv10");
}

[StrategyIndex("s.qv20", Priority = 20)]
internal sealed class Q20 : LifecycleStrategyBase
{
    public override void BeforeQuit(ISndEntity e, ISndContext c) => Rec.Add("s.qv20");
}

[StrategyIndex("s.qv30", Priority = 30)]
internal sealed class Q30 : LifecycleStrategyBase
{
    public override void BeforeQuit(ISndEntity e, ISndContext c) => Rec.Add("s.qv30");
}

[StrategyIndex("s.ld10", Priority = 10)]
internal sealed class LD10 : LifecycleStrategyBase
{
    public override void AfterLoad(ISndEntity e, ISndContext c) => Rec.Add("s.ld10");
}

[StrategyIndex("s.ld20", Priority = 20)]
internal sealed class LD20 : LifecycleStrategyBase
{
    public override void AfterLoad(ISndEntity e, ISndContext c) => Rec.Add("s.ld20");
}

[StrategyIndex("s.ld30", Priority = 30)]
internal sealed class LD30 : LifecycleStrategyBase
{
    public override void AfterLoad(ISndEntity e, ISndContext c) => Rec.Add("s.ld30");
}
