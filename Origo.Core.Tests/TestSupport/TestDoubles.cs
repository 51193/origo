using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.DataSource.Codec;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Save;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Tests;

internal static class TestFactory
{
    public static JsonDataSourceCodec CreateJsonCodec() => new();

    public static MapDataSourceCodec CreateMapCodec() => new();

    public static DataSourceNode NodeFromJson(string json) => CreateJsonCodec().Decode(json);

    public static string JsonFromNode(DataSourceNode node) => CreateJsonCodec().Encode(node);

    public static DataSourceConverterRegistry CreateRegistry()
    {
        var tm = new TypeStringMapping();
        return DataSourceFactory.CreateDefaultRegistry(tm);
    }

    public static DataSourceConverterRegistry CreateRegistry(
        TypeStringMapping tm) =>
        DataSourceFactory.CreateDefaultRegistry(tm);

    public static IDataSourceIoGateway CreateIoGateway(IFileSystem fileSystem) =>
        DataSourceFactory.CreateDefaultIoGateway(fileSystem);

    public static IFileMetaAccess CreateFileMetaAccess(IFileSystem fileSystem) =>
        DataSourceFactory.CreateFileMetaAccess(fileSystem);

    public static IPathResolver CreatePathResolver(IFileSystem fileSystem) =>
        DataSourceFactory.CreatePathResolver(fileSystem);

    public static SndWorld CreateSndWorld(
        TypeStringMapping? tm = null,
        ILogger? logger = null,
        IFileSystem? fileSystem = null)
    {
        tm ??= new TypeStringMapping();
        logger ??= new TestLogger();
        var reg = CreateRegistry(tm);
        return new SndWorld(tm, logger, reg, CreateIoGateway(fileSystem ?? new TestMemoryFileSystem()));
    }

    public static OrigoRuntime CreateRuntime(
        ILogger? logger = null,
        ISndSceneHost? sceneHost = null,
        TypeStringMapping? tm = null,
        IBlackboard? systemBb = null,
        OrigoMeta? meta = null)
    {
        logger ??= new TestLogger();
        sceneHost ??= new TestSndSceneHost();
        tm ??= new TypeStringMapping();
        systemBb ??= new Blackboard.Blackboard();
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        var io = CreateIoGateway(new TestMemoryFileSystem());
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, io, systemBb);
    }

    public static OrigoRuntime CreateRuntime(
        ILogger logger,
        ISndSceneHost sceneHost,
        TypeStringMapping tm,
        IBlackboard systemBb,
        IDataSourceIoGateway sharedDataSourceIo,
        OrigoMeta? meta = null)
    {
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, sharedDataSourceIo, systemBb);
    }

    public static OrigoRuntime CreateRuntime(
        ILogger logger,
        ISndSceneHost sceneHost,
        TypeStringMapping tm,
        IBlackboard systemBb,
        IFileSystem sharedFileSystem,
        OrigoMeta? meta = null)
    {
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        var io = CreateIoGateway(sharedFileSystem);
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, io, systemBb);
    }

    public static OrigoRuntime CreateRuntime(
        ILogger logger,
        ISndSceneHost sceneHost,
        TypeStringMapping tm,
        IBlackboard systemBb,
        IConsoleInputSource consoleInput,
        IConsoleOutputChannel consoleOutput,
        IDataSourceIoGateway? sharedDataSourceIo = null,
        OrigoMeta? meta = null)
    {
        meta ??= new OrigoMeta("Origo", "test", string.Empty);
        var reg = CreateRegistry(tm);
        var io = sharedDataSourceIo ?? CreateIoGateway(new TestMemoryFileSystem());
        return new OrigoRuntime(
            meta, logger, sceneHost, tm, reg, io, systemBb, consoleInput, consoleOutput);
    }

    // ── Lifecycle helpers for tests ────────────────────────────────────

    public static SystemRuntime CreateSystemRuntime(
        ILogger logger,
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        string saveRootPath,
        OrigoRuntime runtime,
        ISaveStorageService? storageService = null,
        ISavePathPolicy? savePathPolicy = null,
        IDataSourceIoGateway? sharedDataSourceIo = null)
    {
        savePathPolicy ??= new DefaultSavePathPolicy();
        storageService ??= CreateDefaultSaveStorageServiceForTests(metaAccess, runtime, pathResolver, saveRootPath, savePathPolicy, sharedDataSourceIo);
        return new SystemRuntime(runtime,
            new SystemParameters(logger, metaAccess, pathResolver, saveRootPath, storageService, savePathPolicy,
                runtime.GetAdapterSceneHost()));
    }

    public static ISessionRun BootstrapForegroundSession(
        OrigoRuntime runtime,
        IDataSourceIoGateway? dataSourceIo = null)
    {
        var fs = new TestMemoryFileSystem();
        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        var io = dataSourceIo ?? CreateIoGateway(fs);
        var metaAccess = CreateFileMetaAccess(fs);
        var pathResolver = CreatePathResolver(fs);

        var ctx = new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "initial", "entry.json"));
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        return runtime.SessionManager.ForegroundSession
               ?? throw new InvalidOperationException("Foreground session was not created.");
    }

    private static DefaultSaveStorageService CreateDefaultSaveStorageServiceForTests(
        IFileMetaAccess metaAccess, OrigoRuntime runtime, IPathResolver pathResolver,
        string saveRootPath, ISavePathPolicy savePathPolicy,
        IDataSourceIoGateway? sharedDataSourceIo = null)
    {
        return new DefaultSaveStorageService(metaAccess,
            sharedDataSourceIo ?? runtime.SndWorld.DataSourceIo,
            pathResolver,
            saveRootPath,
            savePathPolicy);
    }

    public static ProgressRun CreateProgressRun(
        string saveId,
        ILogger logger,
        IFileMetaAccess metaAccess,
        IPathResolver pathResolver,
        string saveRootPath,
        OrigoRuntime runtime,
        ISndContext sndContext,
        ISaveStorageService? storageService = null,
        ISavePathPolicy? savePathPolicy = null,
        IDataSourceIoGateway? sharedDataSourceIo = null)
    {
        var systemRuntime = CreateSystemRuntime(
            logger, metaAccess, pathResolver, saveRootPath, runtime, storageService, savePathPolicy, sharedDataSourceIo);
        return new ProgressRun(
            systemRuntime,
            new ProgressParameters(saveId),
            sndContext.StateMachineContext,
            sndContext);
    }

    public static SaveGamePayload CreateMinimalPayload(string saveId, string activeLevelId)
    {
        return new SaveGamePayload
        {
            SaveId = saveId,
            ActiveLevelId = activeLevelId,
            ProgressNode = NodeFromJson(
                """{"origo.session_topology":{"type":"String","data":"__foreground__=default=false"}}"""),
            ProgressStateMachinesNode = NodeFromJson("""{"machines":[]}"""),
            Levels = new Dictionary<string, LevelPayload>
            {
                [activeLevelId] = new()
                {
                    LevelId = activeLevelId,
                    SndSceneNode = NodeFromJson("[]"),
                    SessionNode = NodeFromJson("{}"),
                    SessionStateMachinesNode = NodeFromJson("""{"machines":[]}""")
                }
            }
        };
    }
}
