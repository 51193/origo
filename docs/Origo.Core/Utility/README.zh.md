<!-- docsync-pair: Origo.Core/Utility/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Utility

> [↑ 回到 Origo.Core](../README.zh.md)

## 功能概述

通用工具函数，提供集合差异比较（`DiffUtility`）与路径规范化（`PathUtility`，被 `GodotDirectoryOperations` 等适配层消费）等纯函数辅助能力。

## 文件清单

| 文件 | 职责 |
|------|------|
| `DiffUtility.cs` | 通用集合差异比较：给定新旧两个集合，返回 (新增元素, 移除元素) |
| `PathUtility.cs` | 路径操作工具：`Combine`（路径拼接 + 遍历攻击检测）、`GetParentDirectory`（父目录提取 + 根路径边界处理）、`NormalizeDirectoryPath`（去除尾部斜杠）、`ExtractGlobSuffix`（`"*.json"` → `".json"`） |

## 设计决策

### 为什么实现为静态工具类

`DiffUtility` 是无状态的纯函数，不依赖任何外部抽象。作为静态工具类可以零成本调用，无需通过依赖注入或接口抽象。

### 为什么不使用更复杂的 diff 算法

当前用途仅需检测集合元素的增删（策略绑定、观察者绑定等拓扑变化的计算），不需要 LCS 等序列级 diff。HashSet-based 实现已足够。

---

[↑ 回到 Origo.Core](../README.zh.md)
