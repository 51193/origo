<!-- docsync-pair: Origo.SourceGeneration/pipeline -->
<!-- docsync-revision: 6 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# TypedData 编译期优化全链路解析

> [↑ 回到 Origo.SourceGeneration](README.zh.md) ·
> [↔ 基准数据](../benchmarks/baseline.zh.md) ·
> [↔ TypedData 文档](../Origo.Core/Snd/Metadata/README.zh.md)

## 概述

本文档是对 TypedData 源码生成器的**为什么、怎么做的、为什么有效**的系统性解析。文档从头梳理问题→方案→链路→性能数据→边界限制的完整推理链，供后续维护与扩展研发参考。

**目标读者**：需要在 Origo 框架上进一步扩展 TypedData 能力、适配新运行时、或理解性能权衡细节的开发者。阅读本文前，建议先通读 [Origo.SourceGeneration README](README.zh.md) 了解双模式架构和生成内容概览。

---

## 1. 问题起点：通用数据字典的性能陷阱

### 1.1 业务需求

游戏实体（Entity）携带若干属性：生命值 `int`、移动速度 `float`、名字 `string`、坐标 `Vector3` 等等。框架需要一套通用机制来容纳这些任意类型的数据。直接方案是：

```csharp
Dictionary<string, object> _data;
_data["hp"] = 100;      // int 装箱
_data["speed"] = 3.5f;  // float 装箱
```

### 1.2 装箱的成本

C# 的值类型（`int`、`float`、`bool` 等）不是堆对象，不能直接存入 `object` 槽。赋值给 `object` 时，CLR 必须做**装箱**：

1. 在托管堆上分配一个新对象（对 `int` 而言通常是 24 字节的堆对象，内含 syncblk + method table pointer + 4 字节数据 + padding）
2. 把值类型的内容拷贝进堆对象
3. 在 `Dictionary` 的 entries 数组中存一个指向该对象的引用

读取时的逆操作即为**拆箱**：运行时类型检查 + 堆到栈/寄存器的拷贝。

### 1.3 对游戏引擎的影响

| 指标 | boxing 方案 |
|------|------------|
| 每次 `int` 写入 | 1 次堆分配（~24 字节）+ GC 对象跟踪 |
| 200 万次 `int` 写入 | ~107 MB 堆分配 |
| 每次 GC 触发 | 扫描所有存活装箱对象，STW（Stop-The-World） |
| 每帧数千次读写 | GC 延迟累积 → 帧率抖动 |

在 60 FPS 游戏里，每帧 16ms 预算中 GC 占用 2-5ms 就足以导致掉帧。离线批处理也许不在乎，但对实时帧循环而言，必须消除热路径上的装箱。

---

## 2. 前置条件：为什么只有 C# 能这么做

TypedData 方案依赖三个 C# / .NET 独有的机制。如果从 Java 或其它运行时视角看这套方案会觉得不可能。这里先解释清楚前提。

### 2.1 泛型不擦除类型

C# 泛型编译后在 IL 和运行时元数据中完整保留类型参数信息。`List<int>` 和 `List<float>` 是不同的类型。关键表达式 `typeof(T)` 不是编译期语法糖——它在运行时是真实的 `System.Type` 实例：

```csharp
static byte LookupKind<T>()
{
    // 这在 C# 中是合法的，因为 T 没有擦除
    if (typeof(T) == typeof(int))  return 5;
    if (typeof(T) == typeof(float)) return 9;
    return 0;
}
```

**Java 对比**：泛型编译后擦除为 `Object`，字节码中根本不存在 `T.class` 表达式。

### 2.2 值类型泛型产生独立机器码

当 CLR 的 JIT 编译器遇到 `TypedDataFactory<int>` 和 `TypedDataFactory<float>` 时，它们生成**两份完全独立的本地机器码**，因为 `int`（4 字节）和 `float`（IEEE 754 单精度）的寄存器布局、读写指令序列完全不同。

这与 C++ 模板的代码膨胀机制等价（`std::vector<int>` 和 `std::vector<float>` 是两套独立汇编）。引用类型泛型（如 `TypedDataFactory<string>`）则共享同一份代码，因为所有引用都是 8 字节指针。

### 2.3 JIT 常量折叠 + 死代码消除

在编译 `TypedDataFactory<int>` 时，JIT 遇到：

```csharp
if (typeof(T) == typeof(byte))   // typeof(int) == typeof(byte) → false
    ...
if (typeof(T) == typeof(int))    // typeof(int) == typeof(int)  → true
    ...
```

`typeof(T)` 对具体 `T = int` 是 JIT 期常量。JIT 在编译时就能判定 `typeof(int) == typeof(byte)` 恒为 `false`，直接把该分支从生成的机器码中剔除。只有 `typeof(T) == typeof(int)` 为 `true` 的分支会保留。

**这就是性能的来源**：源码中看起来是一个巨大的 if-else 链，但 JIT 之后的机器码只包含目标类型的那一个分支。

### 2.4 Roslyn Source Generator

C# 编译器允许插入源码生成器——在编译过程中，生成器可以读写当前程序集的全部元数据（类型、属性、方法签名等），然后动态产出新的 `.cs` 源文件追加到编译中。生成出来的代码和被手写出来的代码完全等价。

---

## 3. TypedData 结构体设计

```csharp
public readonly partial struct TypedData : IEquatable<TypedData>
{
    internal readonly byte _kind;        // 类型标签
    internal readonly long _inlineBits;  // ≤8 字节的值类型裸存储
    internal readonly object? _ref;      // 引用类型或大型结构体
}
```

物理布局（24 字节，含 7 字节对齐填充）：

```
Offset  0  [_kind]       1 byte
Offset  1  [padding]     7 bytes (对齐到 long 边界)
Offset  8  [_inlineBits] 8 bytes
Offset 16  [_ref]        8 bytes (托管引用指针)
```

### 3.1 字段分工

| 字段 | 装什么 | 如何用 |
|------|--------|--------|
| `_kind` | `0` = null, `1-254` = 已注册类型, `255` = 未注册 | 所有类型判别和 `switch` 分发都基于它 |
| `_inlineBits` | `int`, `float`, `double`, `bool`, `long` 等 ≤8 字节的基础类型 | 直接类型强转读出；`float`/`double` 通过 `BitConverter` 重解释位模式 |
| `_ref` | `string`、Godot `Vector3`、未注册的任意类型 | 托管引用，无额外包装 |

### 3.2 为什么是 24 字节

`_inlineBits`（`long` 字段）和 `_ref`（托管引用）不能共享内存——GC 必须独立扫描每一个托管引用指针来确定对象存活状态。如果把引用嵌入 `long` 的高 8 字节，GC 无法区分这是一个数字还是一个指针。所以三者必须顺序排列：

```
byte (1B) + padding (7B) + long (8B) + object? (8B) = 24B
```

能否压到 16 字节？必须牺牲 `long`/`double` 的 8 字节内联能力，改为走 `_ref` 路径（对值类型而言等于每次都装回堆）。经评估这对性能是纯负收益，已放弃。

---

## 4. 全链路拆解

### 4.1 注册阶段：声明类型集合

框架在程序集级应用 `[assembly: SndInlineTypes(...)]`：

```csharp
// Origo.Core 程序集 — StartKind 默认 1
[assembly: SndInlineTypes(
    typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
    typeof(int), typeof(uint), typeof(long), typeof(ulong),
    typeof(float), typeof(double), typeof(bool), typeof(char),
    typeof(string)
)]

// Origo.GodotAdapter 程序集 — StartKind = 128
[assembly: SndInlineTypes(startKind: 128,
    typeof(Vector2), typeof(Vector2I), typeof(Vector3),
    typeof(Vector3I), typeof(Vector4), typeof(Quaternion),
    typeof(Basis), typeof(Transform2D), typeof(Transform3D),
    typeof(Color), typeof(Rect2), typeof(Rect2I),
    typeof(Aabb), typeof(Plane)
)]
```

Kind 分配规则：

| 层 | StartKind | Kind 范围 | 类型数 |
|----|-----------|----------|--------|
| Core | 1 | 1–13 | 13 种 BCL 基础类型 |
| GodotAdapter | 128 | 128–141 | 14 种 Godot 引擎类型 |
| 预留（未来适配器） | 192 | 192–254 | — |

编译期校验（fail-fast）：

| 诊断 ID | 触发条件 |
|---------|---------|
| `ORIGOSG001` | 系统基础类型被注册到非宿主（适配器）程序集 |
| `ORIGOSG002` | 宿主程序集注册了无法内联的值类型（如 `decimal`） |
| `ORIGOSG003` | Kind 越界（不在 `[1, 254]` 内） |
| `ORIGOSG004` | Kind 区间重叠，多类型映射到同一 Kind |
| `ORIGOSG005` | 生成标识符（KindName）冲突：不同命名空间同名类型、名称折叠的泛型实例、或同一类型以不同 Kind 值重复注册（相同 Kind 的重复注册属幂等，被静默去重） |

这些诊断在编译期报告为 Error，使构建失败。Kind 冲突或越界之类的问题**不会进入运行时**。

#### 为什么在编译期校验 Kind

运行时 `TypedData.RegisterKind` 已能检测冲突——同一 Kind 注册不同类型时抛 `InvalidOperationException`（相同类型重复注册幂等）。但 Kind 冲突若依赖运行时才暴露，损坏风险已进入运行期。编译期强制检查（范围、数值唯一性、生成标识符唯一性），Kind 冲突在构建时就报错。

---

### 4.2 链接阶段：源码生成器产出代码

Source Generator 通过 Roslyn `IIncrementalGenerator` 管线在编译期被调用。它扫描程序集的所有 `[assembly: SndInlineTypes]` 属性，然后生成一份 `TypedData.g.cs` 源文件追加到本次编译。

生成内容根据**当前进程序集是否为 TypedData 的宿主程序集**分为两套：

#### Home 模式（Origo.Core）

| 生成类别 | 产物 |
|---------|------|
| **KindMap** | `partial struct TypedData { KindMap { const byte Int32 = 5; ... } }` |
| **内联访问器** | `AsInt32()` / `TryGetInt32(out v)` 等，直接操作 `_inlineBits` |
| **显式转换** | `explicit operator TypedData(int value)` |
| **泛型工厂** | `TypedDataFactory<T>`：含 `Create`（T → TypedData）和 `TryExtract`（TypedData → T） |
| **类型映射** | `TypedDataTypeMap.GetKindForType(Type)` |
| **对象转换器** | `TypedDataObjectConverter`：`ToObject` / `FromObject` |
| **Kind 注册** | `[ModuleInitializer]` 调用 `TypedData.RegisterKind()` |

#### Adapter 模式（Origo.GodotAdapter 等）

| 生成类别 | 产物 |
|---------|------|
| **扩展方法** | `td.TryGetVector3(out Vector3 v)` 等（走 `_ref` 路径） |
| **Kind 注册** | `[ModuleInitializer]` 调用 `TypedData.RegisterKind()` |
| **转换回退** | 向 `TypedDataLayeredRegistry` 注册 `FromObject`/`ToObject` 回退委托 |
| **类型解析** | 向 `TypedDataLayeredRegistry` 注册 `Type → kind` 的 if-else 链 |

#### 源码生成器代码位置

生成器源码位于 `Origo.SourceGeneration/` 下，拆分为 5 个 partial 文件（共 ~1000 行）：`TypedDataGenerator.cs`（管线与输入提取）、`TypedDataGenerator.HomeGeneration.cs`（Home 程序集生成）、`TypedDataGenerator.AdapterGeneration.cs`（适配层生成）、`TypedDataGenerator.FactoryGeneration.cs`（`TypedDataFactory<T>` 分支生成）、`TypedDataGenerator.Diagnostics.cs`（诊断定义）。核心的 `GenerateTypedDataFactory`（`FactoryGeneration.cs`）遍历所有注册类型并逐条生成 `typeof(T) == typeof(...)` 风格的分支。

---

### 4.3 写入链路：T → TypedData

以 `entity.SetData("hp", 100)` 为例：

```
调用方 → SndDataManager.SetData<int>("hp", 100)
       → TypedDataFactory<int>.Create(100)
```

**JIT 前**的 `TypedDataFactory<int>.Create` 代码：

```csharp
public static TypedData Create(T value)
{
    if (typeof(T) == typeof(byte))
    {
        byte local = Unsafe.As<T, byte>(ref value);
        return new TypedData(1, local, null);
    }
    if (typeof(T) == typeof(sbyte))
    {
        sbyte local = Unsafe.As<T, sbyte>(ref value);
        return new TypedData(2, local, null);
    }
    // ... 9 个类似分支 ...
    if (typeof(T) == typeof(int))
    {
        int local = Unsafe.As<T, int>(ref value);       // JIT: typeof(T)==typeof(int) → true
        return new TypedData(5, local, null);            // _kind=5, _inlineBits=100, _ref=null
    }
    // ... uint, long, ulong, float, double, bool, char ...
    if (typeof(T) == typeof(string))
    {
        return new TypedData(13, 0, value);
    }
    // fallback: 未注册类型
    var kind = TypedDataTypeMap.GetKindForType(typeof(T));
    if (kind != 0)
    {
        var result = TypedDataObjectConverter.FromObject(kind, value!);
        return new TypedData(kind, result.inlineBits, result.refValue);
    }
    return new TypedData(TypedData.UnregisteredKind, 0, value);
}
```

**JIT 后**实际执行的机器码（对 `TypedDataFactory<int>`）：

```
存 _kind=5, 存 _inlineBits=100, 存 _ref=null
```

#### 为什么快

| 维度 | 生成（TypedData struct） | 装箱（OldTypedData class） |
|------|------------------------|---------------------------|
| 堆分配 | 0（值类型嵌入字典条目数组） | 每写入一个 `int` → 1 次新生代堆分配 |
| 写入 | 两道 `stfld`（存 _kind + _inlineBits） | `newobj` + `stfld` + GC 记账 |
| GC 压力 | 0（无独立对象） | 每次新生代 GC 都要扫描所有 box |
| 赋给字典的 `object` 键 | 不经过——`TypedData` 是 struct，直接嵌入 | 装箱后的引用存入字典 |

**基准数据**（200 万次写入）：

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 分配 (生成/装箱) |
|------|--------------:|--------------:|:----:|----------------|
| Int32 | 908 | 69.6 | **13.0x** | 0 B / 106.81 MB |
| Int64 | 893 | 70.9 | **12.6x** | 0 B / 106.81 MB |
| Single | 866 | 71.4 | **12.1x** | 0 B / 106.81 MB |
| Double | 881 | 72.3 | **12.2x** | 0 B / 106.81 MB |
| Boolean | 891 | 71.3 | **12.5x** | 0 B / 106.81 MB |
| Char | 910 | 71.6 | **12.7x** | 0 B / 106.81 MB |
| String | 563 | 110 | **5.1x** | 0 B / 61.04 MB |

值类型写入 **12–13x 倍吞吐**，且 **零字节分配**。String 写入也 5.1x（引用类型不需要装箱到另一个引用类型对象，但仍需构造 `OldTypedData` 包装类）。

---

### 4.4 读取链路：TypedData → T

#### TryGetXxx 路径（热路径）

```csharp
// 调用方：
entity.TryGetData<int>("hp", out int hp);
// → SndDataManager.TryGetData<int>
// → _data.TryGetValue("hp", out TypedData td)
// → TypedDataFactory<int>.TryExtract(td, out hp)
```

**JIT 前**的 `TryExtract`：

```csharp
public static bool TryExtract(TypedData source, out T value)
{
    if (typeof(T) == typeof(byte) && source._kind == 1)
    {
        byte local = (byte)source._inlineBits;
        value = Unsafe.As<byte, T>(ref local);
        return true;
    }
    // ... 10 个类似分支 ...
    if (typeof(T) == typeof(int) && source._kind == 5)     // ← JIT 只保留这行
    {
        int local = (int)source._inlineBits;                 // 直接字段读取
        value = Unsafe.As<int, T>(ref local);                // no-op（T 本就是 int）
        return true;
    }
    // ... float, double, bool, char, string ...
    if (typeof(T) == typeof(string) && source._kind == 13)
    {
        if (source._ref is T t) { value = t; return true; }
    }
    // fallback: 注册但非内联类型 → ToObject + cast
    if (source._kind != 0 && source._kind != TypedData.UnregisteredKind)
    {
        var obj = TypedDataObjectConverter.ToObject(source);
        if (obj is T t1) { value = t1; return true; }
    }
    // last resort: 未注册类型
    if (source._ref is T t2) { value = t2; return true; }
    value = default!;
    return false;
}
```

**JIT 后**（对 `TypedDataFactory<int>`）：

```csharp
if (source._kind == 5) { value = (int)source._inlineBits; return true; }
// fallback ...
```

两条指令：字节比较 + 寄存器读出。

#### 为什么单读近乎持平（≤ 1.10x）

**基准数据**（1000 万次读取，两侧均为 0 字节分配）：

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 |
|------|--------------:|--------------:|:----:|
| Int32 | 550 | 581 | 1.06x (装箱略快) |
| Int64 | 551 | 580 | 1.06x (装箱略快) |
| Single | 551 | 568 | 1.03x (近乎手) |
| Double | 560 | 584 | 1.04x (装箱略快) |
| Boolean | 530 | 583 | 1.10x (装箱略快) |
| Char | 545 | 584 | 1.07x (装箱略快) |
| String (IsString) | 586 | 576 | 1.02x (生成略快) |

分析：装箱侧的代码路径也是：取 `object` 引用 → 检查类型元数据（method table pointer）→ 拆箱拷贝。两边要做的工作其实类似——都是类型判别 + 字段读出。生成侧略慢 ~10% 的原因不在指令数量，而在结构体大小导致的缓存亲和性差异（见第 5 章）。

#### 混合分发为什么反超

```csharp
// 生成：每个 TryGetXxx 检查 _kind + 读字段，零装箱
td.TryGetInt32(out _);
td.TryGetSingle(out _);
td.TryGetBoolean(out _);
td.TryGetString(out _);
td.TryGetDouble(out _);

// 装箱：.Data 每次走 ToObject → switch → 值类型必须装箱 → 再 is T 拆箱
data is int;
data is float;
data is bool;
data is string;
data is double;
```

**基准**：混合分发生成 1.54x 快于装箱（~1250 vs ~812 Mops/s），两侧 0 字节分配（生成侧确实零分配，装箱侧的分配被循环复用掩盖，但因反复装箱逻辑开销仍然更大）。

#### TryGetString 的特殊处理

```csharp
// 生成：由 _kind == String 守卫，用 Unsafe.As<string> 而非 (string)_ref
if (_kind == KindMap.String) { value = Unsafe.As<string>(_ref)!; return true; }
```

为什么不用 `(string)_ref`（castclass）：`_kind == String` 已证明 `_ref` 就是 `string` 实例，castclass 是冗余的。更关键的是，castclass 可能抛异常——这会阻断 JIT 对「结果被丢弃的 TryGetString 调用」的消除与外提优化。在观察者通知路径上，去掉 castclass 后生成侧与装箱 `is string` 持平（~5390 vs ~5170 Mops/s）。

---

### 4.5 对象边界路径：ToObject / FromObject / Data 属性

`TypedDataObjectConverter.ToObject` 服务于编译期不知道类型的冷路径（序列化、控制台输出、`ToString`、`Data` 属性读取）：

```csharp
public static object? ToObject(TypedData td)
{
    switch (td._kind)
    {
        case 0:  return null;
        case 1:  return td.AsByte();          // _inlineBits → byte → 装箱为 object
        case 5:  return td.AsInt32();         // _inlineBits → int → 装箱为 object
        case 13: return td._ref;              // 引用类型直接返回
        // ... adapter 的 case 由 TypedDataLayeredRegistry 提供 ...
    }
    var obj = TypedDataLayeredRegistry.ResolveToObject(td);
    if (obj is not null) return obj;
    return td._ref;
}
```

值类型通过 `ToObject` 读时**不可避免地要装箱**——因为返回值类型是 `object`。

#### `TypedDataObjectConverter.ToObject` 迭代的性能取舍

`TypedDataObjectConverter.ToObject` 是框架内部处理类型擦除场景的设施。它接收 `TypedData` 并返回 `object?`——对值类型必然装箱。TypedData 刻意不暴露任何 public 的装箱取值属性，以避免形成 §1.4 禁止的旁路。该路径仅通过 `internal` 访问。以下是其性能数据：

**基准**（2048 键异构字典 `ToObject` 迭代，80% 为值类型）：

| 指标 | 生成侧 | 装箱侧 |
|------|-------|-------|
| 吞吐 | ~404 Mops/s | ~2800 Mops/s |
| 倍率 | **0.14x**（装箱 6.9x 快） | — |
| 分配 | 37.49 MB | 0 B |

这是类型擦除路径固有的开销。**但它不是真实热路径**：

- 框架内部热/温路径（数据变更信号处理、加载校验、实体观察）一律用零装箱 `TryGetXxx`
- `TypedDataObjectConverter.ToObject` 仅用于框架内部的序列化和冷路径，不暴露给外部调用方
- **即使是最坏情况也不会在热路径出现**——该路径仅对内部序列化和调试代码开放

---

### 4.6 适配层扩展链：从编译到运行时

#### 为什么需要双层架构

Origo.Core 定义 `TypedData`，Origo.GodotAdapter 是另一个独立的 DLL，在 Core **编译完之后**才编译。单一的集中代码生成无法工作——生成 Core 时适配层的元数据还不存在。

解法：让每层独立编译、独立生成自己的注册代码，通过 `ModuleInitializer` 在运行时组装。

#### 运行时组装流程

```
程序启动
  ├─ Origo.Core.dll 加载
  │    └─ ModuleInitializer 运行
  │         ├─ RegisterKind(1, typeof(byte))
  │         ├─ RegisterKind(5, typeof(int))
  │         └─ ... 13 个基础类型 ...
  │
  └─ Origo.GodotAdapter.dll 加载（依赖 Core，因此必定在其后）
       └─ ModuleInitializer 运行
            ├─ RegisterKind(128, typeof(Vector2))
            ├─ RegisterKind(130, typeof(Vector3))
            └─ ... 14 个 Godot 类型 ...
            ├─ RegisterKindResolver(if-else chain for Godot types)
            ├─ RegisterFromObjectFallback(switch for Godot types)
            └─ RegisterToObjectFallback(switch for Godot types)
```

#### 适配层读取解析流程

```csharp
// 当调用 TypedDataFactory<Vector3>.TryExtract(td, out v3)
// 1. 13 个 typeof(T)==... 分支全不匹配（T 是 Vector3，不在 Core 的注册列表中）
// 2. 走到 fallback: TypedDataObjectConverter.ToObject(td)
// 3. switch(td._kind): case 0-13 不命中（kind 不是 130）
// 4. TypedDataLayeredRegistry.ResolveToObject(td)
//    → 遍历委托链
//    → Adapter 的 ToObject 回调命中 case 130 → return td._ref
//    → Vector3 从 _ref 取出
// 5. obj is T t1 → true → 返回
```

#### TypedDataLayeredRegistry

```csharp
internal static class TypedDataLayeredRegistry
{
    // 多层委托链，每层注册一个回调
    private static Func<Type, byte>? _kindResolverChain;
    private static Func<byte, object, (long, object?)?>? _fromObjectChain;
    private static Func<TypedData, object?>? _toObjectChain;

    // ResolveXxx 遍历 GetInvocationList() 调用链，返回第一个非 null/0 的结果
}
```

委托组装的顺序即 `ModuleInitializer` 的执行顺序（Core 先于 Adapter，符合 DLL 加载依赖方向）。

#### 为什么适配层类型不能内联

适配层注册的 Godot 类型（`Vector3` 12 字节、`Color` 16 字节等）超过 `_inlineBits` 的 8 字节容量，且 Source Generator 是在编译 Core 时运行的——此时无法可靠推断外部引擎类型在不同平台上的字节布局。安全策略：适配层类型统一走 `_ref`，Kind 字节仍提供零开销分发（避免 `is T` 虚检查）。

---

## 5. 缓存效应与结构限制

### 5.1 结构体大小 vs 缓存行

CPU 缓存线（cache line）为 64 字节。`TypedData` 为 24 字节，一个缓存线只装 2.66 个元素；而 `Dictionary<string, object>` 的 entries 数组中的值槽为 8 字节引用，一个缓存线装 8 个。

这意味着数组遍历时 `TypedData` 的缓存命中率更低。这就是**单读 1.06-1.10x 和 DictLookup 1.31-1.38x 的结构性原因**——不是指令数问题，是缓存亲和性。

### 5.2 内部字段偏移

```
offset 0:  _kind (1B)
offset 1:  padding (7B)
offset 8:  _inlineBits (8B)
offset 16: _ref (8B)
```

`_ref` 在偏移 16，步长 24 字节循环中更容易横跨两个缓存线边界（偏移 16 + 步长 24 × N → 频繁触及 offset 40、64 等边界），这进一步拉低了 `TryGetString` 的数组遍历性能（~1.40x，方差极大，约跑 391–485 Mops/s）。

### 5.3 为什么不能压到 16 字节

`long` 和托管引用 `object?` 不能共享内存——GC 必须能独立扫描每一个引用指针来确定对象存活状态。如果让 `_ref` 和 `_inlineBits` 的高 8 字节重叠，GC 就无法区分那是一个数字还是一个指针。

所以最小安全布局就是：`byte (1B) + padding (7B) + long (8B) + reference (8B) = 24B`。经评估，为了把 1.10x 的边际差距变成平手而牺牲 `double`/`long` 的满 64 位内联能力或引入额外分支，是纯负收益。

---

## 6. 性能全景

> 数据来自 [benchmarks/baseline.zh.md](../benchmarks/baseline.zh.md)，采样环境：AMD Ryzen 7 9700X / .NET 10.0.9

### 写入（生成 12-13x，0 字节分配）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 分配 |
|------|--------------:|--------------:|:----:|------|
| Int32 | 908 | 69.6 | 13.0x | 0 B / 107 MB |
| Single | 866 | 71.4 | 12.1x | 0 B / 107 MB |
| Boolean | 891 | 71.3 | 12.5x | 0 B / 107 MB |
| String | 563 | 110 | 5.1x | 0 B / 61 MB |

### 读取（单读近持平，多类型分发反超）

| 场景 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 胜方 |
|------|--------------:|--------------:|:----:|:----:|
| Int32 单读 | 550 | 581 | 1.06x | 装箱 |
| 混合分派（5 类型） | ~1250 | ~812 | **1.54x** | 生成 |
| 强转链（4 类型） | 351 | 251 | **1.40x** | 生成 |
| 观察者通知 | ~5390 | ~5170 | ~1.0x | 持平 |
| String IsString | 586 | 576 | 1.02x | 生成 |

### 字典构造 + 插入（生成 2.9x，少 3 MB 分配）

| 类型 | 生成 (Mops/s) | 装箱 (Mops/s) | 倍率 | 分配 (生成/装箱) |
|------|--------------:|--------------:|:----:|----------------|
| String | 193 | 144 | 1.34x | 23.53 MB / 14.97 MB |
| Int32 | 218 | 75.9 | **2.88x** | 23.53 MB / 26.42 MB |
| Boolean | 226 | 76.7 | **2.94x** | 23.53 MB / 26.42 MB |

> String 插入生成侧分配反而略多：`Dictionary<string, TypedData>` 的 entries 数组内嵌 24 字节 struct，比 `Dictionary<string, object>` 的 8 字节引用占用更大的后备数组。对值类型插入来说 2.9x 的吞吐收益完全覆盖此开销。

### 结论

- **写入** 12-13x 快于装箱，零堆分配。这是生成方案最大的收益。
- **读取** 不劣于装箱（≤ 1.10x），混合分发反超（1.54x），强转链反超（1.40x）。
- 唯一输项（`TypedDataObjectConverter.ToObject` 迭代 0.14x）是类型擦除路径中不可避免的装箱开销，仅限框架内部冷路径调用，不存在 public 入口。

---

## 7. 如何扩展

### 7.1 新增系统基础类型（在 Core 层注册）

如果框架需要支持新类型（如 C# `nint`、未来 BCL 新增的 ≤8 字节值类型）：

1. 在 `Origo.Core/AssemblyAttributes.cs` 的 `[SndInlineTypes]` 数组中追加 `typeof(...)`
2. 在 `TypedDataGenerator.cs` 的 `IsInlineCandidate` 和 `GenerateKindName` 中追加对应的 `SpecialType` 匹配
3. 如果该类型有特殊的读/写逻辑（如 `float` 的 `BitConverter`），在 `TypedDataGenerator.cs` 的 `InlineTypeExprs`（`Pack` / `Unpack` / `FromObject`）中追加处理——该 helper 是所有位模式表达式的单一来源，Home 访问器、转换与工厂生成共用
4. 运行 `bash scripts/test.sh` 通过全量测试 + 覆盖率门禁
5. 更新 Changelog 和本文档中的性能数据表

### 7.2 新增适配层类型（在新适配器程序集中注册）

1. 在新程序集中添加 `[assembly: SndInlineTypes(startKind: <未占用号段>, typeof(NewType), ...)]`
2. 选择 Kind 号段：检查 `TypedDataGenerator.cs` 中 `KindValue` 的校验范围（1-254），确保不与其他适配器重叠
3. Source Generator 会自动检测此程序集非 Home → 走 Adapter 模式 → 生成完整的扩展方法 + ModuleInitializer 注册链
4. 适配层类型走 `_ref` 路径（除非是 ≤8 字节的系统基础类型，但这类类型不应被适配器注册——会被 ORIGOSG001 挡住）

### 7.3 新增 Source Generator 诊断规则

在 `TypedDataGenerator.cs` 的字段区追加 `static readonly DiagnosticDescriptor`，在 `ValidateAndFilter` 中追加校验逻辑。注意：每增加一条校验，必须同时增加对应的 `ORIGOSG00X` 测试用例（`Origo.SourceGeneration.Tests` 项目）。

---

## 8. 相关文档

| 文档 | 内容 |
|------|------|
| [Origo.SourceGeneration README](README.zh.md) | 双模式架构、生成内容清单、注册机制、设计决策 |
| [TypedData 文档](../Origo.Core/Snd/Metadata/README.zh.md) | TypedData 结构体、访问方式、推荐用法 |
| [性能基线](../benchmarks/baseline.zh.md) | 全部基准数据、方法学、效度局限 |
| [Origo.Core.Tests / Benchmarks](../Origo.Core.Tests/Benchmarks.zh.md) | 真实模拟基准说明 |

---

[↑ 回到 Origo.SourceGeneration](README.zh.md)
