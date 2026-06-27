# Codec

> [↑ 回到 DataSource](../README.md)

## 概述

`IDataSourceCodec` 接口的具体编解码实现。负责将 `DataSourceNode` 与外部数据格式（JSON、自定义 `.map` 格式、原始文本）互相转换。所有编解码器均为 `internal`，仅通过 `DataSourceIoGateway` 按文件后缀路由调用。所有文件内容 I/O 均经 codec 路由，零旁路。

## 包含文件

| 文件 | 职责 |
|------|------|
| `JsonDataSourceCodec.cs` | JSON ↔ DataSourceNode 编解码，支持延迟展开 |
| `MapDataSourceCodec.cs` | key:value 格式（`.map`）编解码，扁平结构，strict 模式（格式错误抛 `FormatException`） |
| `RawStringDataSourceCodec.cs` | 原始文本 ↔ DataSourceNode 编解码（`.sha`/`.write_in_progress`），将整个文件内容包装/解包为单字符串节点 |

## 实现详解

### JsonDataSourceCodec

- **解码时延迟展开**：对象/数组的子节点包装为 `DataSourceNode.CreateLazy(rawText, ExpandOneLevel)`，避免一次性将大型 JSON 完全解析为内存树
- 只有在通过 `node[key]` 或迭代访问时，才展开下一层
- **编码**：递归遍历 DataSourceNode 树，通过 `Utf8JsonWriter` 输出，支持缩进格式控制

### MapDataSourceCodec

- 解析 `key: value` 格式的行文件（`#` 开头的行视为注释，跳过）
- 所有值均为字符串类型
- 不支持延迟加载（`.map` 文件通常较小且扁平，无需延迟）
- 编码时按键的字典序输出，null 值被跳过
- **Strict 模式（`strict: true`）**：遇到格式错误的行时立即抛出 `FormatException`，Gateway 将其包装为包含文件路径信息的 `InvalidOperationException`（fail-fast）

### RawStringDataSourceCodec

- 处理 `.sha` 和 `.write_in_progress` 等原始文本文件
- 解码时将整个文件内容作为单个字符串值包装为 `DataSourceNode`
- 编码时从 `DataSourceNode` 中解包字符串值写入文件
- 确保所有文件内容 I/O 均经 codec 路由，消除直读直写旁路

## 设计决策

### 为什么 JSON 使用延迟展开

游戏存档的 JSON 可能嵌套很深（SND 场景文件包含全部实体、数据、策略），但每次访问通常只触及少数节点。延迟展开将解析开销分摊到访问时，避免初始加载时的全量解析阻塞。

### 为什么 .map 不支持延迟加载

`.map` 文件（如 `session_topology.map`）是简单的扁平键值表，通常只有几十行。延迟加载在此场景下没有性能收益，反而增加实现复杂度。

### 为什么编码器是 internal

编解码由 `DataSourceIoGateway` 统一调度，外部代码通过 Gateway 读写文件，不直接接触具体编解码器。internal 可见性防止业务代码绕过 Gateway 直接编解码。

---
[↑ 回到 DataSource](../README.md)
