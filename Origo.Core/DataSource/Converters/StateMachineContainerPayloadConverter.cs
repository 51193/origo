using Origo.Core.StateMachine;

namespace Origo.Core.DataSource.Converters;

internal sealed class StateMachineContainerPayloadConverter
    : DataSourceConverter<StateMachineContainerPayload>
{
    public override StateMachineContainerPayload Read(DataSourceNode node)
    {
        var payload = new StateMachineContainerPayload();

        if (node.TryGetValue("machines", out var machinesNode) && machinesNode is not null && !machinesNode.IsNull)
            foreach (var element in machinesNode.Elements)
            {
                var entry = new StateMachineEntryPayload();

                if (element.TryGetValue("key", out var keyNode) && keyNode is not null)
                    entry.Key = keyNode.AsString();

                if (element.TryGetValue("pushIndex", out var pushNode) && pushNode is not null)
                    entry.PushIndex = pushNode.AsString();

                if (element.TryGetValue("popIndex", out var popNode) && popNode is not null)
                    entry.PopIndex = popNode.AsString();

                if (element.TryGetValue("stack", out var stackNode) && stackNode is not null && !stackNode.IsNull)
                    foreach (var stackElement in stackNode.Elements)
                        entry.Stack.Add(stackElement.AsString());

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
