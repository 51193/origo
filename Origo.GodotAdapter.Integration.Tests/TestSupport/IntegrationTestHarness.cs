using System;
using System.Collections.Generic;
using Godot;
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests.TestSupport;

public sealed class IntegrationTestHarness : IDisposable
{
    public GodotFileSystem FileSystem { get; }
    public TypeStringMapping TypeMapping { get; }
    public DataSourceConverterRegistry ConverterRegistry { get; }
    public IDataSourceIoGateway DataSourceIo { get; }
    public IFileMetaAccess MetaAccess { get; }
    public IPathResolver PathResolver { get; }
    public GodotSndManager SndManager { get; }
    public Blackboard SystemBlackboard { get; }
    public ILogger Logger { get; }
    public OrigoRuntime Runtime { get; }
    public SndWorld SndWorld => Runtime.SndWorld;

    public IntegrationTestHarness(ILogger? logger = null)
    {
        Logger = logger ?? new StubLogger();
        FileSystem = new GodotFileSystem();
        TypeMapping = new TypeStringMapping();
        DataSourceIo = DataSourceFactory.CreateDefaultIoGateway(FileSystem);
        MetaAccess = DataSourceFactory.CreateFileMetaAccess(FileSystem);
        PathResolver = DataSourceFactory.CreatePathResolver(FileSystem);
        ConverterRegistry = DataSourceFactory.CreateDefaultRegistry(TypeMapping);

        SndManager = new GodotSndManager { Name = "TestSndManager" };

        SystemBlackboard = new Blackboard();

        Runtime = new OrigoRuntime(
            new OrigoMeta("Origo", "test", string.Empty),
            Logger,
            SndManager,
            TypeMapping,
            ConverterRegistry,
            DataSourceIo,
            SystemBlackboard);
    }

    public void BindRuntimeDependencies() => SndManager.BindRuntimeDependencies(Runtime.SndWorld, Logger);

    public void BindContext()
    {
        var fileSystem = new GodotFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fileSystem);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fileSystem);
        var pathResolver = DataSourceFactory.CreatePathResolver(fileSystem);

        var context = new SndContext(new SndContextParameters(
            Runtime, dataSourceIo, metaAccess, pathResolver,
            "user://test_saves", "res://initial", "entry.json"));

        SndManager.BindContext(context);
    }

    public ISndEntity CreateEntity(string name)
    {
        var meta = new SndMetaData { Name = name };
        return ((ISndSceneHost)SndManager).CreateEntity(meta);
    }

    public void Dispose() => SndManager.QueueFree();
}

public sealed class StubLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public void Log(LogLevel level, string tag, string message) => Messages.Add($"[{level}] [{tag}] {message}");
}
