# Origo.SourceGeneration

> [↑ 回到 Origo.manual](../README.md) · [↔ Core: Snd/Metadata](../Origo.Core/Snd/Metadata/README.md)

## 概述

**Origo.SourceGeneration** 是 Roslyn 增量源码生成器（`IIncrementalGenerator`），为 `TypedData` 部分结构体生成类型特化的内联存储与强类型访问器。支持 **Home/Adapter 双模式**生成，使 Core 层和下游适配层分别声明各自的类型集合，并自动协调全局 Kind 值空间。

## 包含文件

| 文件 | 职责 |
|------|------|
| `TypedDataGenerator.cs` | Roslyn `IIncrementalGenerator`：双模式代码生成器 |
| `AnalyzerReleases.Shipped.md` | 分析器发布跟踪（已发布规则，当前为空） |
| `AnalyzerReleases.Unshipped.md` | 分析器发布跟踪（未发布规则：`ORIGOSG001`、`ORIGOSG002`、`ORIGOSG003`、`ORIGOSG004`） |

## 双模式架构

Source Generator 在编译时检测当前程序集是否为 TypedData 的"宿主"程序集（即定义 `TypedData` 结构体的程序集），自动切换生成策略：

| 模式 | 适用程序集 | 生成内容 |
|------|-----------|---------|
| **Home** | Origo.Core | `partial struct TypedData` 的 KindMap、AsXxx/TryGetXxx 方法、explicit operators；`TypedDataTypeMap`、`TypedDataObjectConverter`、`TypedDataFactory<T>`；`[ModuleInitializer]` 注册 KindTypeMap |
| **Adapter** | Origo.GodotAdapter 及其他适配层 | 扩展方法（`AsXxx` / `TryGetXxx`）；`[ModuleInitializer]` 注册 KindTypeMap + KindResolver + FromObject/ToObject 转换桥接 |

### Home 模式生成内容

| 生成类别 | 生成内容 | 说明 |
|---------|---------|------|
| **KindMap** | `partial struct TypedData { internal static class KindMap { const byte Int32 = 5; ... } }` | 每种注册类型的 `const byte` 判别值，从 `StartKind` 开始编号 |
| **Kind 注册** | `TypedDataHomeKindRegistration` + `[ModuleInitializer]` | 调用 `TypedData.RegisterKind()` 填充全局 `KindTypeMap[]`，替代旧方案中的 `static TypedData()` 静态构造器 |
| **强类型构造** | `explicit operator TypedData(...)` | 每种系统类型的显式转换运算符，将值内联写入结构体字段 |
| **强类型读取** | `AsXxx()` / `TryGetXxx()` | 内部/公开访问器方法，通过 Kind 判别值直接字段读取 |
| **泛型工厂** | `TypedDataFactory<T>` | `Create(T)` / `TryExtract(TypedData, out T)`，if-else 链经 JIT 常量折叠 |
| **序列化桥接** | `TypedDataObjectConverter` | `ToObject` / `FromObject`，switch 分发 + `TypedDataLayeredRegistry` fallback 链 |
| **类型映射** | `TypedDataTypeMap` | `GetKindForType(Type)`，if-else 链 + `TypedDataLayeredRegistry.ResolveKind` fallback |

### Adapter 模式生成内容

| 生成类别 | 生成内容 | 说明 |
|---------|---------|------|
| **扩展方法** | `TypedDataLayeredExtensions` 静态类 | `this TypedData` 扩展方法：`TryGetVector3`、`AsVector3` 等，按类型体积选择内联或 `_ref` 读取路径 |
| **Kind 注册** | `TypedDataAdapterKindRegistration` + `[ModuleInitializer]` | 调用 `TypedData.RegisterKind(startKind + i, typeof(T))` |
| **转换桥接** | `TypedDataAdapterConverterRegistration` + `[ModuleInitializer]` | 调用 `TypedDataLayeredRegistry.RegisterFromObjectFallback` / `RegisterToObjectFallback` |
| **类型解析** | `TypedDataAdapterTypeMapRegistration` + `[ModuleInitializer]` | 调用 `TypedDataLayeredRegistry.RegisterKindResolver`，提供 `Type → kind` 的 if-else 链 |

### Kind 值分段

Kind 值是一个 `byte`，由 `SndInlineTypesAttribute` 的 `StartKind` 参数控制每层的起始值：

| 层 | StartKind | Kind 范围 | 类型数 |
|----|-----------|----------|--------|
| Core | 1（默认） | 0–13 | 13 种 BCL 基础类型 |
| GodotAdapter | 128 | 128–141 | 14 种 Godot 引擎类型 |
| 预留（未来适配器） | 192 | 192–254 | — |
| Fallback | — | `TypedData.UnregisteredKind` | 未注册类型兜底 |

### 类型内联策略

存储只有两条路径：宿主程序集的系统基础值类型走 `_inlineBits`，其余一律走 `_ref`。

| 类型 | 存储方式 | 说明 |
|------|---------|------|
| 宿主（Origo.Core）程序集声明的系统基础值类型（`byte`/`sbyte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`/`float`/`double`/`bool`/`char`） | `_inlineBits : long` 字段内联 | 零堆分配，零装箱；内联仅限宿主程序集 |
| 引用类型（`string`） | `_ref : object?` 字段存储 | 内置 KindMap 兜底 |
| 适配器注册类型（非系统值类型） | `_ref : object?` 字段存储 | 非系统值类型体积不可在编译期确定，走 `_ref` 兜底 |
| 未注册类型 | `_ref : object?` 兜底 | Kind=`TypedData.UnregisteredKind`，通过 `FromObject(Type, object?)` 构造 |

> 判别值 `0` 固定为 `Null` 哨兵值（`default(TypedData)`）。内联候选由 `SpecialType` 白名单精确判定（上表第一行枚举的 12 种系统基础类型），不依赖类型显示名匹配。

## 类型校验与诊断

生成器在生成前对每个注册类型做存储模型校验，违反边界时报告为编译错误（fail-fast），而非生成静默返回 `0`/`default` 的访问器。违规类型从生成中剔除，被报告的错误使构建失败。

| 诊断 ID | 严重级别 | 触发条件 |
|---------|---------|---------|
| `ORIGOSG001` | Error | 系统基础类型在非宿主（适配层）程序集的 `SndInlineTypes` 组中注册。内联基础类型由 Origo.Core 独占，适配层只能注册引用类型或非系统值类型（走 `_ref`）。 |
| `ORIGOSG002` | Error | 宿主程序集中注册了无法内联且不受支持的值类型（如 `decimal` 或自定义结构体）。宿主程序集仅允许注册受支持的系统基础类型与引用类型。 |
| `ORIGOSG003` | Error | 注册类型的 Kind 值（`startKind` + 组内位置）落在 `byte` 有效范围 `[1, 255]` 之外。包含 Kind 越界后会回绕到某个已占用值、从而与其它类型静默冲突的情形。 |
| `ORIGOSG004` | Error | 多个 `SndInlineTypes` 组的 `startKind` 区间重叠，导致同一个 Kind 字节被分配给多个不同类型。每个内联类型必须映射到唯一的 Kind。 |

## 注册机制

```csharp
// Core 层（默认 StartKind=1）
[assembly: SndInlineTypes(
    typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
    typeof(int), typeof(uint), typeof(long), typeof(ulong),
    typeof(float), typeof(double), typeof(bool), typeof(char), typeof(string)
)]

// 适配层（指定 StartKind）
[assembly: SndInlineTypes(startKind: 128,
    typeof(Vector2), typeof(Vector2I),
    typeof(Vector3), typeof(Vector3I), typeof(Vector4),
    typeof(Quaternion), typeof(Basis),
    typeof(Transform2D), typeof(Transform3D),
    typeof(Color), typeof(Rect2), typeof(Rect2I),
    typeof(Aabb), typeof(Plane)
)]
```

`SndInlineTypesAttribute` 定义在 `Origo.Core.Snd.Metadata` 命名空间。新增 `StartKind` 参数（默认 `1`）控制 Kind 起始偏移。

## 多层运行时扩展

### TypedDataLayeredRegistry

位于 `Origo.Core.Snd.Metadata`，提供链式回调注册机制，允许多个适配层并发贡献自己的类型映射：

| 注册方法 | 注册的委托形态 | 被调用的位置 |
|---------|--------------|------------|
| `RegisterKindResolver(Func<Type, byte>)` | `Type → kind`，返回 0 表示不处理 | `TypedDataTypeMap.GetKindForType` |
| `RegisterFromObjectFallback(Func<byte, object, (long, object?)?>)` | `(kind, value) → (inlineBits, refValue)?`，返回 null 表示不处理 | `TypedDataObjectConverter.FromObject` |
| `RegisterToObjectFallback(Func<TypedData, object?>)` | `TypedData → object?`，返回 null 表示不处理 | `TypedDataObjectConverter.ToObject` |

### TypedData.RegisterKind

`TypedData` 新增 `internal static void RegisterKind(byte kind, Type type)` 方法，允许外部程序集（通过 `InternalsVisibleTo`）向全局 `KindTypeMap[256]` 数组中写入 kind → Type 映射。各层的 `[ModuleInitializer]` 按程序集加载顺序调用此方法。

## 设计决策

### 为什么使用 Home/Adapter 双模式而非单一集中生成

单一集中生成要求 SG 在编译 Core 时扫描所有下游适配层程序集，但 Core 先于适配层编译 — 此时适配层的元数据尚不存在。双模式让每层独立编译、独立生成，通过 `TypedDataLayeredRegistry` + `ModuleInitializer` 在运行时动态组装。

### 为什么用 ModuleInitializer 替代 static TypedData()

`static TypedData()` 只能存在一个，适配层无法追加 `KindTypeMap` 条目。`ModuleInitializer` 允许多个程序集各自注册自己的 Kind 映射，按程序集加载顺序执行（Core 先于 GodotAdapter）。

### 为什么适配层类型走 _ref 而非 _inlineBits

适配层类型（如 Godot 的 `Vector3`、`Color`）是外部定义的非系统值类型，其实际字节数在源码生成时不可靠推断。安全策略：适配层注册的类型统一走 `_ref` 路径，Kind 判别值仍保证 `DataType` 查找和 `TryGet` 的快速分发。

### 为什么对越界注册报错而非静默降级

存储模型只有内联（宿主系统基础类型）与 `_ref`（其余）两条路径。若把"系统基础类型注册到适配层"或"宿主注册无法内联的值类型"等越界情形静默接受，生成器只能产出返回 `0`/`default` 的访问器，导致数据在往返中悄然损坏。遵循框架的"显式失败优先"约束，生成器对这类越界注册报告 `ORIGOSG001`/`ORIGOSG002` 编译错误并将该类型排除，使问题在构建期暴露而非运行期。

### 为什么在编译期校验 Kind 空间的范围与唯一性

Kind 是 `byte`，运行时 `TypedData.RegisterKind` 直接写入 `KindTypeMap[kind]` 而不校验重复——多个类型映射到同一 Kind 会静默互相覆盖，造成 TypedData 存取错乱。Kind 由 `startKind` 加组内位置算出，若 `startKind` 偏大或类型过多，Kind 会越过 255 并回绕到某个已占用的小值，与既有类型冲突。生成器因此在编译期强制两条不变量：Kind 必须落在 `[1, 255]`（`ORIGOSG003`，按真实值判定，不依赖 `byte` 截断结果），且每个 Kind 必须唯一映射（`ORIGOSG004`，检测重叠的 `startKind` 区间）。违规类型被剔除并报构建错误，使 Kind 冲突在构建期暴露而非运行期损坏存档。

---

[↑ 回到 Origo.manual](../README.md)
