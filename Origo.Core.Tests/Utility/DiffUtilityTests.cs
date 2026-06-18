using System.Collections.Generic;
using Origo.Core.Utility;
using Xunit;

namespace Origo.Core.Tests;

public class DiffUtilityTests
{
    [Fact]
    public void Diff_AddedItems_Detected()
    {
        var oldList = new List<string> { "a", "b" };
        var newList = new List<string> { "a", "b", "c" };

        var (added, removed) = DiffUtility.Diff(oldList, newList);

        Assert.Single(added);
        Assert.Equal("c", added[0]);
        Assert.Empty(removed);
    }

    [Fact]
    public void Diff_RemovedItems_Detected()
    {
        var oldList = new List<string> { "a", "b", "c" };
        var newList = new List<string> { "a", "b" };

        var (added, removed) = DiffUtility.Diff(oldList, newList);

        Assert.Empty(added);
        Assert.Single(removed);
        Assert.Equal("c", removed[0]);
    }

    [Fact]
    public void Diff_AddedAndRemoved()
    {
        var oldList = new List<int> { 1, 2, 3 };
        var newList = new List<int> { 2, 3, 4 };

        var (added, removed) = DiffUtility.Diff(oldList, newList);

        Assert.Single(added);
        Assert.Equal(4, added[0]);
        Assert.Single(removed);
        Assert.Equal(1, removed[0]);
    }

    [Fact]
    public void Diff_EmptyBoth_ReturnsEmpty()
    {
        var (added, removed) = DiffUtility.Diff(
            new List<int>(), new List<int>());

        Assert.Empty(added);
        Assert.Empty(removed);
    }

    [Fact]
    public void Diff_EmptyOld_NewHasItems_ReturnsAdded()
    {
        var (added, removed) = DiffUtility.Diff(
            new List<int>(), new List<int> { 1, 2 });

        Assert.Equal(2, added.Count);
        Assert.Empty(removed);
    }

    [Fact]
    public void Diff_EmptyNew_OldHasItems_ReturnsRemoved()
    {
        var (added, removed) = DiffUtility.Diff(
            new List<int> { 1, 2 }, new List<int>());

        Assert.Empty(added);
        Assert.Equal(2, removed.Count);
    }

    [Fact]
    public void Diff_NoChange_ReturnsEmpty()
    {
        var (added, removed) = DiffUtility.Diff(
            new List<string> { "a", "b" }, new List<string> { "a", "b" });

        Assert.Empty(added);
        Assert.Empty(removed);
    }

    [Fact]
    public void Diff_Duplicates_TreatedAsSingle()
    {
        var (added, removed) = DiffUtility.Diff(
            new List<string> { "a", "a", "b" }, new List<string> { "a", "b", "b" });

        Assert.Empty(added);
        Assert.Empty(removed);
    }

    [Fact]
    public void Diff_NullOld_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            DiffUtility.Diff<string>(null!, new List<string>()));
    }

    [Fact]
    public void Diff_NullNew_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            DiffUtility.Diff(new List<string>(), null!));
    }
}
