using System;
using System.Collections.Generic;
using Origo.Core.Save.Meta;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     Covers the read-only blackboard facade used by save-meta contexts: reads
///     delegate to the wrapped blackboard (including the found-null nullable
///     contract) and every mutation fails fast.
/// </summary>
public class ReadOnlyBlackboardTests
{
    [Fact]
    public void ReadOnlyBlackboard_ReadsDelegateAndPreserveFoundNull()
    {
        var inner = new Blackboard.Blackboard();
        inner.SetValue<string?>("note", null);
        inner.SetValue("score", 7);

        var readOnly = new ReadOnlyBlackboard(inner);

        var (found, note) = readOnly.TryGet<string>("note");
        Assert.True(found);
        Assert.Null(note);

        Assert.Equal(7, readOnly.TryGet<int>("score").value);
        Assert.False(readOnly.TryGet<int>("missing").found);
        Assert.False(readOnly.TryGet<long>("score").found);
        Assert.Contains("score", readOnly.GetKeys());
        Assert.True(readOnly.SerializeAll().ContainsKey("score"));
    }

    [Fact]
    public void ReadOnlyBlackboard_MutationsThrow()
    {
        var readOnly = new ReadOnlyBlackboard(new Blackboard.Blackboard());

        Assert.Throws<InvalidOperationException>(() => readOnly.SetValue("x", 1));
        Assert.Throws<InvalidOperationException>(() => readOnly.Clear());
        Assert.Throws<InvalidOperationException>(
            () => readOnly.DeserializeAll(new Dictionary<string, TypedData>()));
    }
}
