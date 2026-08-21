<!-- docsync-pair: Origo.Core/DataSource/README -->
<!-- docsync-revision: 11 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# DataSource

> [↑ 回到 Origo.Core](../README.zh.md)

## 模块能力

Origo 的数据源抽象层——Core 与外部格式（JSON、.map）之间的编解码桥梁。提供统一的 `DataSourceNode` 树形数据模型、按文件后缀自动路由编解码器的 I/O Gateway、以及 CLR 类型与节点数据之间的双向转换器注册表。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Codec](Codec/README.zh.md) | 格式编解码 | `JsonDataSourceCodec`（延迟展开）+ `MapDataSourceCodec`（key:value，strict fail-fast；**无法往返的 key/value 与非 Text 子节点拒绝编码**，否则产出自身解码器读不回或静默漂移的文件；解码的重复键警告经构造注入的 logger 可观测）+ `RawStringDataSourceCodec`（`.sha`/`.write_in_progress` 原始文本） |
| [Converters](Converters/README.zh.md) | 类型转换 | 14 种基础类型 + 14 种数组 + 8 种领域类型 + TypedData |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `DataSourceNode.cs` | 树形数据节点：Map/Array/Text/Number/Bool/Null + 延迟展开（Lazy）+ `As<T>()` 泛型值访问器（支持 string/char/byte/sbyte/short/ushort/int/uint/long/ulong/float/double/decimal/bool 14 种类型）+ Builder `Add`（**仅允许在 Map/Array 节点上调用**，scalar 节点调用立即抛 `InvalidOperationException`；`null` 子节点立即拒绝）+ `Keys`/`Count`/`Elements`（**形状严格**：对非 Map/Array 节点访问立即抛 `InvalidOperationException`，防止错误形状被静默读成空集合）+ `ComputeSha256Hash()` — 迭代后序遍历生成确定性字符串表示后计算 SHA-256 哈希，用于存档幂等去重。`Dispose()` 同样使用迭代遍历防止深度嵌套树的栈溢出 |
| `DataSourceNodeKind.cs` | 节点类型枚举 |
| `DataSourceCodecKind.cs` | 编解码格式枚举（Json / Map / RawString） |
| `IDataSourceCodec.cs` | 编解码器接口：Decode/Encode |
| `IDataSourceIoGateway.cs` | I/O 网关接口：仅 `ReadTree` / `WriteTree` 两个方法，按后缀路由编解码器后读写文件（Core 与文件的唯一内容接触点），所有文件内容 I/O 均经 codec 路由，零旁路 |
| `DataSourceIoGateway.cs` | I/O 网关实现：后缀 → CodecKind 映射 + 读写 |
| `DataSourceIoOptions.cs` | I/O 路由配置：后缀 → Codec 映射（缩进选项在 `DataSourceFactory.BuildDefaultCodecs(bool)`） |
| `DataSourceFactory.cs` | 工厂：创建默认 Registry + IoGateway |
| `DataSourceConverter.cs` | 泛型转换器基类：`Read(DataSourceNode)` / `Write(T)` |
| `DataSourceConverterRegistry.cs` | 转换器注册表：按 Type 查找 Converter + 泛型 Read/Write。当精确类型未注册时，自动沿基类链和接口链回退查找。 |
| `KeyValueFileParser.cs` | key:value 格式解析器（用于 .map 文件） |
| `MemoryFileSystem.cs` | 内存文件系统实现 `IFileSystem`（internal，测试项目经 InternalsVisibleTo 使用，无生产消费者） |
| `IFileMetaAccess.cs` | 文件元数据操作接口（public）：FileExists / DirectoryExists / EnumerateFiles / EnumerateDirectories / CreateDirectory / Delete / DeleteDirectory / Copy / Rename，与 IDataSourceIoGateway 并行使用——前者负责内容读写（含 codec 路由），本接口负责文件系统结构操作 |
| `FileMetaAccess.cs` | IFileMetaAccess 默认实现（internal），委托给 IFileSystem |
| `PathResolver.cs` | IPathResolver 默认实现（internal）：CombinePath / GetParentDirectory，委托给 IFileSystem |

## 数据流

```
外部文件 (.json / .map / .sha / .write_in_progress / ...)
    │
    ▼
IDataSourceIoGateway.ReadTree / WriteTree (后缀路由 → Codec，零旁路)
    │                          ├── .json  → JsonDataSourceCodec
    │                          ├── .map   → MapDataSourceCodec (strict, fail-fast)
    │                          └── .sha / .write_in_progress → RawStringDataSourceCodec
    ▼
DataSourceNode (树形数据)
    │
    ▼
DataSourceConverterRegistry (类型转换)
    │
    ▼
CLR 对象 (TypedData / SndMetaData / etc.)
```

## 设计决策

- **IDataSourceIoGateway 硬边界**：Core 中所有文件内容 I/O 必须经过 Gateway 的 `ReadTree`/`WriteTree`，禁止直接 `File.*` API，零旁路
- **Fail-fast**：codec 解码失败时，Gateway 将异常包装为包含文件路径的 `InvalidOperationException` 立即抛出。注意：`.json` 解码采用延迟展开（见下），`JsonException` 在首次访问节点时才抛出——位于 Gateway 的 try/catch 之外，不带文件路径上下文。加载路径在首次访问时（如 `ProgressRun` 的 `ValidateLevelPayload`）会补充关卡/文件上下文；`.map`/`.sha` 为急切解码，解析错误始终经 Gateway 包装
- **延迟展开**：JSON 大型节点在访问时才展开子节点，避免全量解析
- **零反射**：所有转换器显式注册，不使用反射自动发现
- **运行时类型容器**：`DataSourceNode` 是通用序列化容器——整个 Save 系统和 DataSource 流转均通过它传递数据，类型安全推迟到 `DataSourceConverterRegistry` 查找时。这是刻意的设计权衡（"简单优于严格类型"），允许所有子系统共享同一棵数据树，代价是转换错误在运行时而非编译时暴露。
- **严格读取**：存档载荷转换器（如 `StateMachineContainerPayloadConverter`）对框架必写字段（`machines` 条目的 `key`/`pushIndex`/`popIndex`）执行必填校验，并校验数组/对象字段的节点形状（stack、pairs、indices 等）；`DataSourceNode.Keys`/`Count`/`Elements` 同样按形状拒绝错误访问，数组转换器对 null/标量/对象节点不再静默返回空数组。损坏存档立即抛 `InvalidOperationException`，绝不静默接受为默认值或空集合（fail-fast，与 Save 严格读取契约一致）
- **null 值不被静默漂移**：`Read<string>`（含运行时类型重载）遇 Null 节点抛 `InvalidOperationException`——读成空串会把 null 静默漂移为 `""`；调用方须先 `IsNull`/`TryGetValue` 检查（`TypedDataConverter` 即此模式）。`AsString()` 对 Null 节点返回空串的文档化行为保持不变（`DataSourceFactoryTests.AsString_OnNullNode_ReturnsEmpty` 钉定）

---
[↑ 回到 Origo.Core](../README.zh.md)
