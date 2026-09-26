using System;
using Origo.Core.StateMachine;

namespace Origo.Core.DataSource.Converters;

internal sealed class StateMachineContainerPayloadConverter
    : DataSourceConverter<StateMachineContainerPayload>
{
    public override StateMachineContainerPayload Read(DataSourceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Kind != DataSourceNodeKind.Map)
            throw new InvalidOperationException(
                $"State machine payload must be a JSON object ({{ \"machines\": [...] }}), but found {node.Kind}.");

        if (!node.TryGetValue("machines", out var machinesNode) || machinesNode is null || machinesNode.IsNull)
            throw new InvalidOperationException(
                "State machine payload is missing the required 'machines' array.");

        if (machinesNode.Kind != DataSourceNodeKind.Array)
            throw new InvalidOperationException(
                $"State machine payload 'machines' must be a JSON array, but found {machinesNode.Kind}.");

        var payload = new StateMachineContainerPayload();
        foreach (var element in machinesNode.Elements)
        {
            var entry = new StateMachineEntryPayload();

            // The identity fields are always written by the framework; a
            // payload missing them is corrupt and must fail the strict read
            // instead of silently producing empty-key state machine entries.
            if (!element.TryGetValue("key", out var keyNode) || keyNode is null || keyNode.IsNull)
                throw new InvalidOperationException(
                    "State machine payload entry is missing the required 'key' string.");
            entry.Key = keyNode.AsString();

            if (!element.TryGetValue("pushIndex", out var pushNode) || pushNode is null || pushNode.IsNull)
                throw new InvalidOperationException(
                    "State machine payload entry is missing the required 'pushIndex' string.");
            entry.PushIndex = pushNode.AsString();

            if (!element.TryGetValue("popIndex", out var popNode) || popNode is null || popNode.IsNull)
                throw new InvalidOperationException(
                    "State machine payload entry is missing the required 'popIndex' string.");
            entry.PopIndex = popNode.AsString();

            if (element.TryGetValue("stack", out var stackNode) && stackNode is not null && !stackNode.IsNull)
            {
                // A non-array stack node is corrupt data: iterating a wrong
                // shape would silently produce an empty stack and lose the
                // machine state.
                if (stackNode.Kind != DataSourceNodeKind.Array)
                    throw new InvalidOperationException(
                        $"State machine payload entry '{entry.Key}' has a stack field that is {stackNode.Kind}, not a JSON array. " +
                        "The save data is corrupt and cannot be recovered.");

                foreach (var stackElement in stackNode.Elements)
                    entry.Stack.Add(StringDataSourceConverter.ReadElement(stackElement));
            }

            payload.Machines.Add(entry);
        }

        return payload;
    }

    public override DataSourceNode Write(StateMachineContainerPayload value)
    {
        var machines = DataSourceNode.CreateArray();

        foreach (var entry in value.Machines)
        {
            var entryNode = DataSourceNode.CreateObject();

            entryNode.Add("key", DataSourceNode.CreateString(entry.Key));
            entryNode.Add("pushIndex", DataSourceNode.CreateString(entry.PushIndex));
            entryNode.Add("popIndex", DataSourceNode.CreateString(entry.PopIndex));

            var stack = DataSourceNode.CreateArray();
            foreach (var item in entry.Stack)
                stack.Add(DataSourceNode.CreateString(item));
            entryNode.Add("stack", stack);

            machines.Add(entryNode);
        }

        return DataSourceNode.CreateObject()
            .Add("machines", machines);
    }
}
