# Blackboard (Abstractions)

> [↑ 回到 Abstractions](../README.md) · [↔ 实现: Blackboard](../../Blackboard/README.md)

## 概述

定义通用键值黑板接口 `IBlackboard`，面向 Core 层的全局/进度/会话级共享状态。所有黑板操作通过泛型方法保持类型安全，内部使用 `TypedData` 保留类型信息，确保序列化/反序列化后类型不丢失。

## 包含文件

| 文件 | 职责 |
|------|------|
| `IBlackboard.cs` | 黑板核心接口：SetValue/Get/Clear/Keys + 序列化/反序列化 |

## 接口成员

| 方法 | 说明 |
|------|------|
| `SetValue<T>(string key, T value)` | 写入键值对，保留完整类型信息 |
| `TryGet<T>(string key)` | 安全读取：返回 `(found, value)` 元组 |
| `Clear()` | 清空所有键值 |
| `GetKeys()` | 枚举全部键名 |
| `SerializeAll()` | 导出全部条目为 `TypedData` 字典，用于持久化 |
| `DeserializeAll(...)` | 从持久化字典恢复全部条目，替换当前内容 |

## 设计决策

### 为什么用 TypedData 包装值

直接用 `object` 存值会在序列化时丢失精确类型（例如 `int` 和 `float` 混淆）。`TypedData` 是 `readonly partial struct`，在存储时以内联值类型携带类型元数据，避免堆分配开销的同时，使得 JSON 反序列化后能精确还原原始类型，这对数值敏感的游戏逻辑（如伤害计算）至关重要。

### 为什么是泛型 TryGet 而非 object

`TryGet<T>` 强制调用方声明期望类型，在编译期捕获类型不匹配。同时 `(found, value)` 元组模式避免了 `null` 检查的歧义——值类型的 `default(T)` 无法与"键不存在"区分，必须通过 `found` 标志判断。

### 为什么不提供事件订阅

黑板是纯数据容器，变更通知职责属于上层模块（如 `SndDataManager` 的数据观察者）。在黑板层添加事件会引入不必要的耦合和生命周期管理负担。

---
[↑ 回到 Abstractions](../README.md)
