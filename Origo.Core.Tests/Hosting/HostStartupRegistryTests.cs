using System;
using Origo.Core;
using Origo.Core.DataSource;
using Origo.Core.Serialization;
using Origo.TestSupport;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Regression coverage for types persisted in the system blackboard:
///     the startup registration hook must run before the blackboard is read
///     from disk, otherwise a host recreation fails before Bootstrap.
/// </summary>
public class HostStartupRegistryTests
{
    [Fact]
    public void SystemBlackboard_CustomType_RoundTripsAcrossHostRecreation()
    {
        var fileSystem = new TestMemoryFileSystem();
        var options = CreateOptions(fileSystem);

        var first = OrigoHost.Create(options);
        first.Context.Blackboard.SystemBlackboard.SetValue("settings", new StartupSettings { Value = 42 });

        var second = OrigoHost.Create(options);
        var (found, settings) = second.Context.Blackboard.SystemBlackboard.TryGet<StartupSettings>("settings");

        Assert.True(found);
        Assert.Equal(42, settings.Value);
    }

    [Fact]
    public void SystemBlackboard_UnregisteredCustomType_HostCreationExplainsStartupHook()
    {
        var fileSystem = new TestMemoryFileSystem();
        var options = new OrigoHostOptions
        {
            Meta = new OrigoMeta("Tests", "1.0.0", "test"),
            FileSystem = fileSystem,
        };

        var first = OrigoHost.Create(options);
        first.Runtime.SndWorld.RegisterTypeMappings(
            mapping => mapping.RegisterType<StartupSettings>("startup_settings"));
        first.Runtime.SndWorld.ConverterRegistry.Register(new StartupSettingsConverter());
        first.Context.Blackboard.SystemBlackboard.SetValue("settings", new StartupSettings { Value = 7 });

        var ex = Assert.Throws<InvalidOperationException>(() => OrigoHost.Create(options));

        Assert.Contains("system.json", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ConfigureStartupRegistry", ex.Message, StringComparison.Ordinal);
    }

    private static OrigoHostOptions CreateOptions(TestMemoryFileSystem fileSystem) =>
        new()
        {
            Meta = new OrigoMeta("Tests", "1.0.0", "test"),
            FileSystem = fileSystem,
            ConfigureStartupRegistry = (typeMapping, registry) =>
            {
                typeMapping.RegisterType<StartupSettings>("startup_settings");
                registry.Register(new StartupSettingsConverter());
            },
        };

    private sealed class StartupSettings
    {
        public int Value { get; init; }
    }

    private sealed class StartupSettingsConverter : DataSourceConverter<StartupSettings>
    {
        public override StartupSettings Read(DataSourceNode node) =>
            new() { Value = node["value"].As<int>() };

        public override DataSourceNode Write(StartupSettings value) =>
            DataSourceNode.CreateObject().Add("value", DataSourceNode.CreateNumber(value.Value));
    }
}
