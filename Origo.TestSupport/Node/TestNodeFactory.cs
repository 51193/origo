using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Node;

namespace Origo.TestSupport;

public sealed class TestNodeFactory(IEnumerable<string>? resourceIdsThatFail = null) : INodeFactory
{
    private readonly HashSet<string> _resourceIdsThatFail = resourceIdsThatFail != null
        ? new HashSet<string>(resourceIdsThatFail, StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    public readonly List<TestNodeHandle> CreatedHandles = [];

    public readonly List<(string logicalName, string resourceId)> Requests = [];

    public INodeHandle Create(string logicalName, string resourceId)
    {
        Requests.Add((logicalName, resourceId));
        if (_resourceIdsThatFail.Contains(resourceId))
            throw new InvalidOperationException($"Simulated node creation failure for resourceId='{resourceId}'.");

        var handle = new TestNodeHandle(logicalName);
        CreatedHandles.Add(handle);
        return handle;
    }
}
