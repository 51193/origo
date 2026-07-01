using System.Collections.Generic;

namespace Origo.Core.StateMachine;

/// <summary>
///     可序列化的状态机容器快照（存档 / 读档）。
/// </summary>
internal sealed class StateMachineContainerPayload
{
    public List<StateMachineEntryPayload> Machines { get; set; } = [];
}

internal sealed class StateMachineEntryPayload
{
    public string Key { get; set; } = string.Empty;

    public string PushIndex { get; set; } = string.Empty;

    public string PopIndex { get; set; } = string.Empty;

    public List<string> Stack { get; set; } = [];
}
