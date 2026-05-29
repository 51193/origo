using System;
using System.Collections.Generic;
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Serialization;
using Origo.Core.Snd.Metadata;

namespace Origo.GodotAdapter.Tests.TestSupport;

internal sealed class TestSndSceneHost : ISndSceneHost
{
    private readonly Dictionary<string, ISndEntity> _entities = new();

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities.Values;

    public ISndEntity? FindByName(string name) =>
        _entities.TryGetValue(name, out var entity) ? entity : null;

    public void ProcessAll(double delta)
    {
    }

    public ISndEntity Spawn(SndMetaData metaData) =>
        throw new NotSupportedException("Spawn not supported in test scene host.");

    public void ClearAll() => _entities.Clear();

    public IReadOnlyList<SndMetaData> SerializeMetaList() => Array.Empty<SndMetaData>();

    public void LoadFromMetaList(IEnumerable<SndMetaData> metaList)
    {
    }

    public void AddEntity(ISndEntity entity) => _entities[entity.Name] = entity;
}

internal sealed class TestLogger : ILogger
{
    public readonly List<string> Infos = new();
    public readonly List<string> Warnings = new();
    public readonly List<string> Errors = new();
    public readonly List<string> Debugs = new();

    public void Log(LogLevel level, string tag, string message)
    {
        var entry = $"[{tag}] {message}";
        switch (level)
        {
            case LogLevel.Debug:
                Debugs.Add(entry);
                break;
            case LogLevel.Warning:
                Warnings.Add(entry);
                break;
            case LogLevel.Error:
                Errors.Add(entry);
                break;
            default:
                Infos.Add(entry);
                break;
        }
    }
}

internal static class TestRuntimeHelper
{
    public static (OrigoRuntime runtime, TestSndSceneHost sceneHost) CreateRuntime()
    {
        var logger = new TestLogger();
        var sceneHost = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var registry = DataSourceFactory.CreateDefaultRegistry(tm);
        var io = DataSourceFactory.CreateDefaultIoGateway(new NullFileSystem());
        var bb = new Blackboard();

        var runtime = new OrigoRuntime(
            new OrigoMeta("Origo", "test", string.Empty),
            logger,
            sceneHost,
            tm,
            registry,
            io,
            bb);

        return (runtime, sceneHost);
    }
}
