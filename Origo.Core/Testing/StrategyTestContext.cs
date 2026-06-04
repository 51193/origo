using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Node;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;

namespace Origo.Core.Testing;

internal sealed class StrategyTestContext : ISndContext
{
    private readonly List<string> _consoleCommands = new();
    private readonly List<string> _consoleOutput = new();
    private readonly Queue<Action> _deferred = new();
    private readonly Dictionary<string, SndMetaData> _templates = new(StringComparer.Ordinal);

    public StrategyTestContext()
    {
        var session = new TestSessionRun();
        SessionManager = new TestSessionManager(session);
    }

    public List<string> SaveRequests { get; } = new();

    public List<string> LoadRequests { get; } = new();

    public List<string> LevelSwitchRequests { get; } = new();

    public int DeferredActionCount { get; private set; }

    public IReadOnlyList<string> ConsoleCommands => _consoleCommands;

    public IReadOnlyList<string> ConsoleOutput => _consoleOutput;

    public IBlackboard SystemBlackboard { get; } = new Blackboard.Blackboard();

    public IBlackboard ProgressBlackboard { get; } = new Blackboard.Blackboard();

    public ISessionManager SessionManager { get; }

    public ISessionRun? CurrentSession => SessionManager.ForegroundSession;

    public bool IsFrontSession => true;

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

    public StateMachineContainer? GetProgressStateMachines() => null;

    public IReadOnlyList<string> ListSaves() => Array.Empty<string>();

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

    public void RequestKillAll()
    {
        foreach (var entity in CurrentSession?.SceneHost?.GetEntities() ?? Array.Empty<ISndEntity>())
        {
            if (entity.IsPendingKill)
                continue;
            RequestKillEntity(entity.Name);
        }
    }

    public void RequestKillEntity(string entityName)
    {
        if (CurrentSession?.SceneHost is ISndSceneHost host) host.RequestKillEntity(entityName);
    }

    public bool HasContinueData() => false;

    public bool RequestContinueGame() => false;

    public void RequestLoadInitialSave()
    {
    }

    public void RequestLoadMainMenuEntrySave()
    {
    }

    public void RegisterTemplate(string key, SndMetaData template) => _templates[key] = template;
}

internal sealed class MinimalTestEntity : ISndEntity
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    internal Func<string, object?, object?>? InvokeStrategyHandler { get; set; }

    public string Name { get; set; } = string.Empty;

    public void SetData<T>(string name, T value) => _data[name] = value;

    public T GetData<T>(string name)
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

    public void Subscribe(string name, Action<ISndEntity, object?, object?> callback,
        Func<ISndEntity, object?, object?, bool>? filter = null)
    {
    }

    public void Unsubscribe(string name, Action<ISndEntity, object?, object?> callback)
    {
    }

    public INodeHandle GetNode(string name) =>
        throw new NotSupportedException("GetNode is not supported in strategy unit tests.");

    public IReadOnlyCollection<string> GetNodeNames() => Array.Empty<string>();

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

    public bool IsPendingKill { get; set; }
}

internal sealed class TestSessionManager : ISessionManager
{
    private readonly Dictionary<string, ISessionRun> _sessions = new(StringComparer.Ordinal);

    public TestSessionManager(ISessionRun foregroundSession)
    {
        ArgumentNullException.ThrowIfNull(foregroundSession);
        _sessions[ISessionManager.ForegroundKey] = foregroundSession;
    }

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
}

internal sealed class TestSessionRun : ISessionRun
{
    public IBlackboard SessionBlackboard { get; } = new Blackboard.Blackboard();

    public ISndSceneHost SceneHost { get; } = new TestSceneHost();

    public string LevelId => "test_level";

    public bool IsFrontSession => true;

    public StateMachineContainer GetSessionStateMachines() =>
        throw new NotSupportedException(
            "GetSessionStateMachines is not supported in strategy unit tests. " +
            "Use StateMachineStrategyTestScenario for state machine strategy testing.");

    public void Dispose()
    {
    }
}

internal sealed class TestSceneHost : ISndSceneHost
{
    private readonly List<ISndEntity> _entities = new();

    public ISndEntity CreateEntity(SndMetaData metaData)
    {
        var entity = new MinimalTestEntity { Name = metaData.Name };
        _entities.Add(entity);
        return entity;
    }

    public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

    public ISndEntity? FindByName(string name) =>
        _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    public void ProcessAll(double delta)
    {
    }

    public IReadOnlyList<SndMetaData> BuildMetaList() => Array.Empty<SndMetaData>();

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
