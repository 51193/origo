using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

[Collection("StrategyStateTests")]
public class StrategyPriorityTests
{
    private static ILogger NoLog => NullLogger.Instance;
    private static ISndContext NoCtx => NullSndContext.Instance;

    public StrategyPriorityTests()
    {
        Rec.BeginTest();
    }

    // ── Pool priority resolution ──

    [Fact]
    public void Pool_GetPriority_ReturnsExplicitPriorityFromAttribute()
    {
        var pool = new SndStrategyPool(NoLog);
        pool.Register(() => new SP100());

        Assert.Equal(100, pool.GetPriority("s.p100"));
    }

    [Fact]
    public void Pool_GetPriority_ReturnsZeroForUnknownIndex()
    {
        var pool = new SndStrategyPool(NoLog);
        Assert.Equal(0, pool.GetPriority("nonexistent"));
    }

    [Fact]
    public void Pool_GetPriority_ReturnsDefault6205WhenNotSpecified()
    {
        var pool = new SndStrategyPool(NoLog);
        pool.Register(() => new SDemo());

        Assert.Equal(6205, pool.GetPriority("s.demo"));
    }

    // ── InsertSorted: different priorities ──

    [Fact]
    public void Add_DifferentPriorities_SortedAscending()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SP50());
        pool.Register(() => new SP100());
        pool.Register(() => new SP200());

        mgr.Add(e, "s.p100", NoCtx);
        mgr.Add(e, "s.p200", NoCtx);
        mgr.Add(e, "s.p50", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p50", "s.p100", "s.p200"], indices);
    }

    [Fact]
    public void Add_SamePriority_MaintainsInsertionFifoOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SA());
        pool.Register(() => new SB());
        pool.Register(() => new SC());

        mgr.Add(e, "s.a", NoCtx);
        mgr.Add(e, "s.b", NoCtx);
        mgr.Add(e, "s.c", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.a", "s.b", "s.c"], indices);
    }

    [Fact]
    public void Add_MixedPriorities_SortedAscWithStableFifoInSamePriority()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S5());
        pool.Register(() => new S10A());
        pool.Register(() => new S10B());
        pool.Register(() => new S10C());
        pool.Register(() => new S20());

        mgr.Add(e, "s.p5", NoCtx);
        mgr.Add(e, "s.p10a", NoCtx);
        mgr.Add(e, "s.p20", NoCtx);
        mgr.Add(e, "s.p10b", NoCtx);
        mgr.Add(e, "s.p10c", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p5", "s.p10a", "s.p10b", "s.p10c", "s.p20"], indices);
    }

    [Fact]
    public void Add_InsertBetweenExisting_PositionsCorrectly()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S10A());
        pool.Register(() => new S10B());
        pool.Register(() => new S15());
        pool.Register(() => new S20());

        mgr.Add(e, "s.p10a", NoCtx);
        mgr.Add(e, "s.p10b", NoCtx);
        mgr.Add(e, "s.p20", NoCtx);
        mgr.Add(e, "s.p15", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p10a", "s.p10b", "s.p15", "s.p20"], indices);
    }

    // ── Process execution order ──

    [Fact]
    public void Process_ExecutesInPriorityAscendingOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SP50());
        pool.Register(() => new SP100());
        pool.Register(() => new SP200());

        mgr.Add(e, "s.p200", NoCtx);
        mgr.Add(e, "s.p50", NoCtx);
        mgr.Add(e, "s.p100", NoCtx);

        Rec.Reset();
        mgr.Process(e, 0.016, NoCtx);

        Assert.Equal(["s.p50", "s.p100", "s.p200"], Rec.Log);
    }

    [Fact]
    public void Process_SamePriority_ExecutesInInsertionOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SA());
        pool.Register(() => new SB());
        pool.Register(() => new SC());

        mgr.Add(e, "s.c", NoCtx);
        mgr.Add(e, "s.a", NoCtx);
        mgr.Add(e, "s.b", NoCtx);

        Rec.Reset();
        mgr.Process(e, 0.016, NoCtx);

        Assert.Equal(["s.c", "s.a", "s.b"], Rec.Log);
    }

    // ── Spawn / Load (Recover) ordering ──

    [Fact]
    public void Spawn_DifferentPriorities_SortedAscending()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S30());
        pool.Register(() => new S10());
        pool.Register(() => new S20());

        mgr.RecoverStrategiesOnly(["s.p30", "s.p10", "s.p20"]);
        mgr.TriggerAfterSpawn(e, NoCtx);

        Rec.Reset();
        mgr.Process(e, 0.016, NoCtx);

        Assert.Equal(["s.p10", "s.p20", "s.p30"], Rec.Log);
    }

    [Fact]
    public void Spawn_SamePriority_MaintainsInputOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SA());
        pool.Register(() => new SB());
        pool.Register(() => new SC());

        mgr.RecoverStrategiesOnly(["s.a", "s.b", "s.c"]);
        mgr.TriggerAfterSpawn(e, NoCtx);

        Rec.Reset();
        mgr.Process(e, 0.016, NoCtx);

        Assert.Equal(["s.a", "s.b", "s.c"], Rec.Log);
    }

    [Fact]
    public void Load_DifferentPriorities_ResortedAscending()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SP100());
        pool.Register(() => new SP50());
        pool.Register(() => new SP200());

        mgr.RecoverStrategiesOnly(["s.p200", "s.p100", "s.p50"]);
        mgr.TriggerAfterLoad(e, NoCtx);

        Rec.Reset();
        mgr.Process(e, 0.016, NoCtx);

        Assert.Equal(["s.p50", "s.p100", "s.p200"], Rec.Log);
    }

    // ── SerializeIndices / save-load roundtrip ──

    [Fact]
    public void SerializeIndices_ReturnsIndicesInPriorityOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SP50());
        pool.Register(() => new SP100());
        pool.Register(() => new SP200());

        mgr.Add(e, "s.p200", NoCtx);
        mgr.Add(e, "s.p50", NoCtx);
        mgr.Add(e, "s.p100", NoCtx);

        var saved = mgr.GetStrategyIndices();
        Assert.Equal(["s.p50", "s.p100", "s.p200"], saved);
    }

    [Fact]
    public void SaveLoadRoundtrip_MaintainsProcessingOrder()
    {
        var pool = new SndStrategyPool(NoLog);
        pool.Register(() => new SP50());
        pool.Register(() => new SP100());
        pool.Register(() => new SP200());

        var mgr = new SndStrategyManager(pool, NoLog);
        var e = new DummySndEntity("e");
        mgr.Add(e, "s.p50", NoCtx);
        mgr.Add(e, "s.p100", NoCtx);
        mgr.Add(e, "s.p200", NoCtx);

        var savedIndices = mgr.GetStrategyIndices();

        var mgr2 = new SndStrategyManager(pool, NoLog);
        var e2 = new DummySndEntity("e2");
        mgr2.RecoverStrategiesOnly(savedIndices);
        mgr2.TriggerAfterLoad(e2, NoCtx);

        Rec.Reset();
        mgr2.Process(e2, 0.016, NoCtx);

        Assert.Equal(["s.p50", "s.p100", "s.p200"], Rec.Log);
    }

    // ── Lifecycle hook ordering ──

    [Fact]
    public void AfterSpawn_ExecutesInPriorityAscendingOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new LC10());
        pool.Register(() => new LC20());
        pool.Register(() => new LC30());

        Rec.Reset();
        mgr.RecoverStrategiesOnly(["s.lc30", "s.lc20", "s.lc10"]);
        mgr.TriggerAfterSpawn(e, NoCtx);

        Assert.Equal(["s.lc10", "s.lc20", "s.lc30"], Rec.Log);
    }

    [Fact]
    public void BeforeQuit_ExecutesInPriorityAscendingOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new Q10());
        pool.Register(() => new Q20());
        pool.Register(() => new Q30());

        mgr.RecoverStrategiesOnly(["s.qv30", "s.qv10", "s.qv20"]);
        mgr.TriggerAfterSpawn(e, NoCtx);

        Rec.Reset();
        mgr.TriggerBeforeQuit(e, NoCtx);

        Assert.Equal(["s.qv10", "s.qv20", "s.qv30"], Rec.Log);
    }

    [Fact]
    public void AfterLoad_ExecutesInPriorityAscendingOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new LD10());
        pool.Register(() => new LD20());
        pool.Register(() => new LD30());

        Rec.Reset();
        mgr.RecoverStrategiesOnly(["s.ld30", "s.ld10", "s.ld20"]);
        mgr.TriggerAfterLoad(e, NoCtx);

        Assert.Equal(["s.ld10", "s.ld20", "s.ld30"], Rec.Log);
    }

    // ── Remove and re-add ──

    [Fact]
    public void Remove_Middle_RemainingOrderPreserved()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S10());
        pool.Register(() => new S20());
        pool.Register(() => new S30());

        mgr.Add(e, "s.p10", NoCtx);
        mgr.Add(e, "s.p20", NoCtx);
        mgr.Add(e, "s.p30", NoCtx);
        mgr.Remove(e, "s.p20", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p10", "s.p30"], indices);
    }

    [Fact]
    public void Remove_First_RemainingOrderPreserved()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S10());
        pool.Register(() => new S20());
        pool.Register(() => new S30());

        mgr.Add(e, "s.p10", NoCtx);
        mgr.Add(e, "s.p20", NoCtx);
        mgr.Add(e, "s.p30", NoCtx);
        mgr.Remove(e, "s.p10", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p20", "s.p30"], indices);
    }

    [Fact]
    public void Remove_Last_RemainingOrderPreserved()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S10());
        pool.Register(() => new S20());
        pool.Register(() => new S30());

        mgr.Add(e, "s.p10", NoCtx);
        mgr.Add(e, "s.p20", NoCtx);
        mgr.Add(e, "s.p30", NoCtx);
        mgr.Remove(e, "s.p30", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p10", "s.p20"], indices);
    }

    [Fact]
    public void AddAfterRemove_InsertsAtCorrectPosition()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S10());
        pool.Register(() => new S20());
        pool.Register(() => new S30());
        pool.Register(() => new S25());

        mgr.Add(e, "s.p10", NoCtx);
        mgr.Add(e, "s.p30", NoCtx);
        mgr.Remove(e, "s.p30", NoCtx);
        mgr.Add(e, "s.p20", NoCtx);
        mgr.Add(e, "s.p25", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p10", "s.p20", "s.p25"], indices);
    }

    [Fact]
    public void Remove_NonexistentStrategy_NoEffect()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SP50());

        mgr.Add(e, "s.p50", NoCtx);
        mgr.Remove(e, "nonexistent", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p50"], indices);
    }

    // ── Edge cases ──

    [Fact]
    public void SingleStrategy_Works()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SP100());

        mgr.Add(e, "s.p100", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p100"], indices);
    }

    [Fact]
    public void EmptyList_ProcessDoesNotThrow()
    {
        var pool = new SndStrategyPool(NoLog);
        var mgr = new SndStrategyManager(pool, NoLog);
        var e = new DummySndEntity("empty");

        mgr.Process(e, 0.016, NoCtx);
    }

    [Fact]
    public void EmptyList_SerializeIndicesReturnsEmpty()
    {
        var pool = new SndStrategyPool(NoLog);
        var mgr = new SndStrategyManager(pool, NoLog);

        var indices = mgr.GetStrategyIndices();
        Assert.Empty(indices);
    }

    [Fact]
    public void AllDefaultPriority6205_MaintainsInsertionOrder()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SDemo());
        pool.Register(() => new SA());
        pool.Register(() => new SB());

        mgr.Add(e, "s.demo", NoCtx);
        mgr.Add(e, "s.b", NoCtx);
        mgr.Add(e, "s.a", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.demo", "s.b", "s.a"], indices);
    }

    [Fact]
    public void NegativePriorities_SortedCorrectly()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SN10());
        pool.Register(() => new SN5());
        pool.Register(() => new SN0());
        pool.Register(() => new SP50());

        mgr.Add(e, "s.n0", NoCtx);
        mgr.Add(e, "s.p50", NoCtx);
        mgr.Add(e, "s.n10", NoCtx);
        mgr.Add(e, "s.n5", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.n10", "s.n5", "s.n0", "s.p50"], indices);
    }

    [Fact]
    public void IntMinAndIntMaxPriority_SortedCorrectly()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SMin());
        pool.Register(() => new SMax());
        pool.Register(() => new S0());

        mgr.Add(e, "s.zero", NoCtx);
        mgr.Add(e, "s.max", NoCtx);
        mgr.Add(e, "s.min", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.min", "s.zero", "s.max"], indices);
    }

    [Fact]
    public void DescendingPriorityInsertion_SortedAscending()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new SP100());
        pool.Register(() => new S80());
        pool.Register(() => new S60());
        pool.Register(() => new S40());
        pool.Register(() => new S20());

        mgr.Add(e, "s.p100", NoCtx);
        mgr.Add(e, "s.p80", NoCtx);
        mgr.Add(e, "s.p60", NoCtx);
        mgr.Add(e, "s.p40", NoCtx);
        mgr.Add(e, "s.p20", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p20", "s.p40", "s.p60", "s.p80", "s.p100"], indices);
    }

    [Fact]
    public void AscendingPriorityInsertion_SortedAscending()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S20());
        pool.Register(() => new S40());
        pool.Register(() => new S60());
        pool.Register(() => new S80());
        pool.Register(() => new SP100());

        mgr.Add(e, "s.p20", NoCtx);
        mgr.Add(e, "s.p40", NoCtx);
        mgr.Add(e, "s.p60", NoCtx);
        mgr.Add(e, "s.p80", NoCtx);
        mgr.Add(e, "s.p100", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p20", "s.p40", "s.p60", "s.p80", "s.p100"], indices);
    }

    [Fact]
    public void AlternatingPriorityInsertion_SortedCorrectly()
    {
        var (pool, mgr, e) = Setup();
        pool.Register(() => new S10());
        pool.Register(() => new SP50());
        pool.Register(() => new SP100());
        pool.Register(() => new SP200());

        mgr.Add(e, "s.p100", NoCtx);
        mgr.Add(e, "s.p10", NoCtx);
        mgr.Add(e, "s.p200", NoCtx);
        mgr.Add(e, "s.p50", NoCtx);

        var indices = mgr.GetStrategyIndices();
        Assert.Equal(["s.p10", "s.p50", "s.p100", "s.p200"], indices);
    }

    // ── Helpers ──

    private static (SndStrategyPool pool, SndStrategyManager mgr, DummySndEntity e) Setup()
    {
        var pool = new SndStrategyPool(NoLog);
        var mgr = new SndStrategyManager(pool, NoLog);
        return (pool, mgr, new DummySndEntity("test_entity"));
    }
}
