<!-- docsync-pair: Origo.Core/Snd/Metadata/README -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# Metadata

> [↑ 回到 Snd](../README.zh.md)

## 概述

SND 实体元数据的数据模型。这些模型是纯数据容器，不包含业务逻辑，仅作为 Core 层各子系统之间传递实体信息的标准契约。用于序列化/反序列化、网络传输、存档存储。

## 包含文件

| 文件 | 职责 |
|------|------|
| `TypedData.cs` | 带类型信息的内联存储结构体（`readonly partial struct`），含 `RegisterKind()` 方法 |
| `SndMetaFluentBuilder.cs` | SND 实体元数据流式构建器，消除 `??= new DataMetaData()` 和手动 `new TypedData(...)` 样板代码 |
| `SndInlineTypesAttribute.cs` | 程序集级属性，声明哪些类型在 TypedData 中内联存储 + 可选 `StartKind` 偏移 |
| `TypedDataLayeredRegistry.cs` | 多层适配扩展点：KindResolver / FromObject / ToObject 委托链注册 |
| `DataMetaData.cs` | 实体数据字典容器 |
| `NodeMetaData.cs` | 节点元数据（逻辑名 → 资源 ID 映射） |
| `StrategyMetaData.cs` | 策略索引列表 |
| `SndMetaData.cs` | 实体元数据聚合：Name + Node + Strategy + Data |

## 模型详解

### TypedData

```csharp
public readonly partial struct TypedData : IEquatable<TypedData>
{
    internal readonly byte _kind;   // 类型判别值（0 = Null）
    internal long _inlineBits;    // 值类型内联存储（≤ 8 字节）
    internal object? _ref;        // 引用类型或大值类型兜底

    public Type DataType { get; }
    public bool IsNull { get; }
}
```

SND 系统的核心类型保留与内联存储机制。值类型（`int`、`float`、`bool` 等）直接存储在结构体的 `_inlineBits` 字段中，零装箱零堆分配。引用类型（`string`）存储在 `_ref` 字段中，无额外包装。

`TypedData` 是一个由 Source Generator 增强的 partial struct——编译时生成的代码提供每种注册类型的强类型访问器（`AsInt32()` / `TryGetSingle(out v)` 等）、显式转换运算符和泛型工厂类（`TypedDataFactory<T>`），详见 [Origo.SourceGeneration](../../../Origo.SourceGeneration/README.zh.md)。

类型判别通过 `_kind` 字段实现。判别值 `0` 为 `Null` 哨兵（`default(TypedData)`）。`DataType` 属性根据 `_kind` 从生成的查找表中返回对应的 `System.Type`。

`TypedData.RegisterKind(byte kind, Type type)` 允许适配层通过 `[ModuleInitializer]` 向全局 `KindTypeMap[256]` 中注册自己的类型。Core 层的 13 种基础类型和 GodotAdapter 层的 14 种引擎类型分别在不同 Kind 区间（1–13 和 128–141），互不冲突。

序列化边界（反序列化时）通过 `TypedDataConverter.Read()` 和 `TypedDataTypeMap.GetKindForType` + `TypedDataObjectConverter.FromObject` 内部链路构造。注册类型走内联存储；未注册类型和适配层类型走 `_ref` 兜底。`TypedDataLayeredRegistry` 提供链式回调机制，使适配层的转换逻辑能够插入 `TypedDataObjectConverter.ToObject` / `FromObject` 的 switch 分发中。

### TypedData 的访问方式与推荐用法

`TypedData` 提供以下读取方式，性能特征不同：

| 方式 | 前提 | 装箱 | 适用 |
|------|------|------|------|
| `TryGetInt32(out int)` / `TryGetString(out string)` 等生成的 `TryGetXxx` 访问器 | 编译期已知目标类型 | **零装箱** | **热路径、已知类型的读取与类型判定**（数据变更处理、每帧读写、替代 `is T` 判定）。`AsXxx()`（Home 与 Adapter 均为 `internal`）无 Kind 守卫，仅供框架内部在 switch 匹配后调用 |
| `TypedDataObjectConverter.ToObject(td)` | 类型擦除 | 值类型**装箱** | **仅框架内部冷路径**：序列化、控制台/调试输出、`ToString`。`internal`，外部代码不可访问 |

**推荐用法**：

- 取值始终用 `TryGetXxx` / `TypedDataFactory<T>.TryExtract()`，前者零装箱，且不经 `ToObject` 的 switch 分发。
- 构造始终用显式 operator（如 `(TypedData)42`）或 `TypedDataFactory<T>.Create()`。
- **不要自行做 kind 分派或 if-else 链处理不同类型的 TypedData**——类型判别逻辑已封装在生成代码和 `TypedDataObjectConverter` 内部。

### SndMetaData

聚合实体全部元数据：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Name` | `string` | 实体唯一标识名 |
| `NodeMetaData` | `NodeMetaData?` | 节点映射；后台实体可为 null |
| `StrategyMetaData` | `StrategyMetaData?` | 策略索引列表 |
| `DataMetaData` | `DataMetaData?` | 数据字典；默认为空容器 |

提供 `DeepClone()` 方法：浅复制值引用（不递归复制对象图），但新建字典/列表容器。

### DataMetaData

包装一个 `Dictionary<string, TypedData>`。每个键对应一个带类型的实体数据点。由于 `TypedData` 是值类型（struct），字典值不可为 null；缺失数据点通过不包含对应 key 来表示。

### NodeMetaData

包装 `Dictionary<string, string>`，存储逻辑节点名到资源标识符的映射。具体资源 ID 的语义由适配层定义（如 Godot 中为 `res://` 路径）。

### StrategyMetaData

包装 `List<string>`，存储该实体关联的策略索引列表（如 `["core.health", "gameplay.movement"]`）。

### SndMetaFluentBuilder

```csharp
var meta = new SndMetaFluentBuilder("Player")
    .SetInt("hp", 100)
    .SetFloat("speed", 200f)
    .SetBool("alive", true)
    .AddLifecycleStrategy("game.player_move")
    .SetNode("scene", "res://player.tscn")
    .Build();
```

流式链式 API 构建 `SndMetaData`，消除手动 `meta.DataMetaData ??= new DataMetaData()` + `meta.DataMetaData.Pairs["key"] = <TypedData 显式转换>` 的样板代码（`TypedData` 不暴露 `new TypedData(Type, object?)` 万能构造器，见下节）。

提供 `SndMetaFluentBuilder.From(SndMetaData)` 静态工厂，在 `ctx.Template.CloneTemplate` 后流式添加数据：

```csharp
var meta = SndMetaFluentBuilder.From(ctx.Template.CloneTemplate("player_template", "Player"))
    .SetInt("hp", 100)
    .Build();
```

类型化 Set 方法包括 `SetInt`、`SetFloat`、`SetDouble`、`SetLong`、`SetBool`、`SetString`、`SetBytes`。`Build()` 返回构建完成的 `SndMetaData`。

## 设计决策

### 为什么 TypedData 是值类型（readonly partial struct）

TypedData 存储实体运行时可变状态（如 `hp = 100`、`speed = 3.5f`），其中绝大多数是值类型。使用 class（引用类型）意味着每次 `SetData` 都需要堆分配一个 TypedData 值（经 `(TypedData)value` 显式转换构造），对频繁更新的游戏实体（每帧多次读写）产生显著的 GC 压力。

作为值类型，内联存储消除堆分配。`Dictionary<string, TypedData>` 的值直接嵌入字典条目，值类型在栈上或内联在集合中，不产生独立 GC 对象。

### 为什么使用 Source Generator 生成类型成员

每种 BC 基本类型需要一致的构造器、访问器、工厂方法和序列化桥接代码。手写维护 13+ 种类型的样板代码容易出错且类型间不一致。Source Generator 读取 `[assembly: SndInlineTypes]` 属性，自动为所有注册类型生成最优实现，确保零装箱读写。

### 为什么 TypedData.DataType 是 Type 而非 string

`System.Type` 允许运行时直接做类型检查和转换（如序列化时的 `ConverterRegistry.Read(type, node)`）。从 `string` 恢复 `Type` 的代价只在反序列化时支付一次。对于注册类型，`DataType` 从编译期生成的静态查找表读取（零反射）；对于未注册类型，通过 `_ref?.GetType()` 获取运行时类型。

### 为什么 DeepClone 不递归复制对象图

Entity 的 Data 值可能是复杂引用类型（如字符串、数组、嵌套对象）。全量递归深拷贝成本高且可能错误复制引擎内部引用（通过 `INodeHandle.Native` 等）。当前浅复制语义与 JSON 往返序列化行为一致——如果 JSON 序列化不会复制，DeepClone 也不复制。

### 为什么 TypedData 使用 ModuleInitializer + RegisterKind

`static TypedData()` 静态构造器只能在定义程序集中存在一个，而多适配层架构下 GodotAdapter 等下游程序集需要注册自己的类型到同一个 `KindTypeMap` 数组中。`ModuleInitializer` 允许多个程序集各自在加载时调用 `TypedData.RegisterKind()`，按依赖顺序执行（Core 先于 Adapter），实现全局类型注册的组合。

### 为什么 TypedData 不暴露 `object?` 装箱取值器

将 `int`/`float` 等值类型以 `object?` 形式返回，在 CLR 上**必然触发装箱分配**。如果 TypedData 暴露这样一个属性，无论命名为 `Data` 还是其他，它都会成为二次开发者面对 TypedData 时最自然的取值手段——因为它对所有类型"都管用"，无需了解泛型或 `TryGetXxx`。这正是 §1.4 禁止的**旁路**：一条功能完备、使用更简单的访问路径，掩盖了零装箱 `TryGetXxx` / `TypedDataFactory<T>.TryExtract()` 的存在。

TypedData 刻意不暴露装箱取值器。取值只有一条路：编译期已知类型 → `TryGetXxx` 或 `TypedDataFactory<T>.TryExtract()`，经 JIT 折叠为零开销字段读取。框架内部需要类型擦除的冷路径（序列化、控制台调试、`ToString`）通过 `internal` 的 `TypedDataObjectConverter.ToObject` 处理，外部代码无法触及。

同理，TypedData 不暴露 `new TypedData(Type, object?)` 万能构造器。构造只有一条路：`TypedDataFactory<T>.Create()` 或显式 operator（如 `(TypedData)42`），经 JIT 折叠为零开销字段写入。

---

[↑ 回到 Snd](../README.zh.md)
