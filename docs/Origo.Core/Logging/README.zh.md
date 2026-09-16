<!-- docsync-pair: Origo.Core/Logging/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Logging

> [↑ 回到 Origo.Core](../README.zh.md) · [↔ 抽象: Abstractions/Logging](../Abstractions/Logging/README.zh.md)

## 概述

`ILogger` 接口的实现层。提供两个实现：生产可用的结构化日志构建器 `LogMessageBuilder`，以及用于测试或静默场景的 `NullLogger`。

## 包含文件

| 文件 | 职责 |
|------|------|
| `LogMessageBuilder.cs` | 流式构建结构化日志消息（上下文 + 耗时） |
| `NullLogger.cs` | 无输出日志实现，单例模式 |
| `Logger.cs` | `Logger<T>` 泛型包装器：将任意 `ILogger` 包装为 `ILogger<T>`，自动以 `typeof(T).Name` 作为 tag |

## 实现详解

### LogMessageBuilder

流式 API，支持链式调用：

```
builder.SetElapsedMs(12.3).AddContext("entity", "player").AddContext("hp", 100).Build("damage applied")
// 输出: [+12.30ms] damage applied | entity=player, hp=100
```

- **耗时**：可选，以 `[+X.XXms]` 格式前置
- **上下文**：消息后以 ` | key=val, key=val` 格式追加
- **空值过滤**：key 非空白才添加（value 允许为 null，输出为 `key=`）
- **多次调用 AddContext**：按添加顺序拼接，用逗号分隔；同名 key 重复添加时替换原值（保持原位置，不追加）

### NullLogger

单例实现，`Log` 方法为空操作体。用于测试或不需要日志的上下文，避免 null 检查。

## 设计决策

### 为什么 LogMessageBuilder 使用统一的 AddContext 而非 Prefix/Suffix 分离

前缀/后缀的区分在实际使用中没有带来语义清晰度。统一的上下文集合（按添加顺序输出）简化了 API，同时保持了结构化能力。

### 为什么不直接在 ILogger 提供结构化方法

`ILogger` 保持最小接口（仅 `Log(level, tag, message)`），以适配不同的日志后端（Godot 的 `GD.Print`、文件 appender、网络日志）。结构化构建是 Core 层的增值服务，由 `LogMessageBuilder` 负责，最终产出纯字符串。

### 为什么 NullLogger 使用单例

`NullLogger` 无任何可变状态，创建多个实例是资源浪费。私有构造函数 + 静态 Instance 属性确保全局唯一。

> **消费说明**：`Logger<T>` 泛型包装器在 Core 仓库内没有生产消费者（仅供测试使用）——它作为公共扩展点提供给下游（策略库、适配层或游戏代码）用于类型化日志。

---

[↑ 回到 Origo.Core](../README.zh.md)
