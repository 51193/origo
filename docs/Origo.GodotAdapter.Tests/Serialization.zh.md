<!-- docsync-pair: Origo.GodotAdapter.Tests/Serialization -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 序列化 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
> [↔ 被测模块: Origo.GodotAdapter/Serialization](../Origo.GodotAdapter/Serialization/README.zh.md)
> [↔ 被测模块: Origo.GodotAdapter/Snd](../Origo.GodotAdapter/Snd/README.zh.md)

## 被测行为概览

验证 14 种 Godot 引擎类型的序列化往返：Vector2/3/4、Vector2I/3I、Quaternion、Color、
Basis、Transform2D/3D、Rect2/2I、Aabb、Plane。所有类型通过 `DataSourceConverterRegistry.Write→Read`
完整往返，并验证类型名映射（`TypeStringMapping`）双向解析。

同时验证 TypedData 多层内联系统：14 种 Godot 类型在运行时通过 `TypedDataTypeMap` Kind 解析（Kind ∈ [128, 141]）、
`TypedData.FromObject` / `TryGetXxx` 扩展方法 / `AsXxx` / `TypedDataObjectConverter` 桥接的完整往返，以及与 Core Kind 区间（< 128）无冲突。

`GodotTypedDataPerformanceTests` 标记 `[Trait("Category","Benchmark")]`（类级），仅由 `scripts/benchmark.sh`
运行；`test.sh` 全量测试以 `--filter "Category!=Benchmark"` 将其排除，故其 6 个用例不计入常规覆盖率门禁运行。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GodotDataSourceConvertersTests.cs` | 14 种 Godot 类型转换器往返：写入→读取值一致 |
| `GodotJsonConverterRegistryTests.cs` | 类型名映射注册（全部 14 种）+ 转换器注册后的往返 |
| `GodotTypedDataLayeredTests.cs` | 多层 TypedData：Kind 解析、FromObject 往返、TryGet/AsXxx 扩展、DataType/Data、ObjectConverter fallback、跨层 Kind 隔离 |
| `GodotTypedDataPerformanceTests.cs` | （Benchmark）多层分发性能：注册 vs 未注册写/读吞吐、ObjectConverter switch vs fallback、Factory 路径、实体帧模拟 |

## GodotDataSourceConvertersTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Vector2Converter_RoundTrip` | Vector2(1.5, -2.5) 往返一致 | GodotAdapter Serialization |
| `Vector2IConverter_RoundTrip` | Vector2I(3, -4) 往返一致 | GodotAdapter Serialization |
| `Vector3IConverter_RoundTrip` | Vector3I(5, -6, 7) 往返一致 | GodotAdapter Serialization |
| `Vector4Converter_RoundTrip` | Vector4(1.1,2.2,3.3,4.4) 往返一致 | GodotAdapter Serialization |
| `QuaternionConverter_RoundTrip` | Quaternion 各分量 ε 内一致 | GodotAdapter Serialization |
| `BasisConverter_RoundTrip` | Basis(对角缩放) 往返一致 | GodotAdapter Serialization |
| `BasisConverter_IdentityRoundTrip` | Basis.Identity 往返一致 | GodotAdapter Serialization |
| `Transform2DConverter_RoundTrip` | Transform2D(基向量+平移) 往返一致 | GodotAdapter Serialization |
| `ColorConverter_RoundTrip` | Color(0.1,0.2,0.3,0.4) 往返一致 | GodotAdapter Serialization |
| `ColorConverter_OpaqueWhiteRoundTrip` | Color(1,1,1) 往返一致 | GodotAdapter Serialization |
| `Rect2Converter_RoundTrip` | Rect2(pos+size) 往返一致 | GodotAdapter Serialization |
| `Rect2IConverter_RoundTrip` | Rect2I(pos+size) 往返一致 | GodotAdapter Serialization |
| `AabbConverter_RoundTrip` | Aabb(pos+size) 往返一致 | GodotAdapter Serialization |
| `AabbConverter_ZeroSizeRoundTrip` | Aabb(0,0,0,0,0,0) 往返一致 | GodotAdapter Serialization |

## GodotJsonConverterRegistryTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RegisterTypeMappings_RegistersAll14TypeNames` | 注册后 14 种类型均可由类型解析出名称（type→name） | GodotAdapter Serialization |
| `RegisterTypeMappings_AllTypesCanBeResolvedByName` | 注册后 14 种类型均可由名称解析出类型（name→type） | GodotAdapter Serialization |
| `RegisterDataSourceConverters_AllowsVectorRoundTrip` | 注册转换器后 Vector3 往返一致 | GodotAdapter Serialization |
| `RegisterDataSourceConverters_AllowsTransformAndPlaneConverters` | Transform3D 与 Plane 往返一致 | GodotAdapter Serialization |
| `RegisterDataSourceConverters_Vector2IAnd3IRoundTrip` | Vector2I/Vector3I 往返一致 | GodotAdapter Serialization |
| `RegisterDataSourceConverters_Vector4AndQuaternionRoundTrip` | Vector4 一致、Quaternion 各分量 ε 内一致 | GodotAdapter Serialization |
| `RegisterDataSourceConverters_Rect2AndRect2IRoundTrip` | Rect2/Rect2I 往返一致 | GodotAdapter Serialization |
| `RegisterDataSourceConverters_AabbRoundTrip` | Aabb 往返一致 | GodotAdapter Serialization |

## GodotTypedDataLayeredTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Godot_Vector2_Kind_Is_Resolved` | Vector2 解析为 Kind 128 | GodotAdapter Snd |
| `Godot_Vector3_Kind_Is_Resolved` | Vector3 解析为 Kind 130 | GodotAdapter Snd |
| `Godot_Color_Kind_Is_Resolved` | Color 解析为 Kind 137 | GodotAdapter Snd |
| `Godot_Transform3D_Kind_Is_Resolved` | Transform3D 解析为 Kind 136 | GodotAdapter Snd |
| `Godot_Plane_Kind_Is_Resolved` | Plane 解析为 Kind 141 | GodotAdapter Snd |
| `Godot_Vector2_FromObject_RoundTrip` | Vector2 经 TypedData(128) 构造，DataType 与 ToObject 还原一致 | GodotAdapter Snd |
| `Godot_Vector3_FromObject_RoundTrip` | Vector3 经 TypedData(130) 构造往返一致 | GodotAdapter Snd |
| `Godot_Color_FromObject_RoundTrip` | Color 经 TypedData(137) 构造往返一致 | GodotAdapter Snd |
| `Godot_Vector2_Extension_TryGet` | `TryGetVector2` 返回 true 且值一致 | GodotAdapter Snd |
| `Godot_Vector3_Extension_TryGet` | `TryGetVector3` 返回 true 且值一致 | GodotAdapter Snd |
| `Godot_Color_Extension_TryGet` | `TryGetColor` 返回 true 且值一致 | GodotAdapter Snd |
| `All_GodotTypes_Registered` | 14 种 Godot 类型 Kind 均落在 [128, 141] 区间 | GodotAdapter Snd |
| `DataType_ForGodotType_ReturnsCorrectType` | Godot 类型 TypedData 的 DataType 正确 | GodotAdapter Snd |
| `Data_ForGodotType_ReturnsUnboxedValue` | ToObject 返回拆箱后的原值 | GodotAdapter Snd |
| `AsXxx_ForGodotType_Works` | `AsVector2()` 返回正确值 | GodotAdapter Snd |
| `TryGetAllGodotTypes_RoundTrip` | Vector2/2I/3/3I、Color、Rect2/2I 经各自 TryGet 往返一致 | GodotAdapter Snd |
| `GodotType_ObjectConverter_ToObject_UsesFallback` | `TypedDataObjectConverter.ToObject` 经 fallback 返回 Vector3 | GodotAdapter Snd |
| `GodotType_ObjectConverter_FromObject_UsesFallback` | `FromObject(130, v)` 经 fallback 返回 (0, refValue) | GodotAdapter Snd |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Godot_Type_WrongKind_ReturnsFalse` | 以错误的 TryGet 读取（Vector2 用 TryGetVector3/TryGetColor） | 返回 false |
| `Core_Int_DoesNotConflict_With_GodotKind` | int 的 TypedData 用 TryGetVector2 读取 | TryGetVector2 false、TryGetInt32 true 返回 42 |
| `GodotType_Null_PreservesDataType` | TypedData(130, 0, null)（null 值） | DataType 仍为 Vector3，ToObject 返回 null |
| `GodotKind_NotRecognized_ByCoreOnlyUnregistered` | Godot 类型 Kind ≥ 128 vs Core int Kind < 128 | Godot Kind 非 0 且 ≥ 128，int Kind=5 且 < 128 |

## GodotTypedDataPerformanceTests 测试详情（Benchmark）

### 正确路径（性能基准）

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `WriteThroughput_Registered_Outperforms_Unregistered` | 已注册 Kind(130) vs 未注册 Kind(255) 写入吞吐对比；断言两侧提取值等价 | GodotAdapter Snd |
| `ReadThroughput_TryGetVector3_Outperforms_IsT` | `TryGetVector3`（Kind）vs `ToObject is Vector3` 读取，结果一致并打印对比 | GodotAdapter Snd |
| `ObjectConverter_ToObject_GodotSwitch_Outperforms_Data` | ToObject switch 分发 vs Data 属性路径对比；断言返回值为正确 Vector3 | GodotAdapter Snd |
| `ObjectConverter_FromObject_GodotSwitch_Outperforms_Fallback` | FromObject Kind-switch(137) vs 未注册 fallback(255) 对比；断言两侧提取 Color 值等价 | GodotAdapter Snd |
| `Factory_CreateExtract_Vector3_RegisteredVsUnregistered` | `TypedDataFactory<Vector3>` Create+Extract 基于 Kind 路径吞吐；断言往返正确 | GodotAdapter Snd |
| `MixedEntitySimulation_GodotTypes` | 500 实体 × 60 帧混合模拟吞吐与分配；断言实体 0 的 position/alive 数据完好 | GodotAdapter Snd |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 序列化测试不定义辅助策略类；性能测试通过 `PerfReporter.ReportTable` / `PerfReporter.CompareTable` 打印统一汇总表格 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| Godot 类型转换器在畸形/缺字段 JSON 节点上的错误路径未覆盖 | 反序列化容错行为未验证 | Origo.GodotAdapter/Serialization |
| `TryGetAllGodotTypes_RoundTrip` 未覆盖 Vector4/Quaternion/Basis/Transform2D/3D/Aabb/Plane 的 TryGet 往返 | 部分 Godot 类型的扩展方法往返未直接验证 | Origo.GodotAdapter/Snd |
| 性能基准含正确性烟雾断言（值等价/往返验证），但不设硬性性能阈值 | 数据正确性回归会使测试失败；性能退化仅作观测，不自动失败 | Origo.GodotAdapter/Snd |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.zh.md)
