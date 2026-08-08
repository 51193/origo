using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save.Meta;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Tests;

internal sealed class StrategyTestContext : ISndContext, ISndBlackboardAccess, ISndDeferredActions,
    ISndTemplateAccess, ISndConsoleAccess, ISndStateMachineAccess, ISndSaveOperations,
    ISndLifecycleOperations, IStateMachineContext
{
    private readonly List<string> _consoleCommands = [];
    private readonly List<string> _consoleOutput = [];
    private readonly Queue<Action> _deferred = new();
    private readonly Dictionary<string, SndMetaData> _templates = new(StringComparer.Ordinal);
    private readonly IFileMetaAccess _metaAccess;
    private readonly IDataSourceIoGateway _dataSourceIo;
    private readonly IPathResolver _pathResolver;
    private readonly DataSourceConverterRegistry _converterRegistry;

    public StrategyTestContext()
    {
        var session = new TestSessionRun();
        SessionManager = new TestSessionManager(session);
        ((IOwningSessionBindable)session.SceneHost).SetOwningSession(session);

        var fileSystem = new TestMemoryFileSystem();
        _metaAccess = DataSourceFactory.CreateFileMetaAccess(fileSystem);
        _dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fileSystem);
        _pathResolver = DataSourceFactory.CreatePathResolver(fileSystem);
        _converterRegistry = DataSourceFactory.CreateDefaultRegistry(new TypeStringMapping());

        FileAccess = new StrategyFileAccess(_dataSourceIo, _metaAccess, _converterRegistry);
        ArchiveFileAccess = new StrategyArchiveFileAccess(_dataSourceIo, _metaAccess, _converterRegistry);
    }

    public void Bootstrap()
    {
    }

    public string SaveRootPath => string.Empty;
    public string InitialSaveRootPath => string.Empty;
    public string EntryConfigPath => string.Empty;

    public ISndBlackboardAccess Blackboard => this;
    public ISndDeferredActions Deferred => this;
    public ISndTemplateAccess Template => this;
    public ISndConsoleAccess ConsoleAccess => this;
    public ISndStateMachineAccess StateMachines => this;
    public ISndSaveOperations Save => this;
    public ISndLifecycleOperations Lifecycle => this;
    public ISndFileAccess FileAccess { get; }
    public ISndArchiveFileAccess ArchiveFileAccess { get; }
    public IStateMachineContext StateMachineContext => this;

    public List<string> SaveRequests { get; } = [];

    public List<string> LoadRequests { get; } = [];

    public List<string> LevelSwitchRequests { get; } = [];

    public int DeferredActionCount { get; private set; }

    public IReadOnlyList<string> ConsoleCommands => _consoleCommands;

    public IReadOnlyList<string> ConsoleOutput => _consoleOutput;

    public IBlackboard SystemBlackboard { get; } = new Blackboard.Blackboard();

    public IBlackboard ProgressBlackboard { get; } = new Blackboard.Blackboard();

    public ISessionManager SessionManager { get; }

    public void EnqueueBusinessDeferred(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _deferred.Enqueue(action);
    }

    public void FlushDeferredActionsForCurrentFrame()
    {
        while (_deferred.Count > 0)
        {
            _deferred.Dequeue()();
            DeferredActionCount++;
        }
    }

    public int GetPendingPersistenceRequestCount() => 0;

    public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
    {
        if (!_templates.TryGetValue(templateKey, out var template))
            throw new InvalidOperationException($"Template '{templateKey}' is not registered in the test context.");
        var clone = template.DeepClone();
        if (overrideName is not null)
            clone.Name = overrideName;
        return clone;
    }

    public bool TrySubmitConsoleCommand(string commandLine)
    {
        _consoleCommands.Add(commandLine);
        return true;
    }

    public void ProcessConsolePending()
    {
    }

    public long SubscribeConsoleOutput(Action<string> onLine) => 0L;

    public void UnsubscribeConsoleOutput(long subscriptionId)
    {
    }

    public IStateMachineContainer? GetProgressStateMachines() => null;

    public IReadOnlyList<string> ListSaves() => [];

    public void RequestLoadGame(string saveId) => LoadRequests.Add(saveId);

    public void RequestSaveGame(string newSaveId) => SaveRequests.Add(newSaveId);

    public string RequestSaveGameAuto(string? newSaveId = null)
    {
        var id = newSaveId ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        SaveRequests.Add(id);
        return id;
    }

    public void SetContinueTarget(string saveId)
    {
    }

    public void RequestSwitchForegroundLevel(string newLevelId) => LevelSwitchRequests.Add(newLevelId);

    public bool HasContinueData() => false;

    public bool RequestContinueGame() => false;

    public void RequestLoadInitialSave()
    {
    }

    public void RequestLoadMainMenuEntrySave()
    {
    }

    public void RegisterSaveMetaContributor(ISaveMetaContributor contributor)
    {
    }

    public void RegisterSaveMetaContributor(Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>> contribute)
    {
    }

    public void RegisterTemplate(string key, SndMetaData template) => _templates[key] = template;

    ISndSceneAccess IStateMachineContext.SceneAccess => throw new InvalidOperationException(
        "SceneAccess unavailable in strategy unit tests. Use full SndContext integration tests for state machine scenarios.");

    IBlackboard? IStateMachineContext.SessionBlackboard => null;
}

internal sealed class StrategyFileAccess(
    IDataSourceIoGateway dataSourceIo,
    IFileMetaAccess metaAccess,
    DataSourceConverterRegistry converterRegistry) : ISndFileAccess
{
    private readonly IDataSourceIoGateway _dataSourceIo = dataSourceIo;
    private readonly IFileMetaAccess _metaAccess = metaAccess;
    private readonly DataSourceConverterRegistry _converterRegistry = converterRegistry;

    public DataSourceNode ReadFile(string path) => _dataSourceIo.ReadTree(path);

    public void WriteFile(string path, DataSourceNode node, bool overwrite)
        => _dataSourceIo.WriteTree(path, node, overwrite);

    public bool FileExists(string path) => _metaAccess.FileExists(path);

    public T ReadObject<T>(string path)
    {
        var node = _dataSourceIo.ReadTree(path);
        return _converterRegistry.Read<T>(node);
    }

    public void WriteObject<T>(string path, T value, bool overwrite)
    {
        var node = _converterRegistry.Write(value);
        _dataSourceIo.WriteTree(path, node, overwrite);
    }
}

internal sealed class StrategyArchiveFileAccess(
    IDataSourceIoGateway dataSourceIo,
    IFileMetaAccess metaAccess,
    DataSourceConverterRegistry converterRegistry) : ISndArchiveFileAccess
{
    private readonly IDataSourceIoGateway _dataSourceIo = dataSourceIo;
    private readonly IFileMetaAccess _metaAccess = metaAccess;
    private readonly DataSourceConverterRegistry _converterRegistry = converterRegistry;

    public DataSourceNode ReadFile(string relativePath)
        => _dataSourceIo.ReadTree(ResolveExtraTestPath(relativePath));

    public void WriteFile(string relativePath, DataSourceNode node, bool overwrite)
        => _dataSourceIo.WriteTree(ResolveExtraTestPath(relativePath), node, overwrite);

    public bool FileExists(string relativePath)
        => _metaAccess.FileExists(ResolveExtraTestPath(relativePath));

    public T ReadObject<T>(string relativePath)
    {
        var node = _dataSourceIo.ReadTree(ResolveExtraTestPath(relativePath));
        return _converterRegistry.Read<T>(node);
    }

    public void WriteObject<T>(string relativePath, T value, bool overwrite)
    {
        var node = _converterRegistry.Write(value);
        _dataSourceIo.WriteTree(ResolveExtraTestPath(relativePath), node, overwrite);
    }

    public void DeleteFile(string relativePath)
    {
        var resolved = ResolveExtraTestPath(relativePath);
        if (!_metaAccess.FileExists(resolved))
            throw new InvalidOperationException($"File not found in archive: '{relativePath}'.");
        _metaAccess.Delete(resolved);
    }

    private static string ResolveExtraTestPath(string relativePath)
    {
        if (relativePath.Contains(".."))
            throw new ArgumentException("Path traversal '..' is not allowed.", nameof(relativePath));
        return $"extra/{relativePath}";
    }
}

internal sealed class MinimalTestEntity : ISndEntity
{
    public ISessionRun OwningSession { get; set; } = null!;
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    internal Func<string, object?, object?>? InvokeStrategyHandler { get; set; }

    public string Name { get; set; } = string.Empty;

    public void SetData<T>(string name, T value) => _data[name] = value;

    public T GetData<T>(string name) where T : notnull
    {
        if (_data.TryGetValue(name, out var value) && value is T cast)
            return cast;
        throw new InvalidOperationException($"Data key '{name}' not found or type mismatch.");
    }

    public (bool found, T? value) TryGetData<T>(string name)
    {
        if (_data.TryGetValue(name, out var value) && value is T cast)
            return (true, cast);
        return (false, default);
    }

    public bool TryGetData<T>(string name, out T? value)
    {
        var (found, stored) = TryGetData<T>(name);
        value = stored;
        return found;
    }

    public bool IsPendingKill { get; set; }

    public void MountObserverStrategy(string targetName, string observerIndex) { }

    public void UnmountObserverStrategy(string targetName, string observerIndex) { }
    public void MountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }
    public void UnmountObserverStrategy(Origo.Core.Abstractions.Entity.ISndEntity target, string observerIndex) { }

    public INodeHandle GetNode(string name) =>
        throw new NotSupportedException("GetNode is not supported in strategy unit tests.");

    public IReadOnlyCollection<string> GetNodeNames() => [];

    public void AddStrategy(string index)
    {
    }

    public void RemoveStrategy(string index)
    {
    }

    public void AddActiveStrategy(string index)
    {
    }

    public void RemoveActiveStrategy(string index)
    {
    }

    public object? InvokeStrategy(string strategyIndex, object? input = null)
    {
        if (InvokeStrategyHandler is not null)
            return InvokeStrategyHandler(strategyIndex, input);
        throw new InvalidOperationException(
            "InvokeStrategy is not configured. Use StrategyTestScenario.ForActive<T>(...) to test ActiveStrategy subclasses.");
    }
}

internal sealed class TestSessionManager : ISessionManager
{
    private readonly Dictionary<string, ISessionRun> _sessions = new(StringComparer.Ordinal);

    public TestSessionManager(ISessionRun foregroundSession)
    {
        ArgumentNullException.ThrowIfNull(foregroundSession);
        _sessions[ISessionManager.ForegroundKey] = foregroundSession;
        ((TestSessionRun)foregroundSession)._sessionManager = this;
    }

    public bool CanCreateSessions => true;

    public ISessionRun? ForegroundSession => TryGet(ISessionManager.ForegroundKey);

    public IReadOnlyCollection<string> Keys => _sessions.Keys;

    public ISessionRun? TryGet(string key) =>
        _sessions.TryGetValue(key, out var session) ? session : null;

    public bool Contains(string key) => _sessions.ContainsKey(key);

    public ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false) =>
        throw new NotSupportedException(
            "CreateBackgroundSession is not supported in strategy unit tests. " +
            "Use full SndContext integration tests for multi-session scenarios.");

    public void DestroySession(string key) => _sessions.Remove(key);

    public void ProcessAllSessions(double delta, bool includeForeground = false)
    {
    }

    public void KillPendingAllSessions()
    {
    }
}

internal sealed class TestSessionRun : ISessionRun, IDisposable
{
    internal ISessionManager? _sessionManager;

    public IBlackboard SessionBlackboard { get; } = new Blackboard.Blackboard();

    public ISndSceneHost SceneHost { get; } = new TestSceneHost();

    public string LevelId => "test_level";

    public bool IsFrontSession => true;

    public ISessionManager SessionManager => _sessionManager ?? throw new InvalidOperationException(
        "SessionManager is not initialized. Ensure TestSessionManager sets the back-reference.");

    public ISndEntity? FindByName(string name) => SceneHost.FindByName(name);
    public IReadOnlyCollection<ISndEntity> GetEntities() => SceneHost.GetEntities();

    public ISndEntity Spawn(SndMetaData meta) =>
        throw new NotSupportedException("Spawn not supported in strategy unit tests.");

    public void SpawnMany(params SndMetaData[] metaList) =>
        throw new NotSupportedException("SpawnMany not supported in strategy unit tests.");

    public void RequestKillEntity(string entityName) =>
        SceneHost.RequestKillEntity(entityName);

    public StateMachineContainer GetSessionStateMachines() =>
        throw new NotSupportedException(
            "GetSessionStateMachines is not supported in strategy unit tests. " +
            "Use StateMachineStrategyTestScenario for state machine strategy testing.");

    IStateMachineContainer ISessionRun.GetSessionStateMachines() => GetSessionStateMachines();

    public void Dispose()
    {
    }
}

internal sealed class TestSceneHost : ISndSceneHost, IOwningSessionBindable
{
    private readonly List<ISndEntity> _entities = [];
    private ISessionRun? _owningSession;

    public void SetOwningSession(ISessionRun session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _owningSession = session;
    }

    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        var entity = new MinimalTestEntity { Name = metaData.Name, OwningSession = _owningSession! };
        _entities.Add(entity);
        return entity;
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

    public ISndEntity? FindByName(string name) =>
        _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    public void ProcessAll(double delta)
    {
    }

    public IReadOnlyList<SndMetaData> BuildMetaList() => [];

    public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
    {
    }

    public void RemoveAllEntities() => _entities.Clear();

    public void RemoveEntity(string name)
    {
        var entity = _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        if (entity is not null)
            _entities.Remove(entity);
    }

    public void RequestKillEntity(string name)
    {
        var entity = _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        if (entity is not MinimalTestEntity testEntity)
            throw new InvalidOperationException($"No entity with name '{name}'.");
        if (testEntity.IsPendingKill)
            throw new InvalidOperationException($"Entity '{name}' is already pending kill.");
        testEntity.IsPendingKill = true;
    }
}
