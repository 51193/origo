using System;
using System.Linq;
using System.Reflection;
using Origo.Core;
using Origo.TestSupport;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Runtime;
using Origo.Core.Kernel.Ports;
using Origo.Core.Snd;
using Origo.Core.Snd.Metadata;
using Origo.Core.Snd.Strategy;
using Xunit;

namespace Origo.Core.Tests;

public class OrigoHostTests
{
    [Fact]
    public void HostKernelPort_ShouldBeInternal_AndLiveInKernelPortsNamespace()
    {
        var portType = typeof(HostKernelPort);
        Assert.False(portType.IsVisible, "Kernel ports must remain internal.");
        Assert.Equal("Origo.Core.Kernel.Ports", portType.Namespace);
        Assert.False(typeof(IHostKernelPort).IsVisible, "Kernel port contracts must remain internal.");
        Assert.DoesNotContain(portType.Assembly.GetExportedTypes(), type => type == typeof(IHostKernelPort));
    }

    [Fact]
    public void OrigoHost_ShouldCreateStableRuntimeAndRunBackgroundWorkflow()
    {
        var fileSystem = new MemoryFileSystem();
        fileSystem.WriteAllText("entry.json",
            "{ \"levels\": { \"main_menu\": { \"snd_scene\": \"main_menu.json\" } }, \"main_menu_level\": \"main_menu\" }",
            overwrite: true);
        fileSystem.WriteAllText("main_menu.json", "[]", overwrite: true);

        var host = OrigoHost.Create(new OrigoHostOptions
        {
            Meta = new OrigoMeta("Tests", "1.0.0", "test"),
            FileSystem = fileSystem,
            AutoDiscoverStrategies = false,
        });

        Assert.IsType<IOrigoRuntime>(host.Runtime, exactMatch: false);
        Assert.IsType<ISndWorldAccess>(host.Runtime.SndWorld, exactMatch: false);
        Assert.NotNull(host.Context);

        host.Runtime.SndWorld.RegisterStrategy(static () => new HostProbeStrategy());
        host.Bootstrap();
        host.DriveFrame(0.016);

        var session = host.Runtime.SessionManager.ForegroundSession;
        Assert.NotNull(session);
        var entity = session.Spawn(new SndMetaFluentBuilder("HostProbe")
            .AddLifecycleStrategy(HostProbeStrategy.Index)
            .Build());

        host.DriveFrame(0.016);

        var (found, ticks) = entity.TryGetData<int>("host_probe_ticks");
        Assert.True(found);
        Assert.True(ticks >= 2);
    }

    [Fact]
    public void CoreShellPublicSignatures_ShouldNotExposeKernelTypes()
    {
        var exported = typeof(OrigoHost).Assembly.GetExportedTypes();
        foreach (var type in exported)
        {
            AssertNotKernel(type);
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                switch (member)
                {
                    case PropertyInfo property:
                        AssertNotKernel(property.PropertyType);
                        break;
                    case MethodInfo method:
                        AssertNotKernel(method.ReturnType);
                        foreach (var parameter in method.GetParameters())
                            AssertNotKernel(parameter.ParameterType);
                        break;
                    case FieldInfo field:
                        AssertNotKernel(field.FieldType);
                        break;
                }
            }
        }
    }

    private static void AssertNotKernel(Type type)
    {
        var current = type;
        while (current.HasElementType)
            current = current.GetElementType()!;

        var assemblyName = current.Assembly.GetName().Name;
        Assert.False(
            string.Equals(assemblyName, "Origo.Core.Kernel", StringComparison.Ordinal),
            $"Kernel type '{current.FullName}' leaked into the Core shell public signature.");
    }

    [StrategyIndex(Index)]
    private sealed class HostProbeStrategy : LifecycleStrategyBase
    {
        internal const string Index = "test.host_probe";

        public override void AfterSpawn(ISndEntity entity, ISndContext ctx) => Increment(entity);

        public override void Process(ISndEntity entity, double delta, ISndContext ctx) => Increment(entity);

        private static void Increment(ISndEntity entity)
        {
            var (_, ticks) = entity.TryGetData<int>("host_probe_ticks");
            entity.SetData("host_probe_ticks", ticks + 1);
        }
    }
}
