using Xunit;

namespace Origo.Core.Tests;

/// <summary>
///     串行测试集合，用于包含静态可变状态的策略测试类。
///     SndStrategyPool 要求注册的策略类型必须无状态（无实例字段/可写属性），
///     因此测试策略通过静态字段共享事件接收器。这些测试类必须在串行集合中运行。
/// </summary>
[CollectionDefinition("StrategyStateTests", DisableParallelization = true)]
public class StrategyStateTestsCollection
{
}
