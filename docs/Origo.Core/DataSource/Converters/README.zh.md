<!-- docsync-pair: Origo.Core/DataSource/Converters/README -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Converters

> [↑ 回到 DataSource](../README.zh.md)

## 概述

`DataSourceConverter<T>` 的注册实现集合。负责将 `DataSourceNode` 与 CLR 类型（基础类型、数组、领域类型）互相转换。所有转换器均为 `internal`，由 `DataSourceConverterRegistry` 统一管理和调度。

## 包含文件

| 文件 | 职责 |
|------|------|
| `PrimitiveConverters.cs` | 14 种基础类型转换器（string, byte, int, float, bool 等） |
| `ArrayConverters.cs` | 14 种基础类型数组转换器（byte[], int[], float[] 等） |
| `BlackboardDataConverter.cs` | Blackboard ↔ DataSourceNode |
| `DataMetaDataConverter.cs` | DataMetaData ↔ DataSourceNode |
| `NodeMetaDataConverter.cs` | NodeMetaData ↔ DataSourceNode |
| `SndMetaDataConverter.cs` | SndMetaData ↔ DataSourceNode |
| `SndMetaDataListConverter.cs` | SndMetaData 列表 ↔ DataSourceNode |
| `StrategyMetaDataConverter.cs` | StrategyMetaData ↔ DataSourceNode |
| `StateMachineContainerPayloadConverter.cs` | 状态机容器载荷 ↔ DataSourceNode |
| `StringDictionaryConverter.cs` | 字符串字典 ↔ DataSourceNode |
| `TypedDataConverter.cs` | TypedData ↔ DataSourceNode，携带类型元数据 |

## 转换器一览

### PrimitiveConverters

`string`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `char`, `bool`

读写模式统一：Read 调用 `DataSourceNode.AsXxx()`，Write 调用 `DataSourceNode.CreateXxx()`。

### ArrayConverters

每个基础类型的数组对应一个转换器。Read 遍历 `node.Elements` 逐个转换，Write 构建 `DataSourceNode.CreateArray()` 并填充元素。`string[]` 的元素读取与 `Read<string>` 同严格语义：null 元素抛 `InvalidOperationException`（不静默漂移为空串，损坏存档在转换层立即失败）。

### 领域转换器（DomainConverters）

| 转换器 | 处理类型 |
|------|------|
| `NodeMetaDataConverter` | 节点元数据（pairs 字典）。pair 值为 null 时抛 `InvalidOperationException`（与 `Read<string>` 严格语义一致，不静默漂移为空串资源路径） |
| `StrategyMetaDataConverter` | 策略索引列表 |
| `DataMetaDataConverter` | 实体数据（依赖 TypedDataConverter） |
| `SndMetaDataConverter` | SND 实体元数据（组合上述三个） |
| `SndMetaDataListConverter` | 实体元数据列表 |
| `BlackboardDataConverter` | 黑板全部数据字典 |
| `StringDictionaryConverter` | 字符串字典。读取时键值必须为标量字符串；null 值抛 `InvalidOperationException`（与 `Read<string>` 拒绝 null 节点的严格语义一致，杜绝 null 静默漂移为空串） |
| `StateMachineContainerPayloadConverter` | 状态机组序列化 |

**节点形状校验**：领域转换器读取时校验根节点与集合字段的形状——对象字段（`pairs`、字符串字典、黑板字典、SndMetaData）必须是 Map，数组字段（状态机 `stack`、策略索引列表、`observer_indices`、SndMetaData 列表）必须是 Array；形状错误抛 `InvalidOperationException`，避免损坏数据静默变为空集合。空 observer target 同样拒绝，不静默丢弃观察者绑定。

### TypedDataConverter

`TypedData` 是一个 struct（值类型），携带类型元数据。该转换器从 JSON 中读写 `"type"` 和 `"data"` 两个字段。读取时从 `"type"` 字段获取 CLR 类型名，通过 `TypeStringMapping` 解析为 `Type`，再用注册表中的对应转换器读取 `"data"` 字段。这是序列化系统保持类型信息的核心机制。

序列化边界：写入时 `TypedData.Data` 将内联值装箱为 `object`；读取时对已注册类型通过 `FromObject` 解除装箱还原内联值。未注册类型的值仍需经过 `object?` 装箱穿越序列化边界。

当指定的具体类型在注册表中无精确匹配时（例如存储了 `ReadOnlyDictionary<string,string>` 但只注册了 `IReadOnlyDictionary<string,string>` 的转换器），`DataSourceConverterRegistry` 会自动沿基类链和接口链回退查找。这允许以接口类型注册转换器，同时支持存储和读取其具体实现类型：`StringDictionaryConverter` 返回 `ReadOnlyDictionary<string,string>` 实例（与请求类型兼容）。若转换器返回的实例与请求类型不兼容（如请求 `SortedDictionary`），读取立即抛 `InvalidOperationException`（fail-fast，报错指明转换器与请求类型，而非晦涩的 `InvalidCastException`），避免静默返回类型漂移的值导致后续序列化失败。

## 设计决策

### 为什么每种基础类型独立一个转换器

泛型转换器（如单个 `PrimitiveConverter<T>`）需要在运行时通过反射实例化不同 T 的版本，违反零反射约束。每个具体类型显式实现避免反射，且在注册表中可静态枚举。

### 为什么数组转换器独立于基础类型转换器

数组是复合类型，其 Read/Write 需要遍历语义（foreach over `Elements`），与标量类型（直接 AsXxx）差异显著。合并会导致转换器内部出现类型分支，违反单一职责。

### 为什么领域转换器按类型拆分为独立文件

每个领域转换器独立成文件，文件即类型，检索与维护成本低；层级依赖通过明确的构造注入保持可见。

---
[↑ 回到 DataSource](../README.zh.md)
