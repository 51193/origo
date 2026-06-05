using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save.Meta;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.StateMachine;

namespace Origo.Core.Tests;

public class ContextBoundaryTests
{
    [Fact]
    public void SessionSndContext_DelegatesToGlobalAndPinsCurrentSession()
    {
        var global = new FakeSndContext();
        var session = new FakeSessionRun("bg_demo");
        var ctx = new SessionSndContext(global, session);

        Assert.Same(session, ctx.CurrentSession);
        Assert.Same(global.SystemBlackboard, ctx.SystemBlackboard);
        Assert.Same(global.ProgressBlackboard, ctx.ProgressBlackboard);
        Assert.Same(global.SessionManager, ctx.SessionManager);

        ctx.EnqueueBusinessDeferred(() => { });
        ctx.FlushDeferredActionsForCurrentFrame();
        _ = ctx.GetPendingPersistenceRequestCount();
        _ = ctx.CloneTemplate("template", "name");
        _ = ctx.TrySubmitConsoleCommand("help");
        ctx.ProcessConsolePending();
        var subId = ctx.SubscribeConsoleOutput(_ => { });
        ctx.UnsubscribeConsoleOutput(subId);
        _ = ctx.GetProgressStateMachines();
        ctx.RequestLoadMainMenuEntrySave();
        Assert.True(global.CallCount > 0);
    }

    [Fact]
    public void NullSndContext_AllNoopMembers_AreSafe()
    {
        var ctx = NullSndContext.Instance;

        Assert.NotNull(ctx.SystemBlackboard);
        Assert.Null(ctx.ProgressBlackboard);
        Assert.Null(ctx.CurrentSession);
        Assert.False(ctx.TrySubmitConsoleCommand("x"));
        Assert.Equal(0, ctx.GetPendingPersistenceRequestCount());
        Assert.Null(ctx.GetProgressStateMachines());
        Assert.NotNull(ctx.SessionManager);
        Assert.Equal(0, ctx.SubscribeConsoleOutput(_ => { }));
        ctx.UnsubscribeConsoleOutput(1);
        ctx.EnqueueBusinessDeferred(() => { });
        ctx.FlushDeferredActionsForCurrentFrame();
        ctx.ProcessConsolePending();
        Assert.Throws<InvalidOperationException>(() => ctx.RequestLoadMainMenuEntrySave());
        Assert.Throws<InvalidOperationException>(() => ctx.CloneTemplate("t"));
    }

    private sealed class FakeSndContext : ISndContext
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

        public void RequestKillAll() => CallCount++;

        public void RequestKillEntity(string entityName) => CallCount++;

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

        public void RegisterSaveMetaContributor(ISaveMetaContributor contributor) => CallCount++;

        public void RegisterSaveMetaContributor(Action<SaveMetaBuildContext, IDictionary<string, string>> contribute) =>
            CallCount++;
    }

    private sealed class FakeSessionRun(string levelId) : ISessionRun
    {
        public IBlackboard SessionBlackboard { get; } = new Blackboard.Blackboard();
        public ISndSceneHost SceneHost => throw new NotImplementedException();
        public string LevelId { get; } = levelId;
        public bool IsFrontSession => false;

        public IStateMachineContainer GetSessionStateMachines() => throw new NotImplementedException();

        public void Dispose()
        {
        }
    }
}
