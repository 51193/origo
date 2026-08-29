using System;
using System.Collections.Generic;
using Origo.Core.Abstractions.Node;

namespace Origo.TestSupport;

/// <summary>
///     Test <see cref="INodeFactory" /> that records every create request and
///     can simulate creation failures for selected resource ids.
/// </summary>
public sealed class TestNodeFactory(IEnumerable<string>? resourceIdsThatFail = null) : INodeFactory
{
    private readonly HashSet<string> _resourceIdsThatFail = resourceIdsThatFail != null
        ? new HashSet<string>(resourceIdsThatFail, StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    /// <summary>Every successfully created test handle, in creation order.</summary>
    public readonly List<TestNodeHandle> CreatedHandles = [];

    /// <summary>Every create request (logical name, resource id) in call order.</summary>
    public readonly List<(string logicalName, string resourceId)> Requests = [];

    /// <inheritdoc/>
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
