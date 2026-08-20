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
        ctx.Save.RegisterSaveMetaContributor(new KeyValueContributor("key_a", "val_a"));

        ctx.Save.RequestSaveGame("slot_01");
        ctx.FlushFrame();

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
        ctx.Save.RegisterSaveMetaContributor(_ => new Dictionary<string, string> { ["dkey"] = "dval" });

        ctx.Save.RequestSaveGame("slot_02");
        ctx.FlushFrame();

        var payload = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_02", MainMenuLevelId);
        Assert.NotNull(payload.CustomMeta);
        Assert.Equal("dval", payload.CustomMeta!["dkey"]);
    }

    [Fact]
    public void RegisterSaveMetaContributor_ThrowsOnNullContributor()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        Assert.Throws<ArgumentNullException>(() => ctx.Save.RegisterSaveMetaContributor((ISaveMetaContributor)null!));
    }

    [Fact]
    public void RegisterSaveMetaContributor_ThrowsOnNullDelegate()
    {
        var ctx = SndContextTestHelper.Create(out var fs);
        SndContextTestHelper.SetupProgressRun(ctx, fs);
        Assert.Throws<ArgumentNullException>(
            () => ctx.Save.RegisterSaveMetaContributor((Func<SaveMetaBuildContext, IReadOnlyDictionary<string, string>>)null!));
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
        ctx.Save.RegisterSaveMetaContributor(new KeyValueContributor("same", "first"));
        ctx.Save.RegisterSaveMetaContributor(new KeyValueContributor("same", "second"));

        ctx.Save.RequestSaveGame("slot_03");
        ctx.FlushFrame();

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
        ctx.Save.RegisterSaveMetaContributor(new KeyValueContributor("a", "1"));
        ctx.Save.RegisterSaveMetaContributor(new KeyValueContributor("b", "2"));
        ctx.Save.RegisterSaveMetaContributor(new KeyValueContributor("c", "3"));

        ctx.Save.RequestSaveGame("slot_04");
        ctx.FlushFrame();

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

        ctx.Save.RequestSaveGame("slot_05");
        ctx.FlushFrame();

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
        var hasProgress = false;
        var hasSession = false;

        ctx.Save.RegisterSaveMetaContributor(context =>
        {
            receivedSaveId = context.SaveId;
            receivedLevelId = context.CurrentLevelId;
            hasProgress = context.Progress is not null;
            hasSession = context.Session is not null;
            return new Dictionary<string, string>();
        });

        ctx.Save.RequestSaveGame("slot_ctx");
        ctx.FlushFrame();

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
        ctx.Save.RegisterSaveMetaContributor(new KeyValueContributor("ts", "1"));

        ctx.Save.RequestSaveGame("slot_a");
        ctx.FlushFrame();

        var payload1 = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_a", MainMenuLevelId);
        Assert.Equal("1", payload1.CustomMeta!["ts"]);

        ctx.Save.RegisterSaveMetaContributor(new KeyValueContributor("ts", "2"));
        ctx.Save.RequestSaveGame("slot_b");
        ctx.FlushFrame();

        var payload2 = SaveStorageFacade.ReadSavePayloadFromCurrent(handle, "slot_b", MainMenuLevelId);
        Assert.Equal("2", payload2.CustomMeta!["ts"]);
    }

    private sealed class KeyValueContributor(string key, string value) : ISaveMetaContributor
    {
        public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context) => new Dictionary<string, string> { [key] = value };
    }
}

public class SaveMetaNullAndSessionContextTests
{
    [Fact]
    public void NullSndContext_RegisterSaveMetaContributor_Throws()
    {
        var ctx = NullSndContext.Instance;
        Assert.Throws<InvalidOperationException>(
            () => ctx.Save.RegisterSaveMetaContributor(new StubContributor()));
        Assert.Throws<InvalidOperationException>(
            () => ctx.Save.RegisterSaveMetaContributor(_ => new Dictionary<string, string>()));
    }

    private sealed class StubContributor : ISaveMetaContributor
    {
        public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context) => new Dictionary<string, string>();
    }

}

internal static class SndContextTestHelper
{
    public static SndContext Create(out TestMemoryFileSystem fs)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = TestFactory.CreateRuntime(logger, host);
        fs = new TestMemoryFileSystem();
        var dataSourceIo = DataSourceFactory.CreateDefaultIoGateway(fs);
        var metaAccess = DataSourceFactory.CreateFileMetaAccess(fs);
        var pathResolver = DataSourceFactory.CreatePathResolver(fs);
        return new SndContext(new SndContextParameters(runtime, dataSourceIo, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
    }

    public static void SetupProgressRun(SndContext ctx, TestMemoryFileSystem fs)
    {
        fs.SeedFile("entry.json", "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"res://levels/main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }");
        fs.SeedFile("res://levels/main_menu.json", "[]"); ;
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();
    }
}
