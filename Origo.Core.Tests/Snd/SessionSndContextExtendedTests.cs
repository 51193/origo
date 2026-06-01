using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. SndContext save / load / continue workflows
// ─────────────────────────────────────────────────────────────────────────────

public class SessionSndContextExtendedTests
{
    [Fact]
    public void ListSaves_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        _ = ctx.ListSaves();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestLoadGame_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestLoadGame("id");
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestSaveGame_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestSaveGame("id");
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestSaveGameAuto_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        var result = ctx.RequestSaveGameAuto("auto_id");
        Assert.Equal("auto_id", result);
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void SetContinueTarget_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.SetContinueTarget("id");
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestSwitchForegroundLevel_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestSwitchForegroundLevel("level");
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestKillAll_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestKillAll();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void HasContinueData_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        _ = ctx.HasContinueData();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestContinueGame_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        _ = ctx.RequestContinueGame();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void RequestLoadInitialSave_DelegatesToGlobal()
    {
        var (ctx, global) = Create();
        ctx.RequestLoadInitialSave();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void Constructor_ThrowsOnNullGlobal()
    {
        var session = new StubSessionRun("lv");
        Assert.Throws<ArgumentNullException>(() => new SessionSndContext(null!, session));
    }

    [Fact]
    public void Constructor_ThrowsOnNullSession()
    {
        var global = new TrackingFakeSndContext();
        Assert.Throws<ArgumentNullException>(() => new SessionSndContext(global, null!));
    }

    private static (SessionSndContext ctx, TrackingFakeSndContext global) Create()
    {
        var global = new TrackingFakeSndContext();
        var session = new StubSessionRun("bg_level");
        return (new SessionSndContext(global, session), global);
    }

    private sealed class TrackingFakeSndContext : ISndContext
    {
        public int CallCount { get; private set; }
        public IBlackboard SystemBlackboard { get; } = new Blackboard.Blackboard();
        public IBlackboard? ProgressBlackboard { get; } = new Blackboard.Blackboard();
        public ISessionManager SessionManager { get; } = EmptySessionManager.Instance;
        public ISessionRun? CurrentSession => null;
        public bool IsFrontSession => false;

        public void EnqueueBusinessDeferred(Action action)
        {
            CallCount++;
            action();
        }

        public void FlushDeferredActionsForCurrentFrame() => CallCount++;

        public int GetPendingPersistenceRequestCount()
        {
            CallCount++;
            return 0;
        }

        public SndMetaData CloneTemplate(string templateKey, string? overrideName = null)
        {
            CallCount++;
            return new SndMetaData
            {
                Name = overrideName ?? templateKey, NodeMetaData = new NodeMetaData(),
                StrategyMetaData = new StrategyMetaData(), DataMetaData = new DataMetaData()
            };
        }

        public bool TrySubmitConsoleCommand(string commandLine)
        {
            CallCount++;
            return true;
        }

        public void ProcessConsolePending() => CallCount++;

        public long SubscribeConsoleOutput(Action<string> onLine)
        {
            CallCount++;
            return 1;
        }

        public void UnsubscribeConsoleOutput(long subscriptionId) => CallCount++;

        public StateMachineContainer? GetProgressStateMachines()
        {
            CallCount++;
            return null;
        }

        public IReadOnlyList<string> ListSaves()
        {
            CallCount++;
            return Array.Empty<string>();
        }

        public void RequestLoadGame(string saveId) => CallCount++;

        public void RequestSaveGame(string newSaveId) => CallCount++;

        public string RequestSaveGameAuto(string? newSaveId = null)
        {
            CallCount++;
            return newSaveId ?? "auto";
        }

        public void SetContinueTarget(string saveId) => CallCount++;

        public void RequestSwitchForegroundLevel(string newLevelId) => CallCount++;

        public void RequestKillAll()
        {
            CallCount++;
        }

        public void RequestKillEntity(string entityName)
        {
            CallCount++;
        }

        public bool HasContinueData()
        {
            CallCount++;
            return false;
        }

        public bool RequestContinueGame()
        {
            CallCount++;
            return false;
        }

        public void RequestLoadInitialSave() => CallCount++;

        public void RequestLoadMainMenuEntrySave() => CallCount++;
    }

    private sealed class StubSessionRun(string levelId) : ISessionRun
    {
        public IBlackboard SessionBlackboard { get; } = new Blackboard.Blackboard();
        public ISndSceneHost SceneHost => throw new NotImplementedException();
        public string LevelId { get; } = levelId;
        public bool IsFrontSession => false;

        public StateMachineContainer GetSessionStateMachines() => throw new NotImplementedException();

        public void Dispose()
        {
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. MemorySndSceneHost + MemorySndEntity
// ─────────────────────────────────────────────────────────────────────────────
