using System;

namespace Origo.GodotAdapter.Integration.Tests.Runner;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class IntegrationTestAttribute : Attribute
{
    public string? Description { get; set; }
}
