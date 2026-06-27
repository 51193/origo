# DataSource

> [↑ 回到 Origo.Core](../README.md)

## 模块能力

Origo 的数据源抽象层——Core 与外部格式（JSON、.map）之间的编解码桥梁。提供统一的 `DataSourceNode` 树形数据模型、按文件后缀自动路由编解码器的 I/O Gateway、以及 CLR 类型与节点数据之间的双向转换器注册表。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Codec](Codec/README.md) | 格式编解码 | `JsonDataSourceCodec`（延迟展开）+ `MapDataSourceCodec`（key:value，strict fail-fast）+ `RawStringDataSourceCodec`（`.sha`/`.write_in_progress` 原始文本） |
| [Converters](Converters/README.md) | 类型转换 | 14 种基础类型 + 14 种数组 + 8 种领域类型 + TypedData |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `DataSourceNode.cs` | 树形数据节点：Map/Array/Text/Number/Bool/Null + 延迟展开（Lazy）+ `ComputeSha256Hash()` — 迭代后序遍历生成确定性字符串表示后计算 SHA-256 哈希，用于存档幂等去重。`Dispose()` 同样使用迭代遍历防止深度嵌套树的栈溢出 |
| `DataSourceNodeKind.cs` | 节点类型枚举 |
| `DataSourceCodecKind.cs` | 编解码格式枚举（Json / Map / RawString） |
| `IDataSourceCodec.cs` | 编解码器接口：Decode/Encode |
| `IDataSourceIoGateway.cs` | I/O 网关接口：仅 `ReadTree` / `WriteTree` 两个方法，按后缀路由编解码器后读写文件（Core 与文件的唯一内容接触点），所有文件内容 I/O 均经 codec 路由，零旁路 |
| `DataSourceIoGateway.cs` | I/O 网关实现：后缀 → CodecKind 映射 + 读写 |
| `DataSourceIoOptions.cs` | I/O 选项：是否缩进输出 |
| `DataSourceFactory.cs` | 工厂：创建默认 Registry + IoGateway |
| `DataSourceConverter.cs` | 泛型转换器基类：`Read(DataSourceNode)` / `Write(T)` |
| `DataSourceConverterRegistry.cs` | 转换器注册表：按 Type 查找 Converter + 泛型 Read/Write。当精确类型未注册时，自动沿基类链和接口链回退查找。 |
| `KeyValueFileParser.cs` | key:value 格式解析器（用于 .map 文件） |
| `MemoryFileSystem.cs` | 内存文件系统实现 `IFileSystem`（public，供测试和适配层复用） |
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

## 设计原则

- **IDataSourceIoGateway 硬边界**：Core 中所有文件内容 I/O 必须经过 Gateway 的 `ReadTree`/`WriteTree`，禁止直接 `File.*` API，零旁路
- **Fail-fast**：codec 解码失败时，Gateway 将异常包装为包含文件路径的 `InvalidOperationException` 立即抛出
- **延迟展开**：JSON 大型节点在访问时才展开子节点，避免全量解析
- **零反射**：所有转换器显式注册，不使用反射自动发现

---
[↑ 回到 Origo.Core](../README.md)
