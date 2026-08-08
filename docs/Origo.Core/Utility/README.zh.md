<!-- docsync-pair: Origo.Core/Utility/README -->
<!-- docsync-revision: 7 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Utility

> [↑ 回到 Origo.Core](../README.zh.md)

## 功能概述

通用工具函数，提供路径规范化（`PathUtility`，被 `GodotDirectoryOperations` 等适配层消费）与字符串到类型值推断（`ValueInference`，被 Archetype 与控制台类型推断共享）等纯函数辅助能力。

## 文件清单

| 文件 | 职责 |
|------|------|
| `PathUtility.cs` | 路径操作工具：`Combine`（路径拼接 + 遍历攻击检测；`basePath` 为 null 抛 `ArgumentNullException`，空字符串 base 直接透传 relative）、`GetParentDirectory`（父目录提取 + 根路径边界处理）、`NormalizeDirectoryPath`（去除尾部斜杠；null 抛 `ArgumentNullException`）、`ExtractGlobSuffix`（`"*.json"` → `".json"`）。三个路径函数均识别 `scheme://` 方案根（如 `user://`）：根不会被去尾斜杠破坏，`user://x` 的父目录正确返回 `user://`，方案根自身无父目录 |
| `ValueInference.cs` | `internal` — 字符串到类型值的统一推断（int → long → float → bool → string；float 解析拒绝 NaN/Infinity），供 `SndArchetypeLoader` 与控制台 `bb_set` / `entity_set_data` 共用 |

## 设计决策

### 为什么实现为静态工具类

`PathUtility` 与 `ValueInference` 是无状态的纯函数，不依赖任何外部抽象。作为静态工具类可以零成本调用，无需通过依赖注入或接口抽象。

---

[↑ 回到 Origo.Core](../README.zh.md)
