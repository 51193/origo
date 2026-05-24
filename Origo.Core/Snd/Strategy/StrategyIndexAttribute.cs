using System;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     可选策略索引声明。自动发现阶段优先读取该特性，避免仅为读取 Index 而实例化策略。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StrategyIndexAttribute : Attribute
{
    public StrategyIndexAttribute(string index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);
        Index = index;
    }

    public string Index { get; }
}
