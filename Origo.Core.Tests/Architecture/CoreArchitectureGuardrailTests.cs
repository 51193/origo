using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Origo.Core.Abstractions.Snd;
using Origo.Core.Abstractions.StateMachine;
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

    [Fact]
    public void ISndContext_ShouldBeCompositionInterface_WithNoOwnMethodDeclarations()
    {
        var type = typeof(ISndContext);
        var ownMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var ownProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        Assert.Empty(ownMethods);
        Assert.Empty(ownProperties);
    }

    [Fact]
    public void ISndContext_ShouldInheritAllRoleInterfaces()
    {
        var type = typeof(ISndContext);
        var interfaces = type.GetInterfaces();

        Assert.Contains(typeof(ISndBlackboardAccess), interfaces);
        Assert.Contains(typeof(ISndSessionAccess), interfaces);
        Assert.Contains(typeof(ISndDeferredActions), interfaces);
        Assert.Contains(typeof(ISndTemplateAccess), interfaces);
        Assert.Contains(typeof(ISndConsoleAccess), interfaces);
        Assert.Contains(typeof(ISndStateMachineAccess), interfaces);
        Assert.Contains(typeof(ISndSaveOperations), interfaces);
        Assert.Contains(typeof(ISndLifecycleOperations), interfaces);
    }

    [Fact]
    public void ISndContextPublicSurface_ShouldMatchOriginalBeforeSplit()
    {
        // ISndContext is a pure composition interface; collect members from all inherited interfaces.
        var ctxType = typeof(ISndContext);
        var allIfaces = new HashSet<Type>(ctxType.GetInterfaces()) { ctxType };

        var methodNames = new HashSet<string>(StringComparer.Ordinal);
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var iface in allIfaces)
        {
            foreach (var m in iface.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                if (!m.IsSpecialName)
                    methodNames.Add(m.Name);
            foreach (var p in iface.GetProperties(BindingFlags.Instance | BindingFlags.Public |
                                                  BindingFlags.DeclaredOnly))
                propertyNames.Add(p.Name);
        }

        methodNames.RemoveWhere(n => n is "GetType" or "ToString" or "Equals" or "GetHashCode");

        Assert.Contains("SystemBlackboard", propertyNames);
        Assert.Contains("ProgressBlackboard", propertyNames);
        Assert.Contains("SessionManager", propertyNames);
        Assert.Contains("CurrentSession", propertyNames);
        Assert.Contains("IsFrontSession", propertyNames);

        Assert.Contains("EnqueueBusinessDeferred", methodNames);
        Assert.Contains("FlushDeferredActionsForCurrentFrame", methodNames);
        Assert.Contains("GetPendingPersistenceRequestCount", methodNames);
        Assert.Contains("CloneTemplate", methodNames);
        Assert.Contains("TrySubmitConsoleCommand", methodNames);
        Assert.Contains("ProcessConsolePending", methodNames);
        Assert.Contains("SubscribeConsoleOutput", methodNames);
        Assert.Contains("UnsubscribeConsoleOutput", methodNames);
        Assert.Contains("GetProgressStateMachines", methodNames);
        Assert.Contains("ListSaves", methodNames);
        Assert.Contains("RequestLoadGame", methodNames);
        Assert.Contains("RequestSaveGame", methodNames);
        Assert.Contains("RequestSaveGameAuto", methodNames);
        Assert.Contains("SetContinueTarget", methodNames);
        Assert.Contains("RequestSwitchForegroundLevel", methodNames);
        Assert.Contains("RequestClearEntities", methodNames);
        Assert.Contains("RequestKillEntity", methodNames);
        Assert.Contains("HasContinueData", methodNames);
        Assert.Contains("RequestContinueGame", methodNames);
        Assert.Contains("RequestLoadInitialSave", methodNames);
        Assert.Contains("RequestLoadMainMenuEntrySave", methodNames);
    }

    [Fact]
    public void IStateMachineContext_ShouldInheritSharedRoleInterfaces()
    {
        var type = typeof(IStateMachineContext);
        var interfaces = type.GetInterfaces();

        Assert.Contains(typeof(ISndBlackboardAccess), interfaces);
        Assert.Contains(typeof(ISndDeferredActions), interfaces);

        // Verify no duplicate members — the shared members should only be in the role interfaces.
        var ownMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(ownMethods, m => m.Name == "get_SystemBlackboard");
        Assert.DoesNotContain(ownMethods, m => m.Name == "get_ProgressBlackboard");
        Assert.DoesNotContain(ownMethods, m => m.Name == "EnqueueBusinessDeferred");
    }
}
