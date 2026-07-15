using System;
using System.Collections.Generic;
using System.Reflection;

namespace Origo.TestSupport;

public static class TestStrategyIndices
{
    public const string FrameCounter = "test.frame_counter";
    public const string BlackboardReader = "test.bb_reader";
    public const string BlackboardWriter = "test.bb_writer";
    public const string BlackboardMarker = "test.bb_marker";
    public const string Killable = "test.killable";
    public const string ConsoleCmd = "test.console_cmd";
    public const string DeferredProbe = "test.deferred_probe";
    public const string PeerLookup = "test.peer_lookup";
    public const string KillProbeInt = "test.kill_probe_int";
    public const string HpObserverInt = "test.hp_observer_int";
    public const string EchoActive = "test.int.active.echo";
    public const string Noop = "test.nop";
    public const string Lifecycle = "test.lifecycle";
    public const string AfterSpawnInit = "test.after_spawn_init";

    static TestStrategyIndices()
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in typeof(TestStrategyIndices).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(string)) continue;
            var value = (string)field.GetValue(null)!;
            if (!values.Add(value))
                throw new InvalidOperationException(
                    $"Duplicate test strategy index '{value}' defined in {nameof(TestStrategyIndices)}.");
        }
    }
}
