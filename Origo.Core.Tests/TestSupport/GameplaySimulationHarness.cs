using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;

namespace Origo.Core.Tests.TestSupport;

internal sealed class GameplaySimulationHarness
{
    private readonly ConsoleOutputChannel _consoleOutput;
    private readonly List<string> _capturedConsoleOutput = [];

    internal GameplaySimulationHarness(
        OrigoRuntime runtime,
        SndContext context,
        ISessionRun gameSession,
        TestFileSystem fileSystem,
        TestLogger logger,
        ConsoleOutputChannel consoleOutput)
    {
        Runtime = runtime;
        Context = context;
        GameSession = gameSession;
        FileSystem = fileSystem;
        Logger = logger;
        _consoleOutput = consoleOutput;
        _consoleOutput.Subscribe(line => _capturedConsoleOutput.Add(line));
    }

    public OrigoRuntime Runtime { get; }
    public SndContext Context { get; }
    public ISessionRun GameSession { get; }
    public TestFileSystem FileSystem { get; }
    public TestLogger Logger { get; }
    public IReadOnlyList<string> ConsoleOutput => _capturedConsoleOutput;
    public IBlackboard SessionBlackboard => GameSession.SessionBlackboard;

    public static GameplaySimulationBuilder Create() => new();

    public void DriveFrame(double delta = 0.016) => ((IOrigoFrameDriver)Runtime).DriveFrame(delta);

    public void RunFrames(int count, double delta = 0.016)
    {
        for (var i = 0; i < count; i++)
            DriveFrame(delta);
    }

    public ISndEntity SpawnEntity(string name, string[] lifecycleIndices)
    {
        var meta = new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData { LifecycleIndices = [.. lifecycleIndices] },
            DataMetaData = new DataMetaData()
        };
        return GameSession.Spawn(meta);
    }

    public ISndEntity SpawnEntity(SndMetaData meta) => GameSession.Spawn(meta);

    public ISndEntity? FindEntity(string name) => GameSession.FindByName(name);

    public IReadOnlyCollection<ISndEntity> GetEntities() => GameSession.GetEntities();

    public void RequestKillEntity(string entityName) => GameSession.RequestKillEntity(entityName);

    public ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = true)
    {
        return Runtime.SessionManager.CreateBackgroundSession(key, levelId, syncProcess);
    }

    public ISessionRun? TryGetSession(string key) => Runtime.SessionManager.TryGet(key);

    public T GetEntityData<T>(string entityName, string key)
    {
        var entity = GameSession.FindByName(entityName)
                     ?? throw new InvalidOperationException($"Entity '{entityName}' not found.");
        var (found, value) = entity.TryGetData<T>(key);
        if (!found)
            throw new InvalidOperationException($"Data key '{key}' not found on entity '{entityName}'.");
        return value!;
    }

    public (bool found, T? value) TryGetEntityData<T>(string entityName, string key)
    {
        var entity = GameSession.FindByName(entityName);
        if (entity is null)
            return (false, default);
        return entity.TryGetData<T>(key);
    }
}

internal sealed class GameplaySimulationBuilder
{
    private readonly List<Action<SndWorld>> _strategyRegistrations = [];
    private readonly List<Action<IBlackboard>> _sessionConfigs = [];
    private string _entryConfigPath = "entry.json";

    public GameplaySimulationBuilder WithStrategy<T>(Func<T> factory) where T : BaseStrategy
    {
        _strategyRegistrations.Add(world => world.RegisterStrategy(factory));
        return this;
    }

    public GameplaySimulationBuilder WithSessionConfig<T>(string key, T value)
    {
        _sessionConfigs.Add(bb => bb.SetValue(key, value));
        return this;
    }

    public GameplaySimulationBuilder WithEntryConfigPath(string path)
    {
        _entryConfigPath = path;
        return this;
    }

    public GameplaySimulationHarness Build()
    {
        var logger = new TestLogger();
        var fileSystem = new TestFileSystem();
        fileSystem.SeedFile(_entryConfigPath, "[]");

        var consoleInput = new ConsoleInputBuffer();
        var consoleOutput = new ConsoleOutputChannel();

        var dataSourceIo = TestFactory.CreateIoGateway(fileSystem);
        var metaAccess = TestFactory.CreateFileMetaAccess(fileSystem);
        var pathResolver = TestFactory.CreatePathResolver(fileSystem);

        var adapterHost = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var bb = new Blackboard.Blackboard();

        var runtime = TestFactory.CreateRuntime(
            logger, adapterHost, tm, bb,
            consoleInput, consoleOutput, dataSourceIo);

        foreach (var register in _strategyRegistrations)
            register(runtime.SndWorld);

        var context = new SndContext(new SndContextParameters(
            runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "res://initial", _entryConfigPath));

        context.Lifecycle.RequestLoadMainMenuEntrySave();
        context.Deferred.FlushDeferredActionsForCurrentFrame();

        var gameSession = runtime.SessionManager.CreateBackgroundSession(
            "game", "game_level", syncProcess: true);

        var harness = new GameplaySimulationHarness(
            runtime, context, gameSession, fileSystem, logger, consoleOutput);

        foreach (var config in _sessionConfigs)
            config(harness.SessionBlackboard);

        return harness;
    }
}
