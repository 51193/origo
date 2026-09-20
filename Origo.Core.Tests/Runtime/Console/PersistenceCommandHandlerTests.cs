using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Lifecycle;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Save;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class PersistenceCommandHandlerTests
{
    [Fact]
    public void ListSavesCommand_ListsSlotAndDisplayMetadata()
    {
        var ctx = CreateContext(out var fs, out var input, out _, out var messages);
        SeedSaveSnapshot(fs, "slot_a", "default", "name: Alice\n");

        input.Enqueue("list_saves");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("slot_a", StringComparison.Ordinal)
                                       && m.Contains("name=Alice", StringComparison.Ordinal));
    }

    [Fact]
    public void ListSavesCommand_NoSaves_ReportsEmpty()
    {
        var ctx = CreateContext(out _, out var input, out _, out var messages);

        input.Enqueue("list_saves");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains("No saves found.", messages);
    }

    [Fact]
    public void ListSavesCommand_SlotWithoutMetadata_ListsIdOnly()
    {
        var ctx = CreateContext(out var fs, out var input, out _, out var messages);
        SeedSaveSnapshot(fs, "plain_slot", "default");

        input.Enqueue("list_saves");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains("plain_slot", messages);
    }

    [Fact]
    public void SaveCommand_QueuesRequestAndPersistsOnFrame()
    {
        var ctx = CreateContext(out var fs, out var input, out _, out var messages);
        SetupProgressRun(ctx, fs);

        input.Enqueue("save cmd_slot");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("Save request queued", StringComparison.Ordinal));
        Assert.True(ctx.Deferred.GetPendingPersistenceRequestCount() > 0);

        ctx.FlushFrame();

        Assert.True(fs.Exists("root/save_cmd_slot/progress.json"));
    }

    [Fact]
    public void LoadCommand_QueuesRequestAndLoadsOnFrame()
    {
        var ctx = CreateContext(out var fs, out var input, out _, out var messages);
        SeedSaveSnapshot(fs, "slot_load", "default");

        input.Enqueue("load slot_load");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("Load request queued", StringComparison.Ordinal));

        ctx.FlushFrame();

        Assert.NotNull(ctx.Runtime.SessionManager.ForegroundSession);
    }

    [Fact]
    public void LoadCommand_MissingSlot_ReportsError()
    {
        var ctx = CreateContext(out _, out var input, out _, out var messages);

        input.Enqueue("load missing_slot");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("does not exist", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadCommand_MalformedStoredId_ReportsError()
    {
        var ctx = CreateContext(out var fs, out var input, out _, out var messages);
        fs.CreateDirectory("root/save_bad@name");

        input.Enqueue("load bad@name");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("not allowed", StringComparison.Ordinal));
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void SwitchLevelCommand_QueuesRequestAndSwitchesOnFrame()
    {
        var ctx = CreateContext(out var fs, out var input, out _, out var messages);
        SetupProgressRun(ctx, fs);
        fs.SeedFile("root/current/level_cmd_target/snd_scene.json", "[]");
        fs.SeedFile("root/current/level_cmd_target/session.json", "{}");
        fs.SeedFile("root/current/level_cmd_target/session_state_machines.json", """{"machines":[]}""");

        input.Enqueue("switch_level cmd_target");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("Level switch queued", StringComparison.Ordinal));
        Assert.True(ctx.Deferred.GetPendingPersistenceRequestCount() > 0);

        ctx.FlushFrame();

        Assert.Equal("cmd_target", ctx.Runtime.SessionManager.ForegroundSession?.LevelId);
    }

    [Fact]
    public void SaveCommand_InvalidSaveId_ReportsError()
    {
        var ctx = CreateContext(out _, out var input, out _, out var messages);

        input.Enqueue("save invalid/id");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("not allowed", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, m => m.Contains("queued", StringComparison.Ordinal));
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void DeleteSaveCommand_InvalidSaveId_ReportsError()
    {
        var ctx = CreateContext(out _, out var input, out _, out var messages);

        input.Enqueue("delete_save bad@name");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("not allowed", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, m => m.Contains("deleted", StringComparison.Ordinal));
    }

    [Fact]
    public void SwitchLevelCommand_InvalidLevelId_ReportsError()
    {
        var ctx = CreateContext(out _, out var input, out _, out var messages);

        input.Enqueue("switch_level bad@name");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("not allowed", StringComparison.Ordinal));
        Assert.Equal(0, ctx.Deferred.GetPendingPersistenceRequestCount());
    }

    [Fact]
    public void DeleteSaveCommand_RemovesInactiveSlot()
    {
        var ctx = CreateContext(out var fs, out var input, out _, out var messages);
        SeedSaveSnapshot(fs, "slot_delete", "default");
        Assert.True(fs.DirectoryExists("root/save_slot_delete"));

        input.Enqueue("delete_save slot_delete");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("deleted", StringComparison.Ordinal));
        Assert.False(fs.DirectoryExists("root/save_slot_delete"));
    }

    [Fact]
    public void DeleteSaveCommand_MissingSlot_ReportsError()
    {
        var ctx = CreateContext(out _, out var input, out _, out var messages);

        input.Enqueue("delete_save missing_slot");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m => m.Contains("does not exist", StringComparison.Ordinal));
    }

    [Fact]
    public void HelpCommand_ListsPersistenceCommands()
    {
        var ctx = CreateContext(out _, out var input, out _, out var messages);

        input.Enqueue("help");
        ctx.Runtime.Console!.ProcessPending();

        Assert.Contains(messages, m =>
            m.TrimStart().StartsWith("list_saves ", StringComparison.Ordinal));
        Assert.Contains(messages, m =>
            m.TrimStart().StartsWith("save ", StringComparison.Ordinal));
        Assert.Contains(messages, m =>
            m.TrimStart().StartsWith("load ", StringComparison.Ordinal));
        Assert.Contains(messages, m =>
            m.TrimStart().StartsWith("delete_save ", StringComparison.Ordinal));
        Assert.Contains(messages, m =>
            m.TrimStart().StartsWith("switch_level ", StringComparison.Ordinal));
    }

    private static SndContext CreateContext(
        out TestMemoryFileSystem fs,
        out ConsoleInputBuffer input,
        out ConsoleOutputChannel output,
        out List<string> messages)
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var tm = new TypeStringMapping();
        var bb = new Blackboard.Blackboard();
        input = new ConsoleInputBuffer();
        output = new ConsoleOutputChannel();
        fs = new TestMemoryFileSystem();
        var io = TestFactory.CreateIoGateway(fs);
        var metaAccess = TestFactory.CreateFileMetaAccess(fs);
        var pathResolver = TestFactory.CreatePathResolver(fs);
        var runtime = TestFactory.CreateRuntime(logger, host, tm, bb, input, output, io);

        messages = [];
        output.Subscribe(messages.Add);

        return new SndContext(new SndContextParameters(
            runtime, io, metaAccess, pathResolver, "root", "res://initial", "entry.json"));
    }

    private static void SetupProgressRun(SndContext ctx, TestMemoryFileSystem fs)
    {
        fs.SeedFile("entry.json",
            """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""");
        fs.SeedFile("res://levels/main_menu.json", "[]");
        ctx.Lifecycle.RequestLoadMainMenuEntrySave();
        ctx.FlushFrame();
    }

    private static void SeedSaveSnapshot(
        TestMemoryFileSystem fs,
        string saveId,
        string activeLevelId,
        string? metaMap = null)
    {
        var saveDir = $"root/save_{saveId}";
        var levelDir = $"{saveDir}/level_{activeLevelId}";
        fs.SeedFile($"{saveDir}/progress.json",
            $$$"""{"origo.session_topology":{"type":"String","data":"__foreground__={{{activeLevelId}}}=false"}}""");
        fs.SeedFile($"{saveDir}/progress_state_machines.json", """{"machines":[]}""");
        fs.SeedFile($"{levelDir}/snd_scene.json", "[]");
        fs.SeedFile($"{levelDir}/session.json", "{}");
        fs.SeedFile($"{levelDir}/session_state_machines.json", """{"machines":[]}""");
        if (metaMap is not null)
            fs.SeedFile($"{saveDir}/meta.map", metaMap);
    }
}
