using System;

namespace Origo.GodotAdapter.Integration.Tests.Runner;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class DeferredTestAttribute : Attribute
{
    public string? Description { get; set; }
}
