using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Origo.Core.DataSource.Codec;

/// <summary>
///     JSON codec based on System.Text.Json, supporting lazy expansion.
/// </summary>
internal sealed class JsonDataSourceCodec(bool writeIndented = true) : IDataSourceCodec
{
    private readonly bool _writeIndented = writeIndented;

    public DataSourceNode Decode(string rawText) => DataSourceNode.CreateLazy(rawText, ExpandOneLevel);

    public string Encode(DataSourceNode node)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = _writeIndented });

        WriteNode(writer, node, []);
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

    private static void WriteNode(
        Utf8JsonWriter writer, DataSourceNode node, HashSet<DataSourceNode> active)
    {
        if (!active.Add(node))
            throw new InvalidOperationException(
                "DataSourceNode graph contains a cycle; a node tree must be acyclic and cannot be encoded.");

        try
        {
            switch (node.Kind)
            {
                case DataSourceNodeKind.Map:
                    writer.WriteStartObject();
                    foreach (var key in node.Keys)
                        WriteProperty(writer, key, node[key], active);
                    writer.WriteEndObject();
                    break;

                case DataSourceNodeKind.Array:
                    writer.WriteStartArray();
                    foreach (var element in node.Elements)
                        WriteNode(writer, element, active);
                    writer.WriteEndArray();
                    break;

                case DataSourceNodeKind.Text:
                    WriteTextValue(writer, node);
                    break;

                case DataSourceNodeKind.Number:
                    writer.WriteRawValue(node.AsString());
                    break;

                case DataSourceNodeKind.Bool:
                    writer.WriteBooleanValue(node.As<bool>());
                    break;

                case DataSourceNodeKind.Null:
                    writer.WriteNullValue();
                    break;
            }
        }
        finally
        {
            active.Remove(node);
        }
    }

    private static void WriteTextValue(Utf8JsonWriter writer, DataSourceNode node)
    {
        var value = node.AsString();
        // The writer's replacement fallback would collapse an unpaired UTF-16
        // surrogate into U+FFFD, silently changing the value on read-back.
        Utf16Text.ThrowIfNotWellFormed(value, "JSON text value");
        writer.WriteStringValue(value);
    }

    private static void WriteProperty(
        Utf8JsonWriter writer, string key, DataSourceNode node, HashSet<DataSourceNode> active)
    {
        // The default encoder would replace an unpaired surrogate with U+FFFD,
        // silently changing the key on read-back. Reject it instead.
        Utf16Text.ThrowIfNotWellFormed(key, "JSON object key");
        writer.WritePropertyName(key);
        WriteNode(writer, node, active);
    }
}
