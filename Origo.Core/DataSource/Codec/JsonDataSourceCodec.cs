using System.IO;
using System.Text;
using System.Text.Json;

namespace Origo.Core.DataSource;

/// <summary>
///     基于 System.Text.Json 的 JSON 编解码器，支持延迟展开。
/// </summary>
internal sealed class JsonDataSourceCodec : IDataSourceCodec
{
    private readonly bool _writeIndented;

    public JsonDataSourceCodec(bool writeIndented = true)
    {
        _writeIndented = writeIndented;
    }

    public DataSourceNode Decode(string rawText) => DataSourceNode.CreateLazy(rawText, ExpandOneLevel);

    public string Encode(DataSourceNode node)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = _writeIndented });

        WriteNode(writer, node);
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static DataSourceNode ExpandOneLevel(string rawText)
    {
        using var doc = JsonDocument.Parse(rawText);
        var root = doc.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Object => ExpandObject(root),
            JsonValueKind.Array => ExpandArray(root),
            JsonValueKind.String => DataSourceNode.CreateString(root.GetString()!),
            JsonValueKind.Number => DataSourceNode.CreateNumber(root.GetRawText()),
            JsonValueKind.True => DataSourceNode.CreateBoolean(true),
            JsonValueKind.False => DataSourceNode.CreateBoolean(false),
            JsonValueKind.Null => DataSourceNode.CreateNull(),
            _ => DataSourceNode.CreateNull()
        };
    }

    private static DataSourceNode ExpandObject(JsonElement element)
    {
        var node = DataSourceNode.CreateObject();

        foreach (var property in element.EnumerateObject())
        {
            var child = property.Value.ValueKind switch
            {
                JsonValueKind.Object or JsonValueKind.Array =>
                    DataSourceNode.CreateLazy(property.Value.GetRawText(), ExpandOneLevel),
                JsonValueKind.String => DataSourceNode.CreateString(property.Value.GetString()!),
                JsonValueKind.Number => DataSourceNode.CreateNumber(property.Value.GetRawText()),
                JsonValueKind.True => DataSourceNode.CreateBoolean(true),
                JsonValueKind.False => DataSourceNode.CreateBoolean(false),
                _ => DataSourceNode.CreateNull()
            };

            node.Add(property.Name, child);
        }

        return node;
    }

    private static DataSourceNode ExpandArray(JsonElement element)
    {
        var node = DataSourceNode.CreateArray();

        foreach (var item in element.EnumerateArray())
        {
            var child = item.ValueKind switch
            {
                JsonValueKind.Object or JsonValueKind.Array =>
                    DataSourceNode.CreateLazy(item.GetRawText(), ExpandOneLevel),
                JsonValueKind.String => DataSourceNode.CreateString(item.GetString()!),
                JsonValueKind.Number => DataSourceNode.CreateNumber(item.GetRawText()),
                JsonValueKind.True => DataSourceNode.CreateBoolean(true),
                JsonValueKind.False => DataSourceNode.CreateBoolean(false),
                _ => DataSourceNode.CreateNull()
            };

            node.Add(child);
        }

        return node;
    }

    private static void WriteNode(Utf8JsonWriter writer, DataSourceNode node)
    {
        switch (node.Kind)
        {
            case DataSourceNodeKind.Object:
                writer.WriteStartObject();
                foreach (var key in node.Keys)
                    WriteProperty(writer, key, node[key]);
                writer.WriteEndObject();
                break;

            case DataSourceNodeKind.Array:
                writer.WriteStartArray();
                foreach (var element in node.Elements)
                    WriteNode(writer, element);
                writer.WriteEndArray();
                break;

            case DataSourceNodeKind.String:
                writer.WriteStringValue(node.AsString());
                break;

            case DataSourceNodeKind.Number:
                writer.WriteRawValue(node.AsString());
                break;

            case DataSourceNodeKind.Boolean:
                writer.WriteBooleanValue(node.AsBool());
                break;

            case DataSourceNodeKind.Null:
                writer.WriteNullValue();
                break;
        }
    }

    private static void WriteProperty(Utf8JsonWriter writer, string key, DataSourceNode node)
    {
        switch (node.Kind)
        {
            case DataSourceNodeKind.Object:
                writer.WriteStartObject(key);
                foreach (var childKey in node.Keys)
                    WriteProperty(writer, childKey, node[childKey]);
                writer.WriteEndObject();
                break;

            case DataSourceNodeKind.Array:
                writer.WriteStartArray(key);
                foreach (var element in node.Elements)
                    WriteNode(writer, element);
                writer.WriteEndArray();
                break;

            case DataSourceNodeKind.String:
                writer.WriteString(key, node.AsString());
                break;

            case DataSourceNodeKind.Number:
                writer.WritePropertyName(key);
                writer.WriteRawValue(node.AsString());
                break;

            case DataSourceNodeKind.Boolean:
                writer.WriteBoolean(key, node.AsBool());
                break;

            case DataSourceNodeKind.Null:
                writer.WriteNull(key);
                break;
        }
    }
}
