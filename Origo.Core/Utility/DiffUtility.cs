using System;
using System.Collections.Generic;

namespace Origo.Core.Utility;

public static class DiffUtility
{
    public static (List<T> Added, List<T> Removed) Diff<T>(
        IEnumerable<T> oldItems,
        IEnumerable<T> newItems)
    {
        ArgumentNullException.ThrowIfNull(oldItems);
        ArgumentNullException.ThrowIfNull(newItems);

        var oldSet = new HashSet<T>(oldItems);
        var newSet = new HashSet<T>(newItems);

        var added = new List<T>();
        var removed = new List<T>();

        foreach (var item in newSet)
            if (!oldSet.Contains(item))
                added.Add(item);

        foreach (var item in oldSet)
            if (!newSet.Contains(item))
                removed.Add(item);

        return (added, removed);
    }
}
