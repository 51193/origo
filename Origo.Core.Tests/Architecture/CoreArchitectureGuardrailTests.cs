using System;
using System.Linq;
using System.Reflection;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Lifecycle;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class CoreArchitectureGuardrailTests
{
    [Fact]
    public void CoreAssembly_ShouldNotReferenceGodot()
    {
        var refs = typeof(OrigoRuntime).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs,
            r => r.Name != null && r.Name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CorePublicSurface_ShouldNotExposeInternalInfrastructureTypes()
    {
        var exportedNames = typeof(OrigoRuntime).Assembly
            .GetExportedTypes()
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        Assert.DoesNotContain("Origo.Core.Abstractions.INodeHost", exportedNames);
        Assert.DoesNotContain("Origo.Core.Snd.SndMappings", exportedNames);
        Assert.DoesNotContain("Origo.Core.Snd.Strategy.SndStrategyPool", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Lifecycle.SystemRuntime", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Lifecycle.ProgressRuntime", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Lifecycle.SessionManagerRuntime", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Lifecycle.RunStateScope", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Lifecycle.RunDependencies", exportedNames);
        // SessionManager is now internal, should not be in public surface.
        Assert.DoesNotContain("Origo.Core.Runtime.Lifecycle.SessionManager", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Lifecycle.EmptySessionManager", exportedNames);

        // Console command handler implementations are internal.
        Assert.DoesNotContain("Origo.Core.Runtime.Console.CommandHandlers.AutoSaveCommandHandler", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Console.CommandHandlers.SaveGameCommandHandler", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Console.CommandHandlers.LoadGameCommandHandler", exportedNames);
        Assert.DoesNotContain("Origo.Core.Runtime.Console.CommandHandlers.ChangeLevelCommandHandler", exportedNames);

        // Save infrastructure utilities are internal.
        Assert.DoesNotContain("Origo.Core.Save.Meta.SaveMetaMerger", exportedNames);
        Assert.DoesNotContain("Origo.Core.Save.Storage.SaveStorageFacade", exportedNames);
        Assert.DoesNotContain("Origo.Core.Save.Storage.SavePathLayout", exportedNames);

        // NullNode types are internal (used only by FullMemorySndSceneHost).
        Assert.DoesNotContain("Origo.Core.Snd.Scene.NullNodeFactory", exportedNames);
        Assert.DoesNotContain("Origo.Core.Snd.Scene.NullNodeHandle", exportedNames);
    }

    [Fact]
    public void SndContext_ShouldNotExposeRuntimeOrSystemQueueAsPublicApi()
    {
        var type = typeof(SndContext);
        Assert.Null(type.GetProperty("Runtime", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetProperty("FileSystem", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetMethod("EnqueueSystemDeferred", BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void SndContext_ShouldNotExposeSessionCreationOrForegroundShortcut()
    {
        var type = typeof(SndContext);
        // Session creation is now exclusively through ISessionManager.
        Assert.Null(type.GetMethod("CreateBackgroundSession", BindingFlags.Instance | BindingFlags.Public));
        Assert.Null(type.GetMethod("CreateBackgroundSessionFromPayload", BindingFlags.Instance | BindingFlags.Public));
        // ForegroundSession shortcut removed — use SessionManager.ForegroundSession.
        Assert.Null(type.GetProperty("ForegroundSession", BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void ProgressRun_ShouldNotExposeLifecycleMethodsAsPublicApi()
    {
        var type = typeof(ProgressRun);
        var publicMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        Assert.DoesNotContain("SetSaveId", publicMethods);
        Assert.DoesNotContain("LoadFromPayload", publicMethods);
        Assert.DoesNotContain("LoadAndMountForeground", publicMethods);
        Assert.DoesNotContain("SwitchForeground", publicMethods);
        Assert.DoesNotContain("PersistProgress", publicMethods);
        Assert.DoesNotContain("BuildSaveMetaContext", publicMethods);
        Assert.DoesNotContain("BuildSavePayload", publicMethods);
    }

    [Fact]
    public void ISessionRun_ShouldNotExposeLifecycleOrSerializationMethods()
    {
        var type = typeof(ISessionRun);
        var methodNames = type.GetMethods().Select(m => m.Name).ToArray();
        var propertyNames = type.GetProperties().Select(p => p.Name).ToArray();

        // Lifecycle and serialization methods are managed by SessionManager.
        Assert.DoesNotContain("MountKey", propertyNames);
        Assert.DoesNotContain("SerializeToPayload", methodNames);
        Assert.DoesNotContain("LoadFromPayload", methodNames);
        Assert.DoesNotContain("PersistLevelState", methodNames);
    }

    [Fact]
    public void ISessionManager_ShouldNotExposeProcessingKeys()
    {
        var type = typeof(ISessionManager);
        Assert.Null(type.GetProperty("ProcessingKeys"));
    }

    [Fact]
    public void ISessionManager_ShouldNotExposeMountOrUnmount()
    {
        var type = typeof(ISessionManager);
        var methodNames = type.GetMethods().Select(m => m.Name).ToArray();

        // Mount/Unmount replaced by CreateBackgroundSession/DestroySession.
        Assert.DoesNotContain("Mount", methodNames);
        Assert.DoesNotContain("Unmount", methodNames);
    }
}
