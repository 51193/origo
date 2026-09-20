<!-- docsync-pair: Origo.Core.Contracts/DataSource/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# DataSource

> [↑ 回到 Origo.Core.Contracts](../README.zh.md) · [↔ 实现: Origo.Core/DataSource](../../Origo.Core/DataSource/README.zh.md)

## 模块能力

data-source 契约层：树形数据模型、I/O 网关契约、文件元数据契约与类型转换器基类。
Codec、Factory、Registry 与具体 converter 实现位于 [Origo.Core/DataSource](../../Origo.Core/DataSource/README.zh.md)。

## 包含文件

| 文件 | 职责 |
|------|------|
| `DataSourceNode.cs` | 树形数据节点：Map/Array/Text/Number/Bool/Null + 延迟展开 + `As<T>()` + Builder `Add` + 严格形状的 `Keys`/`Elements` + `ComputeSha256Hash()` |
| `DataSourceNodeKind.cs` | 节点类型枚举 |
| `DataSourceConverter.cs` | 转换器基类：`DataSourceConverterBase` 与 `DataSourceConverter<T>` |
| `IDataSourceIoGateway.cs` | I/O 网关契约：`ReadTree` / `WriteTree` |
| `IFileMetaAccess.cs` | 文件元数据契约：存在性、枚举、目录、删除、复制、重命名 |

## 设计决策

### 为什么 DataSourceNode 形状访问是严格的

`Keys` / `Elements` 对非 Map/Array 节点立即抛 `InvalidOperationException`，不会把错误形状静默读成空集合。存档读取是严格路径，错误形状必须显式失败。

### 为什么节点树实现 IDisposable

延迟展开持有子节点与闭包资源。`Dispose()` 使用迭代遍历释放整棵树，防止深度嵌套树在递归释放时栈溢出；存档边界的节点所有权由调用方负责释放。

### 为什么转换器基类进入 Contracts

`DataSourceConverter<T>` / `DataSourceConverterBase` 是消费者与适配器扩展点；具体注册表与默认 converter 实现保留在实现包，契约层不包含运行时构造逻辑。

---
[↑ 回到 Origo.Core.Contracts](../README.zh.md)
