# Archetype

> [↑ 回到 Snd](../README.md)

## 设计定位

Archetype 是 Origo 提供的**数值外部化工具**——将实体属性数值抽取为独立的扁平键值对文件，供策略按需加载。

- Archetype 不包含任何行为定义（无策略索引、无节点引用）
- Archetype 与 Template 没有强制对应关系：一个实体可以不使用、使用一个或多个配方
- 加载时机和数量完全由策略逻辑决定

典型场景：同一套行为逻辑需要服务多种数值变体（不同食物的营养值、不同装备的属性），或属性数值需要脱离代码管理（策划调参、数据驱动）。

## 概述

数值配方文件的加载与应用工具。解析扁平键值对格式的 Archetype 定义文件，将字符串值推断为最合适的类型（int/long/float/bool/string），并写入实体数据字典。文件的实际格式由框架文件抽象的后缀路由决定。

## 包含文件

| 文件 | 职责 |
|------|------|
| `SndArchetypeLoader.cs` | TryLoad（从 .map 文件读取键值对）+ ApplyAttributes（类型推断并写入实体数据）|

## 实现详解

### SndArchetypeLoader

- **TryLoad(ISndFileAccess fileAccess, string path)**：通过框架文件抽象读取 .map 文件，要求根节点为 Object，返回 `Dictionary<string, string>`。文件不存在或格式不正确时返回 false
- **ApplyAttributes(ISndEntity entity, Dictionary<string, string> attributes)**：遍历属性字典，按 int → long → float → bool → string 的顺序尝试解析每个值

类型推断规则：
1. `int.TryParse`（`InvariantCulture`）成功 → 写入为 `int`
2. `long.TryParse`（`InvariantCulture`）成功 → 写入为 `long`（超出 int 范围的整数，避免被浮点化丢失精度）
3. `float.TryParse`（`InvariantCulture`）成功 → 写入为 `float`
4. `bool.TryParse` 成功 → 写入为 `bool`
5. 上述均失败 → 写入为 `string`

## 设计决策

### 为什么使用严格的类型推断顺序而非类型注解

`.map` 格式设计起源于简洁的 key:value 文本文件，不包含类型元信息。int → long → float → bool → string 的回退顺序确保 `"100"` 存为 `int`、超出 int 范围的整数存为 `long`（不被浮点化丢失精度）、`"3.14"` 存为 `float`、`"true"` 存为 `bool`，符合游戏数据最常用场景。如需精确类型控制，使用 JSON 模板（`snd_templates`）。

### 为什么作为独立 Snd 子模块而非放在 DataSource

虽然逻辑上涉及文件解析，但其输出是针对 `ISndEntity` 的数据写入，直接服务于 SND 数值配方加载工作流。DataSource 是通用数据层，不应包含 SND 绑定。

---

[↑ 回到 Snd](../README.md)
