using System.Collections.Generic;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.DataSource;
using Origo.Core.Snd.Archetype;
using Xunit;

namespace Origo.Core.Tests;

public class SndArchetypeLoaderTests
{
    private static TestArchetypeFileSystem CreateFileSystem(string content) => new(content);

    [Fact]
    public void TryLoad_ValidMapFile_ReturnsAttributes()
    {
        var fs = CreateFileSystem("""
        {
            "hunger": "100",
            "speed": "1.5",
            "label": "player",
            "active": "true"
        }
        """);

        var result = SndArchetypeLoader.TryLoad(fs, "archetypes/player.map", out var attrs);

        Assert.True(result);
        Assert.Equal(4, attrs.Count);
        Assert.Equal("100", attrs["hunger"]);
        Assert.Equal("1.5", attrs["speed"]);
        Assert.Equal("player", attrs["label"]);
        Assert.Equal("true", attrs["active"]);
    }

    [Fact]
    public void TryLoad_FileNotExists_ReturnsFalse()
    {
        var fs = new TestArchetypeFileSystem(null);
        var result = SndArchetypeLoader.TryLoad(fs, "nonexistent.map", out var attrs);
        Assert.False(result);
        Assert.Empty(attrs);
    }

    [Fact]
    public void TryLoad_EmptyObject_ReturnsFalse()
    {
        var fs = CreateFileSystem("{}");
        var result = SndArchetypeLoader.TryLoad(fs, "archetypes/empty.map", out var attrs);
        Assert.False(result);
        Assert.Empty(attrs);
    }

    [Fact]
    public void TryLoad_NonObjectNode_ReturnsFalse()
    {
        var fs = CreateFileSystem("\"hello\"");
        var result = SndArchetypeLoader.TryLoad(fs, "archetypes/bad.map", out var attrs);
        Assert.False(result);
        Assert.Empty(attrs);
    }

    [Fact]
    public void ApplyAttributes_IntString_StoresAsInt()
    {
        var entity = new TestArchetypeEntity();
        var attrs = new Dictionary<string, string> { ["hp"] = "100" };
        SndArchetypeLoader.ApplyAttributes(entity, attrs);

        var (found, val) = entity.TryGetData<int>("hp");
        Assert.True(found);
        Assert.Equal(100, val);
    }

    [Fact]
    public void ApplyAttributes_LargeIntegerString_StoresAsLong()
    {
        var entity = new TestArchetypeEntity();
        var attrs = new Dictionary<string, string> { ["population"] = "10000000000" };
        SndArchetypeLoader.ApplyAttributes(entity, attrs);

        // Exceeds int.MaxValue; it must be stored as long without precision loss
        // instead of being silently coerced to float.
        var (foundLong, longVal) = entity.TryGetData<long>("population");
        Assert.True(foundLong);
        Assert.Equal(10000000000L, longVal);

        var (foundFloat, _) = entity.TryGetData<float>("population");
        Assert.False(foundFloat);
    }

    [Fact]
    public void ApplyAttributes_FloatString_StoresAsFloat()
    {
        var entity = new TestArchetypeEntity();
        var attrs = new Dictionary<string, string> { ["speed"] = "3.14" };
        SndArchetypeLoader.ApplyAttributes(entity, attrs);

        var (found, val) = entity.TryGetData<float>("speed");
        Assert.True(found);
        Assert.Equal(3.14f, val);
    }

    [Fact]
    public void ApplyAttributes_BoolString_StoresAsBool()
    {
        var entity = new TestArchetypeEntity();
        var attrs = new Dictionary<string, string> { ["active"] = "true" };
        SndArchetypeLoader.ApplyAttributes(entity, attrs);

        var (found, val) = entity.TryGetData<bool>("active");
        Assert.True(found);
        Assert.True(val);
    }

    [Fact]
    public void ApplyAttributes_PlainString_StoresAsString()
    {
        var entity = new TestArchetypeEntity();
        var attrs = new Dictionary<string, string> { ["name"] = "hero" };
        SndArchetypeLoader.ApplyAttributes(entity, attrs);

        var (found, val) = entity.TryGetData<string>("name");
        Assert.True(found);
        Assert.Equal("hero", val);
    }

    private sealed class TestArchetypeFileSystem(string? content) : ISndFileAccess
    {
        private readonly string? _content = content;

        public bool FileExists(string path) => _content is not null;

        public DataSourceNode ReadFile(string path) =>
            _content is not null
                ? ParseJson(_content)
                : throw new System.IO.FileNotFoundException();

        private static DataSourceNode ParseJson(string json)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return ConvertElement(doc.RootElement);
        }

        private static DataSourceNode ConvertElement(System.Text.Json.JsonElement element)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    var obj = DataSourceNode.CreateObject();
                    foreach (var prop in element.EnumerateObject())
                        obj.Add(prop.Name, ConvertElement(prop.Value));
                    return obj;
                case System.Text.Json.JsonValueKind.Array:
                    var arr = DataSourceNode.CreateArray();
                    foreach (var item in element.EnumerateArray())
                        arr.Add(ConvertElement(item));
                    return arr;
                case System.Text.Json.JsonValueKind.String:
                    return DataSourceNode.CreateString(element.GetString()!);
                case System.Text.Json.JsonValueKind.Number:
                    return DataSourceNode.CreateNumber(element.GetRawText());
                case System.Text.Json.JsonValueKind.True:
                    return DataSourceNode.CreateBoolean(true);
                case System.Text.Json.JsonValueKind.False:
                    return DataSourceNode.CreateBoolean(false);
                default:
                    return DataSourceNode.CreateNull();
            }
        }

        public T ReadObject<T>(string path) =>
            throw new System.NotImplementedException();

        public void WriteFile(string path, DataSourceNode node, bool overwrite = true) =>
            throw new System.NotImplementedException();

        public void WriteObject<T>(string path, T obj, bool overwrite = true) =>
            throw new System.NotImplementedException();

        public void DeleteFile(string path) =>
            throw new System.NotImplementedException();
    }

    private sealed class TestArchetypeEntity : Origo.Core.Abstractions.Entity.ISndEntity
    {
        public ISessionRun OwningSession { get; set; } = null!;
        private readonly Dictionary<string, object> _data = [];

        public string Name => "test";
        public bool IsPendingKill => false;

        public void SetData<T>(string name, T value) => _data[name] = value!;
        public T GetData<T>(string name) where T : notnull => throw new System.NotImplementedException();

        public (bool found, T? value) TryGetData<T>(string name)
        {
            if (_data.TryGetValue(name, out var v) && v is T tv)
                return (true, tv);
            return (false, default);
        }

        public void MountObserverStrategy(string targetName, string observerIndex) { }

        public void UnmountObserverStrategy(string targetName, string observerIndex) { }
        public void MountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
        public void UnmountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }

        public Origo.Core.Abstractions.Node.INodeHandle GetNode(string name) => throw new System.NotImplementedException();
        public System.Collections.Generic.IReadOnlyCollection<string> GetNodeNames() => throw new System.NotImplementedException();
        public void AddStrategy(string index) => throw new System.NotImplementedException();
        public void RemoveStrategy(string index) => throw new System.NotImplementedException();
        public void AddActiveStrategy(string index) => throw new System.NotImplementedException();
        public void RemoveActiveStrategy(string index) => throw new System.NotImplementedException();
        public object? InvokeStrategy(string strategyIndex, object? input = null) => throw new System.NotImplementedException();

    }
}
