using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Blackboard;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Scene;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
using Origo.Core.DataSource;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Runtime.StateMachine;
using Origo.Core.Save;
using Origo.Core.Save.Meta;
using Origo.Core.Save.Storage;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Xunit;

using static Origo.Core.Snd.SndDefaults;

namespace Origo.Core.Tests;

public class SaveMetaContributorRegistrationTests
{
    [Fact]
    public void RegisterSaveMetaContributor_WithISaveMetaContributor_ContributesToSavePayload()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        ctx.RegisterSaveMetaContributor(new KeyValueContributor("key_a", "val_a"));

        ctx.RequestSaveGame("slot_01");
        ctx.FlushDeferredActionsForCurrentFrame();

        var payload = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_01", MainMenuLevelId);
        Assert.NotNull(payload.CustomMeta);
        Assert.Equal("val_a", payload.CustomMeta!["key_a"]);
    }

    [Fact]
    public void RegisterSaveMetaContributor_WithDelegate_ContributesToSavePayload()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        ctx.RegisterSaveMetaContributor(_ => new Dictionary<string, string> { ["dkey"] = "dval" });

        ctx.RequestSaveGame("slot_02");
        ctx.FlushDeferredActionsForCurrentFrame();

        var payload = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_02", MainMenuLevelId);
        Assert.NotNull(payload.CustomMeta);
        Assert.Equal("dval", payload.CustomMeta!["dkey"]);
    }

    [Fact]
    public void RegisterSaveMetaContributor_ThrowsOnNullContributor()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        Assert.Throws<ArgumentNullException>(() => ctx.RegisterSaveMetaContributor((ISaveMetaContributor)null!));
    }

    [Fact]
    public void RegisterSaveMetaContributor_ThrowsOnNullDelegate()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        Assert.Throws<ArgumentNullException>(
            () => ctx.RegisterSaveMetaContributor((Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>>)null!));
    }

    [Fact]
    public void MultipleContributors_LaterOverwritesEarlier()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        ctx.RegisterSaveMetaContributor(new KeyValueContributor("same", "first"));
        ctx.RegisterSaveMetaContributor(new KeyValueContributor("same", "second"));

        ctx.RequestSaveGame("slot_03");
        ctx.FlushDeferredActionsForCurrentFrame();

        var payload = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_03", MainMenuLevelId);
        Assert.NotNull(payload.CustomMeta);
        Assert.Equal("second", payload.CustomMeta!["same"]);
    }

    [Fact]
    public void MultipleContributors_EachAddsDifferentKey()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        ctx.RegisterSaveMetaContributor(new KeyValueContributor("a", "1"));
        ctx.RegisterSaveMetaContributor(new KeyValueContributor("b", "2"));
        ctx.RegisterSaveMetaContributor(new KeyValueContributor("c", "3"));

        ctx.RequestSaveGame("slot_04");
        ctx.FlushDeferredActionsForCurrentFrame();

        var payload = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_04", MainMenuLevelId);
        Assert.NotNull(payload.CustomMeta);
        Assert.Equal(3, payload.CustomMeta!.Count);
        Assert.Equal("1", payload.CustomMeta["a"]);
        Assert.Equal("2", payload.CustomMeta["b"]);
        Assert.Equal("3", payload.CustomMeta["c"]);
    }

    [Fact]
    public void SaveWithoutContributors_CustomMetaIsNull()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");

        ctx.RequestSaveGame("slot_05");
        ctx.FlushDeferredActionsForCurrentFrame();

        var payload = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_05", MainMenuLevelId);
        Assert.Null(payload.CustomMeta);
    }

    [Fact]
    public void ContributorReceivesCorrectSaveMetaBuildContext()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        string? receivedSaveId = null;
        string? receivedLevelId = null;
        bool hasProgress = false;
        bool hasSession = false;

        ctx.RegisterSaveMetaContributor(context =>
        {
            receivedSaveId = context.SaveId;
            receivedLevelId = context.CurrentLevelId;
            hasProgress = context.Progress is not null;
            hasSession = context.Session is not null;
            return new Dictionary<string, string>();
        });

        ctx.RequestSaveGame("slot_ctx");
        ctx.FlushDeferredActionsForCurrentFrame();

        Assert.Equal("slot_ctx", receivedSaveId);
        Assert.Equal(MainMenuLevelId, receivedLevelId);
        Assert.True(hasProgress);
        Assert.True(hasSession);
    }

    [Fact]
    public void SaveMultipleTimes_EachSaveHasCorrectMeta()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        var handle = new SaveFileHandle(metaAccess, dataSourceIo, pathResolver, "root");
        ctx.RegisterSaveMetaContributor(new KeyValueContributor("ts", "1"));

        ctx.RequestSaveGame("slot_a");
        ctx.FlushDeferredActionsForCurrentFrame();

        var payload1 = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_a", MainMenuLevelId);
        Assert.Equal("1", payload1.CustomMeta!["ts"]);

        ctx.RegisterSaveMetaContributor(new KeyValueContributor("ts", "2"));
        ctx.RequestSaveGame("slot_b");
        ctx.FlushDeferredActionsForCurrentFrame();

        var payload2 = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_b", MainMenuLevelId);
        Assert.Equal("2", payload2.CustomMeta!["ts"]);
    }

    private sealed class KeyValueContributor(string key, string value) : ISaveMetaContributor
    {
        public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context)
        {
            return new Dictionary<string, string> { [key] = value };
        }
    }
}

public class SaveMetaNullAndSessionContextTests
{
    [Fact]
    public void NullSndContext_RegisterSaveMetaContributor_Throws()
    {
        var ctx = NullSndContext.Instance;
        Assert.Throws<InvalidOperationException>(
            () => ctx.RegisterSaveMetaContributor(new StubContributor()));
        Assert.Throws<InvalidOperationException>(
            () => ctx.RegisterSaveMetaContributor(_ => new Dictionary<string, string>()));
    }

    private sealed class StubContributor : ISaveMetaContributor
    {
        public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context)
        {
            return new Dictionary<string, string>();
        }
    }

}

internal static class SndContextTestHelper
{
    public static SndContext Create(out TestFileSystem fs)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        fs = new TestFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        return new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
    }

    public static void SetupProgressRun(SndContext ctx, TestFileSystem fs)
    {
        fs.SeedFile("entry.json", "[]");
        ctx.RequestLoadMainMenuEntrySave();
        ctx.FlushDeferredActionsForCurrentFrame();
    }
}
