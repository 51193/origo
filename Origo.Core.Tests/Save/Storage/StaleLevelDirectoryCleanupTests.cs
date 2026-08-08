using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Abstractions.Scene;
using Origo.Core.DataSource;
using Origo.Core.Snd;
using Origo.Core.Snd.Entity;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Scene;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression tests: saving must keep the on-disk level set consistent
///     with the live payload. Level directories whose sessions no longer
///     exist (e.g. a background session was destroyed) are stale: they must
///     be pruned from <c>current/</c> on the next full save so they are not
///     carried into every subsequent snapshot.
/// </summary>
public class StaleLevelDirectoryCleanupTests
{
    [Fact]
    public void SaveAfterDestroyingBackgroundSession_PrunesStaleLevelDirectory()
    {
        var (ctx, fs) = CreateContext();

        var fg = ctx.EnsureProgressRun().LoadAndMountForeground("level_a");
        fg.Spawn(CreateMeta("A"));

        var bg = ctx.Runtime.SessionManager.CreateBackgroundSession("bg", "level_b");
        bg.Spawn(CreateMeta("B"));

        ctx.Save.RequestSaveGame("001");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // Both live sessions' levels are persisted.
        Assert.True(fs.Exists("root/current/level_level_a/snd_scene.json"));
        Assert.True(fs.Exists("root/current/level_level_b/snd_scene.json"));

        ctx.Runtime.SessionManager.DestroySession("bg");

        ctx.Save.RequestSaveGame("002");
        ctx.Deferred.FlushDeferredActionsForCurrentFrame();

        // The destroyed session's level is no longer part of the payload and
        // must not survive in current/ or leak into the new snapshot.
        Assert.True(fs.Exists("root/current/level_level_a/snd_scene.json"));
        Assert.True(fs.Exists("root/save_002/level_level_a/snd_scene.json"));
        Assert.False(fs.Exists("root/current/level_level_b/snd_scene.json"));
        Assert.False(fs.Exists("root/save_002/level_level_b/snd_scene.json"));
    }

    private static (SndContext ctx, TestMemoryFileSystem fs) CreateContext()
    {
        var logger = new TestLogger();
        var host = new LiveViewSndSceneHost(logger);
        var fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, new TypeStringMapping(), new Blackboard.Blackboard(), dataSourceIo);
        var ctx = new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver,
            "root", "initial", "entry.json"));
        host.BindWorld(runtime.SndWorld);
        host.BindContext(ctx);

        var progressRun = TestFactory.CreateProgressRun(
            "run", logger, metaAccess, pathResolver, "root", runtime, ctx, sharedDataSourceIo: dataSourceIo);
        ctx.SetProgressRun(progressRun);
        return (ctx, fs);
    }

    private static SndMetaData CreateMeta(string name) =>
        new()
        {
            Name = name,
            NodeMetaData = new NodeMetaData(),
            StrategyMetaData = new StrategyMetaData(),
            DataMetaData = new DataMetaData()
        };

    private sealed class LiveViewSndSceneHost : ISndSceneHost, ISndContextAttachableSceneHost, IOwningSessionBindable, IObserverTopologyHost
    {
        private readonly ILogger _logger;
        private readonly List<ISndEntity> _entities = [];
        private readonly HashSet<string> _pendingKill = [];
        private SndWorld? _world;
        private ISndContext? _context;
        private ISessionRun? _session;
        private ObserverTopology? _topology;

        internal LiveViewSndSceneHost(ILogger logger) => _logger = logger;

        ObserverTopology IObserverTopologyHost.ObserverTopology =>
            _topology ?? throw new InvalidOperationException("Observer topology not bound; BindWorld must run first.");

        internal void BindWorld(SndWorld world)
        {
            _world = world;
            _topology = new ObserverTopology(world.StrategyPool, _logger);
        }

        public void BindContext(ISndContext context) => _context = context;

        public void SetOwningSession(ISessionRun session) => _session = session;

        public ISndEntity CreateEntity(SndMetaData metaData)
        {
            _topology!.BindContext(_context!);
            var entity = _world!.CreateEntity(new TestNodeFactory(), _context!, _logger, _topology);
            entity.Name = metaData.Name;
            _entities.Add(entity);
            ((IEntityLifecycle)entity).RecoverForLifecycle(metaData);
            if (_session is not null)
                entity.BindSession(_session);
            return entity;
        }

        public IReadOnlyCollection<ISndEntity> GetEntities() => _entities;

        public ISndEntity? FindByName(string name) =>
            _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

        public IReadOnlyList<SndMetaData> BuildMetaList() =>
            [.. _entities.Select(e => ((IEntityLifecycle)e).BuildMetaData())];

        public void RecoverFromMetaList(IEnumerable<SndMetaData> metaList)
        {
            foreach (var meta in metaList)
                CreateEntity(meta);
        }

        public void RemoveAllEntities()
        {
            _entities.Clear();
            _pendingKill.Clear();
        }

        public void RemoveEntity(string name)
        {
            var entity = FindByName(name)
                         ?? throw new InvalidOperationException($"No entity with name '{name}'.");
            _entities.Remove(entity);
            _pendingKill.Remove(name);
        }

        public void RequestKillEntity(string name)
        {
            if (FindByName(name) is null)
                throw new InvalidOperationException($"No entity with name '{name}'.");
            if (!_pendingKill.Add(name))
                throw new InvalidOperationException($"Entity '{name}' is already pending kill.");
        }

        public void ProcessAll(double delta)
        {
        }
    }
}
