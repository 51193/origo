using System;
using System.Collections.Generic;
using System.Reflection;

namespace Origo.TestSupport;

/// <summary>
///     Central registry of test-only strategy indices. The static constructor
///     verifies that every index is unique within the registry.
/// </summary>
public static class TestStrategyIndices
{
    /// <summary>Frame-counter lifecycle strategy index.</summary>
    public const string FrameCounter = "test.frame_counter";
    /// <summary>Blackboard reader strategy index.</summary>
    public const string BlackboardReader = "test.bb_reader";
    /// <summary>Blackboard writer strategy index.</summary>
    public const string BlackboardWriter = "test.bb_writer";
    /// <summary>Blackboard marker strategy index.</summary>
    public const string BlackboardMarker = "test.bb_marker";
    /// <summary>Killable lifecycle strategy index.</summary>
    public const string Killable = "test.killable";
    /// <summary>Console-command strategy index.</summary>
    public const string ConsoleCmd = "test.console_cmd";
    /// <summary>Deferred-probe strategy index.</summary>
    public const string DeferredProbe = "test.deferred_probe";
    /// <summary>Peer-lookup strategy index.</summary>
    public const string PeerLookup = "test.peer_lookup";
    /// <summary>Integer kill-probe strategy index.</summary>
    public const string KillProbeInt = "test.kill_probe_int";
    /// <summary>Integer HP observer strategy index.</summary>
    public const string HpObserverInt = "test.hp_observer_int";
    /// <summary>Echo active strategy index.</summary>
    public const string EchoActive = "test.int.active.echo";
    /// <summary>No-op strategy index.</summary>
    public const string Noop = "test.nop";
    /// <summary>Generic lifecycle strategy index.</summary>
    public const string Lifecycle = "test.lifecycle";
    /// <summary>AfterSpawn initializer strategy index.</summary>
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
