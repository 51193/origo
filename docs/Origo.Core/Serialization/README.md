# Serialization

> [↑ 回到 Origo.Core](../README.md) · [↔ DataSource: Converters](../DataSource/Converters/README.md)

## 概述

Core 层的类型-字符串映射基础设施。在 JSON 序列化中，`TypedData` 需要知道值的精确 CLR 类型才能正确反序列化。对于已注册类型，类型识别码（kind discriminator）通过编译期生成的查找表提供 `Type`；对于未注册类型，则通过 `_ref?.GetType()` 获取运行时类型。`TypeStringMapping` 维护类型与稳定字符串标识符的双向映射，避免在 JSON 中嵌入完整类型名。

## 包含文件

| 文件 | 职责 |
|------|------|
| `BclTypeNames.cs` | 14 种 BCL 基础类型 + 14 种数组类型 + 2 种不可变集合类型的稳定字符串常量 |
| `TypeStringMapping.cs` | 类型 ↔ 字符串名双向映射表，启动时注册所有已知类型 |

## 实现详解

### BclTypeNames

仅包含常量字符串定义。使用简短名称（如 `"Int32"`、`"ArraySingle"`）而非完整 CLR 名（`"System.Int32"`），减小 JSON 体积。

### TypeStringMapping

- **构造函数**：预注册 28 个 BCL 类型（14 标量 + 14 数组）+ `IReadOnlyDictionary<string,string>` 和 `ReadOnlyDictionary<string,string>`
- **RegisterType<T>()**：公开方法，供适配层注册引擎特有类型（如 `Godot.Vector2`），也可用于注册自定义游戏类型
- **双向校验**：注册时检查类型名是否已被占用、类型是否已被映射，冲突时抛 `InvalidOperationException`
- **查找失败即抛**：`GetTypeByName` 和 `GetNameByType` 在未注册时均抛异常，不返回 null

## 设计决策

### 为什么不使用 Type.FullName 直接序列化

`Type.FullName` 随命名空间和程序集版本变化，存档跨版本恢复时类型名可能不匹配。稳定字符串映射将类型标识与代码结构解耦，存档格式保持前向兼容。

### 为什么类型名不使用 "System." 前缀

当前类型名如 `"Int32"` 足够区分。若未来需要支持重名类型（不同命名空间），可通过适配层注册带命名空间的名称。当前简化减少 JSON 存储开销。

### 为什么查找失败直接抛异常

类型映射是序列化系统的骨架。读取到一个未知类型名意味着存档格式错误或不兼容，最佳处理是立即失败并报告，而非返回 null 后在更下游产生难以诊断的错误。

---
[↑ 回到 Origo.Core](../README.md)
