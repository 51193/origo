using System;
using Godot;
using Origo.Core.Blackboard;
using Xunit;

namespace Origo.GodotAdapter.Tests;

public class TypedDataAllocationTests
{
    [Fact]
    public void BlackboardTryGetVector3_SteadyState_DoesNotAllocate()
    {
        // Execute an adapter-assembly method so its module initializers run;
        // otherwise the generic factory in Contracts never sees the adapter
        // registration chain and this measures the wrong path.
        _ = new Origo.GodotAdapter.Logging.GodotLogger((_, _, _) => { });
        Assert.NotEqual(0, Origo.Core.Snd.Metadata.TypedDataLayeredRegistry.ResolveKind(typeof(Vector3)));

        var blackboard = new Blackboard();
        blackboard.SetValue("vector", new Vector3(1f, 2f, 3f));

        // Warm up JIT and the adapter fallback registration.
        for (var i = 0; i < 1000; i++)
        {
            var (found, _) = blackboard.TryGet<Vector3>("vector");
            Assert.True(found);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            var (found, _) = blackboard.TryGet<Vector3>("vector");
            if (!found)
                throw new InvalidOperationException("Vector3 value was not found.");
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocated);
    }
}
