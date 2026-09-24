using System;
using System.IO;
using Origo.Core;
using Origo.Core.Abstractions.Lifecycle;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Golden save-format tests. The fixture under
///     <c>Save/Golden/v1/save_goldenv1</c> is a real format-version 1
///     snapshot committed to the repository; loading it through
///     <see cref="OrigoHost" /> pins the persistence promise for old saves,
///     current saves, and unsupported future formats.
/// </summary>
public class SaveFormatGoldenTests
{
    private enum GoldenVariant
    {
        Current,
        MissingFormatVersion,
        FutureFormatVersion,
    }

    [Fact]
    public void GoldenV1Save_LoadsThroughShellHostAndPreservesState()
    {
        var fileSystem = new MemoryFileSystem();
        SeedGoldenSave(fileSystem, "goldenv1", GoldenVariant.Current);
        var host = CreateBootstrappedHost(fileSystem);

        host.Context.Save.RequestLoadGame("goldenv1");
        host.DriveFrame(0.016);

        var session = Assert.IsType<ISessionRun>(host.Runtime.SessionManager.ForegroundSession, exactMatch: false);
        Assert.Equal("main_menu", session.LevelId);
        var hero = session.FindByName("hero");
        Assert.NotNull(hero);
        Assert.Equal(100, hero.GetData<int>("hp"));
        Assert.Equal("Ada", hero.GetData<string>("name"));
        Assert.Equal(30, hero.GetData<int>("mana"));
        var (foundScore, score) = session.SessionBlackboard.TryGet<int>("score");
        Assert.True(foundScore);
        Assert.Equal(42, score);
        var (foundMode, mode) = session.SessionBlackboard.TryGet<string>("mode");
        Assert.True(foundMode);
        Assert.Equal("golden", mode);

        var entry = Assert.Single(host.Context.Save.ListSavesWithMetaData(), e => e.SaveId == "goldenv1");
        Assert.Equal("Golden save", entry.MetaData["display_name"]);
        Assert.DoesNotContain(entry.MetaData.Keys, key => key.StartsWith("origo.", StringComparison.Ordinal));
    }

    [Fact]
    public void GoldenV1SaveWithoutFormatVersion_LoadsAsInitialFormat()
    {
        var fileSystem = new MemoryFileSystem();
        SeedGoldenSave(fileSystem, "goldenold", GoldenVariant.MissingFormatVersion);
        var host = CreateBootstrappedHost(fileSystem);

        host.Context.Save.RequestLoadGame("goldenold");
        host.DriveFrame(0.016);

        var session = host.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(session);
        var hero = session.FindByName("hero");
        Assert.NotNull(hero);
        Assert.Equal(100, hero.GetData<int>("hp"));
        var (foundScore, score) = session.SessionBlackboard.TryGet<int>("score");
        Assert.True(foundScore);
        Assert.Equal(42, score);
    }

    [Fact]
    public void GoldenFutureFormatVersion_IsRejectedWithoutPartialMount()
    {
        var fileSystem = new MemoryFileSystem();
        SeedGoldenSave(fileSystem, "goldenfuture", GoldenVariant.FutureFormatVersion);
        var host = CreateBootstrappedHost(fileSystem);

        host.Context.Save.RequestLoadGame("goldenfuture");
        var exception = Assert.Throws<InvalidOperationException>(() => host.DriveFrame(0.016));

        Assert.Contains("newer Origo version", exception.Message, StringComparison.Ordinal);
        Assert.Contains("format version 2", exception.Message, StringComparison.Ordinal);

        // The rejected future save must not leave a partially mounted scene
        // behind: no entity from the unsupported payload is observable.
        var session = host.Runtime.SessionManager.ForegroundSession;
        Assert.Null(session?.FindByName("hero"));
    }

    private static OrigoHost CreateBootstrappedHost(MemoryFileSystem fileSystem)
    {
        fileSystem.WriteAllText("entry.json",
            """{ "levels": { "main_menu": { "snd_scene": "res://levels/main_menu.json" } }, "main_menu_level": "main_menu" }""",
            overwrite: true);
        fileSystem.WriteAllText("res://levels/main_menu.json", "[]", overwrite: true);

        var host = OrigoHost.Create(new OrigoHostOptions
        {
            Meta = new OrigoMeta("Golden", "1.0.0", "golden"),
            FileSystem = fileSystem,
            SaveRootPath = "root",
            InitialSaveRootPath = "res://initial",
            EntryConfigPath = "entry.json",
            AutoDiscoverStrategies = false,
        });
        host.Bootstrap();
        host.DriveFrame(0.016);
        return host;
    }

    private static void SeedGoldenSave(MemoryFileSystem fileSystem, string saveId, GoldenVariant variant)
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "Save", "Golden", "v1", "save_goldenv1");
        Assert.True(Directory.Exists(sourceRoot), $"Golden fixture not copied to output: {sourceRoot}");

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, sourceFile).Replace('\\', '/');
            var content = File.ReadAllText(sourceFile);

            if (relative == "meta.map")
            {
                content = variant switch
                {
                    GoldenVariant.MissingFormatVersion => content
                        .Replace("origo.format_version: 1\n", string.Empty, StringComparison.Ordinal)
                        .Replace("origo.format_version: 1", string.Empty, StringComparison.Ordinal),
                    GoldenVariant.FutureFormatVersion => content.Replace(
                        "origo.format_version: 1", "origo.format_version: 2", StringComparison.Ordinal),
                    _ => content,
                };
            }

            fileSystem.WriteAllText($"root/save_{saveId}/{relative}", content, overwrite: true);
        }
    }
}
