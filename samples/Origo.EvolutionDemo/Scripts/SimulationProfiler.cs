using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Origo.EvolutionDemo;

/// <summary>
/// Optional, environment-gated tick profiler. Writes one JSON object per tick
/// to ORIGO_DEMO_PROFILE_PATH; disabled by default.
/// </summary>
internal sealed class SimulationProfiler
{
    private readonly string? _path;
    private readonly int _interval;
    private readonly Dictionary<string, double> _phases = new(StringComparer.Ordinal);
    private readonly Stopwatch _wall = new();
    private int _tickCount;
    private long _allocStart;
    private int _gc0Start;
    private int _gc1Start;
    private int _gc2Start;
    private StreamWriter? _writer;

    private SimulationProfiler(string? path, int interval)
    {
        _path = path;
        _interval = Math.Max(1, interval);
    }

    public static SimulationProfiler CreateFromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable("ORIGO_DEMO_PROFILE_PATH");
        var interval = int.TryParse(Environment.GetEnvironmentVariable("ORIGO_DEMO_PROFILE_INTERVAL"),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
        return new SimulationProfiler(string.IsNullOrWhiteSpace(path) ? null : path, interval);
    }

    public bool Enabled => _path is not null;

    public void Begin()
    {
        if (!Enabled)
            return;
        _phases.Clear();
        _wall.Restart();
        _allocStart = GC.GetAllocatedBytesForCurrentThread();
        _gc0Start = GC.CollectionCount(0);
        _gc1Start = GC.CollectionCount(1);
        _gc2Start = GC.CollectionCount(2);
    }

    public void RecordSystem(string id, double milliseconds)
    {
        if (!Enabled)
            return;
        _phases[id] = _phases.GetValueOrDefault(id) + milliseconds;
    }

    public void End(EvolutionTickContext context)
    {
        if (!Enabled)
            return;
        _wall.Stop();
        _tickCount++;
        if (_tickCount % _interval != 0)
            return;
        _writer ??= new StreamWriter(_path!, append: true);
        var record = new
        {
            tick = _tickCount,
            simTime = ReadFloat(context.Controller, DemoKeys.Time),
            wallMs = _wall.Elapsed.TotalMilliseconds,
            allocBytes = GC.GetAllocatedBytesForCurrentThread() - _allocStart,
            gc0 = GC.CollectionCount(0) - _gc0Start,
            gc1 = GC.CollectionCount(1) - _gc1Start,
            gc2 = GC.CollectionCount(2) - _gc2Start,
            population = context.Alive,
            food = context.FoodCount,
            corpses = context.Snapshot?.Corpses.Count ?? 0,
            phases = _phases,
        };
        _writer.WriteLine(JsonSerializer.Serialize(record));
        _writer.Flush();
    }

    private static float ReadFloat(Origo.Core.Abstractions.Entity.ISndEntity entity, string key) =>
        entity.TryGetData<float>(key) is { found: true } result ? result.value : 0f;
}
