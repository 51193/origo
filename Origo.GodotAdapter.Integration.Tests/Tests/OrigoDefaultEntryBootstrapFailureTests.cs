using System;
using Godot;
using Origo.Core.Snd;
using Origo.GodotAdapter.Bootstrap;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

/// <summary>
///     Regression: a derived entry whose post-runtime bootstrap step throws
///     must mark the host failed so the next frame fails fast instead of
///     driving a partially initialized runtime.
/// </summary>
public partial class OrigoDefaultEntryBootstrapFailureTests : IDeferredTestFixture
{
    private FailingAfterRuntimeEntry? _entry;
    private int _frame;

    public bool IsComplete => _frame >= 1;

    public void Setup()
    {
        _frame = 0;
        _entry = new FailingAfterRuntimeEntry { Name = "FailingAfterRuntimeEntry" };
    }

    public void AdvanceFrame() => _frame++;

    [DeferredTest(Description = "Derived entry post-runtime bootstrap failure disables frame driving")]
    public void DefaultEntry_PostRuntimeBootstrapFailure_FailsFastOnProcess()
    {
        var entry = _entry!;
        var root = ((SceneTree)Engine.GetMainLoop()).Root;

        // _Ready runs synchronously when the node enters the tree. Godot logs
        // the intentional hook exception; whether add_child rethrows is an
        // engine detail, so only assert that the runtime was created before
        // the failing post-runtime step.
        root.AddChild(entry);
        IntegrationTestRunner.AssertNotNull(entry.Runtime, "Runtime created by OrigoAutoHost before the failing step");

        InvalidOperationException? processError = null;
        try
        {
            entry._Process(0.016);
        }
        catch (InvalidOperationException ex)
        {
            processError = ex;
        }

        IntegrationTestRunner.Assert(
            processError is not null,
            "a partially initialized entry must fail fast on _Process instead of driving frames");
        IntegrationTestRunner.AssertContains("bootstrap failed", processError!.Message, "_Process error");

        root.RemoveChild(entry);
        entry.QueueFree();
    }

    private sealed partial class FailingAfterRuntimeEntry : OrigoDefaultEntry
    {
        protected override void ConfigureSaveMetadataContributors(ISndContext context)
            => throw new InvalidOperationException("Intentional post-runtime bootstrap failure");
    }
}
