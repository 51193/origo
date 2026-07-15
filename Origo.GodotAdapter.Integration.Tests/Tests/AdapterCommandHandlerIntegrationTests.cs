using System.Collections.Generic;
using Godot;
using Origo.Core;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Console;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.Blackboard;
using Origo.Core.DataSource;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Serialization;
using Origo.Core.Snd.Metadata;
using Origo.GodotAdapter.Console;
using Origo.GodotAdapter.Integration.Tests.Runner;
using Origo.GodotAdapter.Integration.Tests.TestSupport;
using Origo.GodotAdapter.Snd;

namespace Origo.GodotAdapter.Integration.Tests;

public class AdapterCommandHandlerIntegrationTests : IDeferredTestFixture
{
    private Node _root = null!;
    private int _frame;

    public bool IsComplete => _frame >= 1;
    public void Setup() => _frame = 0;
    public void AdvanceFrame() => _frame++;

    private static OrigoRuntime CreateRuntimeWithMockSession(ISndEntity? entityToFind)
    {
        var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        var sessionManager = new StubSessionManager(entityToFind);
        harness.Runtime.SetSessionManagerProvider(() => sessionManager);
        return harness.Runtime;
    }

    private static void SetupEntity(GodotSndEntity entity, string name)
    {
        entity.Name = name;
        ((IEntityLifecycle)entity).RecoverForLifecycle(new SndMetaData
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData()
        });
    }

    [DeferredTest(Description = "TreeDebug handler prints node tree for valid entity")]
    public void TreeDebug_ValidEntity_PrintsTree()
    {
        _root = ((SceneTree)Engine.GetMainLoop()).Root;

        var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        var entity = new GodotSndEntity(
            harness.SndWorld, harness.SndManager.Context!, harness.Logger,
            harness.SndManager.GetObserverTopology(), _ => new StubNodeFactory());
        SetupEntity(entity, "test_entity");
        _root.AddChild(entity);

        var child1 = new Node { Name = "ChildA" };
        entity.AddChild(child1);

        var runtime = CreateRuntimeWithMockSession(entity);
        var handler = new TreeDebugCommandHandler(runtime);
        var output = new StubConsoleOutput();

        var invocation = new CommandInvocation
        {
            Command = "tree_debug",
            PositionalArgs = ["test_entity"],
            NamedArgs = new Dictionary<string, string>()
        };

        var success = handler.TryExecute(invocation, output, out var error);
        IntegrationTestRunner.Assert(success, "TryExecute should succeed.");
        IntegrationTestRunner.AssertNotEmpty(output.PublishedLines, "output.PublishedLines");
        IntegrationTestRunner.AssertContains("ChildA", string.Join("\n", output.PublishedLines), "output");
    }

    [DeferredTest(Description = "TreeDebug handler errors on unknown entity")]
    public void TreeDebug_UnknownEntity_ReturnsError()
    {
        var runtime = CreateRuntimeWithMockSession(null);
        var handler = new TreeDebugCommandHandler(runtime);
        var output = new StubConsoleOutput();

        var invocation = new CommandInvocation
        {
            Command = "tree_debug",
            PositionalArgs = ["nonexistent"],
            NamedArgs = new Dictionary<string, string>()
        };
        var success = handler.TryExecute(invocation, output, out _);
        IntegrationTestRunner.Assert(!success, "TryExecute should fail for unknown entity.");
    }

    [DeferredTest(Description = "PressButton handler emits button Pressed signal")]
    public void PressButton_EmitsPressedSignal()
    {
        _root = ((SceneTree)Engine.GetMainLoop()).Root;

        var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        var entity = new GodotSndEntity(
            harness.SndWorld, harness.SndManager.Context!, harness.Logger,
            harness.SndManager.GetObserverTopology(), _ => new StubNodeFactory());
        SetupEntity(entity, "button_entity");
        _root.AddChild(entity);

        var button = new Button { Name = "TestBtn" };
        entity.AddChild(button);

        var pressed = false;
        button.Pressed += () => pressed = true;

        var runtime = CreateRuntimeWithMockSession(entity);
        var handler = new PressButtonCommandHandler(runtime);
        var output = new StubConsoleOutput();

        var invocation = new CommandInvocation
        {
            Command = "press_button",
            PositionalArgs = ["button_entity", "TestBtn"],
            NamedArgs = new Dictionary<string, string>()
        };

        var success = handler.TryExecute(invocation, output, out var error);
        IntegrationTestRunner.Assert(success, "TryExecute should succeed.");
        IntegrationTestRunner.Assert(pressed, "Button Pressed signal should fire.");
    }

    [DeferredTest(Description = "PressButton handler errors on unknown button path")]
    public void PressButton_UnknownButtonPath_ReturnsError()
    {
        _root = ((SceneTree)Engine.GetMainLoop()).Root;

        var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        var entity = new GodotSndEntity(
            harness.SndWorld, harness.SndManager.Context!, harness.Logger,
            harness.SndManager.GetObserverTopology(), _ => new StubNodeFactory());
        SetupEntity(entity, "no_btn_entity");
        _root.AddChild(entity);

        var runtime = CreateRuntimeWithMockSession(entity);
        var handler = new PressButtonCommandHandler(runtime);
        var output = new StubConsoleOutput();

        var invocation = new CommandInvocation
        {
            Command = "press_button",
            PositionalArgs = ["no_btn_entity", "NoSuchButton"],
            NamedArgs = new Dictionary<string, string>()
        };

        var success = handler.TryExecute(invocation, output, out var error);
        IntegrationTestRunner.Assert(!success, "TryExecute should fail for unknown button path.");
    }

    [DeferredTest(Description = "CameraView handler produces output in headless mode")]
    public void CameraView_ProducesOutputInHeadless()
    {
        _root = ((SceneTree)Engine.GetMainLoop()).Root;

        var harness = new IntegrationTestHarness();
        harness.BindRuntimeDependencies();
        harness.BindContext();
        var entity = new GodotSndEntity(
            harness.SndWorld, harness.SndManager.Context!, harness.Logger,
            harness.SndManager.GetObserverTopology(), _ => new StubNodeFactory());
        SetupEntity(entity, "cam_entity");
        _root.AddChild(entity);

        var node3d = new Node3D { Name = "VisibleThing", Position = new Vector3(0, 1, -10) };
        entity.AddChild(node3d);

        var camera = new Camera3D { Name = "TestCamera", Current = true, Position = new Vector3(0, 0, 0) };
        _root.AddChild(camera);

        var runtime = CreateRuntimeWithMockSession(entity);
        var handler = new CameraViewCommandHandler(runtime);
        var output = new StubConsoleOutput();

        var invocation = new CommandInvocation
        {
            Command = "camera_view",
            PositionalArgs = [],
            NamedArgs = new Dictionary<string, string>()
        };

        var success = handler.TryExecute(invocation, output, out var error);
        IntegrationTestRunner.Assert(success, "TryExecute should succeed in headless.");
        IntegrationTestRunner.AssertNotEmpty(output.PublishedLines, "output.PublishedLines");
    }

    private sealed class StubSessionManager(ISndEntity? entity) : ISessionManager
    {
        public ISessionRun? ForegroundSession => entity != null ? new StubForegroundSession(entity) : null;
        public ISessionRun? TryGet(string key) => null;
        public bool Contains(string key) => false;
        public ISessionRun CreateBackgroundSession(string key, string levelId, bool syncProcess = false)
            => new StubForegroundSession(entity!);
        public void DestroySession(string key) { }
        public void ProcessAllSessions(double delta, bool includeForeground = false) { }
        public void KillPendingAllSessions() { }
        public IReadOnlyCollection<string> Keys => [];
        public bool CanCreateSessions => true;
    }

    private sealed class StubForegroundSession(ISndEntity entity) : ISessionRun
    {
        public IBlackboard SessionBlackboard => new Blackboard();
        public string LevelId => "test_level";
        public bool IsFrontSession => true;
        public IStateMachineContainer GetSessionStateMachines() => null!;
        public ISessionManager SessionManager => new StubSessionManager(entity);
        public ISndEntity? FindByName(string name) => entity;
        public IReadOnlyCollection<ISndEntity> GetEntities() => [entity];
        public ISndEntity Spawn(SndMetaData meta) => entity;
        public void SpawnMany(params SndMetaData[] metaList) { }
        public void RequestKillEntity(string entityName) { }
        public void Dispose() { }
    }
}
