# 序列化 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)
> [↔ 被测模块: Origo.GodotAdapter/Serialization](../Origo.GodotAdapter/Serialization/README.md)

## 被测行为概览

验证 14 种 Godot 引擎类型的序列化往返：Vector2/3/4、Vector2I/3I、Quaternion、Color、
Basis、Transform2D/3D、Rect2/2I、Aabb、Plane。所有类型通过 `ConverterRegistry.Write→Read`
完整往返，JSON 集成验证。

同时验证 TypedData 多层内联系统：14 种 Godot 类型在运行时通过 KindResolver 委托链正确解析 Kind 值，`TypedData.FromObject` / `TryGetXxx` 扩展方法 / `TypedDataObjectConverter` 桥接的完整往返，以及跨层 Kind 无冲突。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GodotDataSourceConvertersTests.cs` | 14 种 Godot 类型转换器往返：写入→读取值一致 |
| `GodotJsonConverterRegistryTests.cs` | Godot 类型在 JSON 下的编解码往返 |
| `GodotTypedDataLayeredTests.cs` | 多层 TypedData：Kind 解析、FromObject 往返、TryGet 扩展方法、DataType/Data 属性、ObjectConverter fallback、跨层 Kind 隔离 |
| `GodotTypedDataPerformanceTests.cs` | 多层分发性能：注册 vs 未注册写入/读取吞吐、ObjectConverter switch vs fallback、Factory 路径、实体帧模拟 |

### 正确路径（代表性摘录）

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Vector2Converter_RoundTrip` | Vector2(1.5, -2.5) 往返一致 | GodotAdapter Serialization |
| `Vector2IConverter_RoundTrip` | Vector2I(3, -4) 往返一致 | GodotAdapter Serialization |
| `Vector3IConverter_RoundTrip` | Vector3I(5, -6, 7) 往返一致 | GodotAdapter Serialization |
| `Vector4Converter_RoundTrip` | Vector4(1.1,2.2,3.3,4.4) 往返一致 | GodotAdapter Serialization |
| `QuaternionConverter_RoundTrip` | Quaternion 各分量 ε 内一致 | GodotAdapter Serialization |
| `BasisConverter_RoundTrip` | Basis 往返一致 | GodotAdapter Serialization |
| `BasisConverter_IdentityRoundTrip` | Basis.Identity 往返一致 | GodotAdapter Serialization |
| `Transform2DConverter_RoundTrip` | Transform2D(平移+旋转) 往返一致 | GodotAdapter Serialization |
| `ColorConverter_RoundTrip` | Color(0.1,0.2,0.3,0.4) 往返一致 | GodotAdapter Serialization |
| `ColorConverter_OpaqueWhiteRoundTrip` | Color(1,1,1) 往返一致 | GodotAdapter Serialization |
| `Rect2Converter_RoundTrip` | Rect2(pos+size) 往返一致 | GodotAdapter Serialization |
| `Rect2IConverter_RoundTrip` | Rect2I(pos+size) 往返一致 | GodotAdapter Serialization |
| `AabbConverter_RoundTrip` | Aabb(pos+size) 往返一致 | GodotAdapter Serialization |
| `AabbConverter_ZeroSizeRoundTrip` | Aabb(0,0,0,0,0,0) 往返一致 | GodotAdapter Serialization |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.md)
