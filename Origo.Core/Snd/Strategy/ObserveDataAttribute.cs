using System;

namespace Origo.Core.Snd.Strategy;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ObserveDataAttribute : Attribute
{
    public ObserveDataAttribute(string dataKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataKey);
        DataKey = dataKey;
    }

    public string DataKey { get; }
}
