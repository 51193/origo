# Serialization

> [↑ 回到 Origo.GodotAdapter](../README.md) · [↔ Core: Serialization](../../Origo.Core/Serialization/README.md)

## 概述

Godot 引擎类型在 Origo 序列化系统中的注册。向 Core 的 `TypeStringMapping` 和 `DataSourceConverterRegistry` 添加 14 种 Godot 内置类型的支持，使 `Vector2`、`Vector3`、`Color`、`Transform3D` 等引擎类型能够在存档 JSON 中正确序列化/反序列化。

此外，Origo.GodotAdapter 通过 `[assembly: SndInlineTypes(startKind: 128, ...)]` 向 TypedData 多层内联系统注册这 14 种类型，使运行时 `GetKindForType(typeof(Vector3))` 返回确定的 kind 值（130），避免走 `is T` 运行时模式匹配的兜底路径。详见 [Origo.SourceGeneration](../../Origo.SourceGeneration/README.md)。

## 包含文件

| 文件 | 职责 |
|------|------|
| `GodotEngineTypeNames.cs` | 14 种 Godot 引擎类型的稳定字符串常量 |
| `GodotDataSourceConverters.cs` | 14 种 Godot 类型的 DataSourceConverter 实现 |
| `GodotJsonConverterRegistry.cs` | 一站式注册方法：RegisterTypeMappings + RegisterDataSourceConverters |

## 支持的 Godot 类型

| 类型 | JSON 格式 | 说明 |
|------|----------|------|
| `Vector2` | `{"x":1.0,"y":2.0}` | 2D 浮点向量 |
| `Vector2I` | `{"x":1,"y":2}` | 2D 整数向量 |
| `Vector3` | `{"x":1,"y":2,"z":3}` | 3D 浮点向量 |
| `Vector3I` | `{"x":1,"y":2,"z":3}` | 3D 整数向量 |
| `Vector4` | `{"x":1,"y":2,"z":3,"w":4}` | 4D 浮点向量 |
| `Quaternion` | `{"x","y","z","w"}` | 四元数旋转 |
| `Color` | `{"r","g","b","a"}` | RGBA 颜色 |
| `Basis` | `{"x":Vec3,"y":Vec3,"z":Vec3}` | 3x3 矩阵基 |
| `Transform2D` | `{"x":Vec2,"y":Vec2,"origin":Vec2}` | 2D 变换 |
| `Transform3D` | `{"basis":Basis,"origin":Vec3}` | 3D 变换 |
| `Rect2` | `{"position":Vec2,"size":Vec2}` | 2D 矩形 |
| `Rect2I` | `{"position":Vec2I,"size":Vec2I}` | 2D 整数矩形 |
| `Aabb` | `{"position":Vec3,"size":Vec3}` | 轴对齐包围盒 |
| `Plane` | `{"normal":Vec3,"d":float}` | 3D 平面 |

## 设计决策

### 为什么复合类型依赖基础类型转换器

`Basis` 由 3 个 `Vector3` 组成，`Transform3D` 由 `Basis` + `Vector3` 组成。复用已有转换器（如 `Vector3DataSourceConverter`）避免重复实现，且确保子字段的读写格式一致（如所有向量的 x/y/z 格式统一）。

### 为什么类型名使用简短形式而非带命名空间

`"Vector3"` 足以在 Godot 上下文中唯一标识 `Godot.Vector3` 类型。简短类型名减少 JSON 存储开销，同时 `TypeStringMapping` 的注册机制保证在反序列化时精确映射到正确的 CLR 类型。

### 为什么 Plane 的 d 字段使用 AsFloat 而非组合

`Plane` = `Normal(Vector3)` + `D(float)`。`D` 是标量而非向量，直接使用 `AsFloat()` 简单直接。组合方式（如继续用 Vector3DataSourceConverter）会引入不必要的嵌套。

---
[↑ 回到 Origo.GodotAdapter](../README.md)
