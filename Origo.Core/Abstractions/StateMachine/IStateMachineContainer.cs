namespace Origo.Core.Abstractions.StateMachine;

/// <summary>
///     Container that manages multiple <see cref="IStateMachine" /> instances,
///     creating / looking up / removing them by key. The strategy layer creates
///     session-level state machines through this interface without depending on
///     a concrete container implementation.
/// </summary>
public interface IStateMachineContainer
{
    /// <summary>
    ///     Create or get a state machine by key. Throws if the key already exists
    ///     but with different strategy indices.
    /// </summary>
    IStateMachine CreateOrGet(string machineKey, string pushStrategyIndex, string popStrategyIndex);

    /// <summary>Look up an existing state machine by key.</summary>
    bool TryGet(string machineKey, out IStateMachine? machine);

    /// <summary>Remove and release a state machine by key.</summary>
    void Remove(string machineKey);

    /// <summary>Release all state machines and clear the container.</summary>
    void Clear();
}
