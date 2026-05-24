using System;
using System.Collections.Generic;
using Origo.Core.Logging;
using Origo.Core.Scheduling;
using Xunit;

namespace Origo.Core.Tests;

public class SchedulingAndTypeMappingTests
{
    [Fact]
    public void ActionScheduler_Tick_ExecutesQueuedAndNestedActions()
    {
        var scheduler = new ActionScheduler(NullLogger.Instance);
        var order = new List<int>();

        scheduler.Enqueue(() =>
        {
            order.Add(1);
            scheduler.Enqueue(() => order.Add(3));
        });
        scheduler.Enqueue(() => order.Add(2));

        var executed = scheduler.Tick();

        Assert.Equal(3, executed);
        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void ActionScheduler_Clear_RemovesPendingActions()
    {
        var scheduler = new ActionScheduler(NullLogger.Instance);
        scheduler.Enqueue(() => throw new InvalidOperationException("should not execute"));
        scheduler.Clear();

        var executed = scheduler.Tick();
        Assert.Equal(0, executed);
    }

    [Fact]
    public void TypeStringMapping_HasDefaultTypes_AndSupportsCustomRegistration()
    {
        var mapping = new TypeStringMapping();

        Assert.Equal(typeof(int), mapping.GetTypeByName("Int32"));
        Assert.Equal("ArrayString", mapping.GetNameByType(typeof(string[])));

        mapping.RegisterType<Guid>("Guid");
        Assert.Equal(typeof(Guid), mapping.GetTypeByName("Guid"));
        Assert.Equal("Guid", mapping.GetNameByType(typeof(Guid)));
    }
}
