using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origo.Core.DataSource.Converters;

internal sealed class StringDictionaryConverter : DataSourceConverter<IReadOnlyDictionary<string, string>>
{
    public override IReadOnlyDictionary<string, string> Read(DataSourceNode node)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in node.Keys)
            dict[key] = node[key].AsString();
        return new ReadOnlyDictionary<string, string>(dict);
    }

    public override DataSourceNode Write(IReadOnlyDictionary<string, string> value)
    {
        var obj = DataSourceNode.CreateObject();
        foreach (var kvp in value)
            obj.Add(kvp.Key, DataSourceNode.CreateString(kvp.Value));
        return obj;
    }
}
