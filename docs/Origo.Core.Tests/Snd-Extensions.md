# SND 扩展 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Snd](../Origo.Core/Snd/README.md)

## 被测行为概览

验证 `ISndEntity` 上的扩展方法行为：惰性策略挂载与幂等守卫（`EnsureStrategy`）、可替换策略挂载（`EnsureReplaceableStrategy`）、跨数值类型兼容读取（`TryGetNumeric`/`GetNumeric`）、泛型 ActiveStrategy 调用（`InvokeStrategy<TInput, TOutput>`）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `EnsureStrategyTests.cs` | EnsureStrategy 首次挂载、幂等跳过、空值覆盖 |
| `EntityStrategyExtensionsTests.cs` | EnsureReplaceableStrategy 默认/自定义/空值覆盖/幂等方式/参数校验 |
| `TryGetNumericExtensionsTests.cs` | TryGetNumeric 跨类型读取（int/float/long/double）、非数值返回 false、GetNumeric fallback |
| `ActiveStrategyExtensionsTests.cs` | InvokeStrategy 泛型重载：带输入/无输入序列化往返、null 返回 default |

## EnsureStrategyTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `EnsureStrategy_DataKeyMissing_SetsDataAndReturnsTrue` | 数据键不存在时设置 dataKey 并返回 true | Snd README: ActiveStrategyExtensions |
| `EnsureStrategy_DataKeyExistsWithValue_ReturnsFalse` | 数据键已有非空值时跳过，返回 false 且值不变 | Snd README: ActiveStrategyExtensions |
| `EnsureStrategy_DataKeyExistsButEmpty_StillSetsAndReturnsTrue` | 数据键存在但值为空字符串时仍覆盖并返回 true | Snd README: ActiveStrategyExtensions |

## EntityStrategyExtensionsTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `EnsureReplaceableStrategy_NoConfig_UsesDefault` | 无配置时使用 defaultStrategyIndex，返回 true | Snd README: EnsureReplaceableStrategy |
| `EnsureReplaceableStrategy_CalledAgain_ReturnsFalse` | 第二次调用返回 false（幂等） | Snd README: EnsureReplaceableStrategy |
| `EnsureReplaceableStrategy_ConfiguredOverride_UsesOverride` | 已配置自定义策略索引时保留并返回 false | Snd README: EnsureReplaceableStrategy |
| `EnsureReplaceableStrategy_EmptyOverride_UsesDefault` | 配置为空字符串时覆盖为 defaultStrategyIndex，返回 true | Snd README: EnsureReplaceableStrategy |
| `EnsureReplaceableStrategy_DifferentDefault_CalledAgain_ReturnsFalse` | 第一次用 A 作为默认已挂载，第二次用 B 作为默认仍返回 false 且值保持 A | Snd README: EnsureReplaceableStrategy |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `EnsureReplaceableStrategy_NullEntity_Throws` | entity 为 null | ArgumentNullException |
| `EnsureReplaceableStrategy_NullImplKey_Throws` | implKey 为 null | ArgumentNullException |
| `EnsureReplaceableStrategy_NullDefault_Throws` | defaultStrategyIndex 为 null | ArgumentNullException |

## TryGetNumericExtensionsTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `TryGetNumeric_FloatStored_ReturnsFloat` | 存储 float 通过 TryGetNumeric<float> 读回 | Snd README: TryGetNumericExtensions |
| `TryGetNumeric_IntStored_ReturnsFloat` | 存储 int 通过 TryGetNumeric<float> 跨类型读回（42→42f） | Snd README: TryGetNumericExtensions |
| `TryGetNumeric_LongStored_ReturnsFloat` | 存储 long 通过 TryGetNumeric<float> 跨类型读回（123L→123f） | Snd README: TryGetNumericExtensions |
| `TryGetNumeric_DoubleStored_ReturnsFloat` | 存储 double 通过 TryGetNumeric<float> 跨类型读回（2.5d→2.5f） | Snd README: TryGetNumericExtensions |
| `GetNumeric_FloatStored_ReturnsValue` | GetNumeric 直接读取存储的 float 值 | Snd README: TryGetNumericExtensions |
| `GetNumeric_Missing_ReturnsFallback` | 缺失键时 GetNumeric 返回指定的 fallback 值 | Snd README: TryGetNumericExtensions |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `TryGetNumeric_StringStored_ReturnsFalse` | 存储 string 时 TryGetNumeric 返回 false | 返回 false，out value 为 0f |
| `TryGetNumeric_MissingKey_ReturnsFalse` | 不存在的键时返回 false | 返回 false，out value 为 0f |

## ActiveStrategyExtensionsTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `InvokeStrategy_GenericWithInput_SerializesAndDeserializes` | InvokeStrategy<TInput,TOutput> 将 input 序列化后调用策略，返回结果反序列化为强类型 | Snd README: ActiveStrategyExtensions |
| `InvokeStrategy_GenericNoInput_CallsWithoutInput` | InvokeStrategy<TOutput> 无 input 重载仍正确调用策略 | Snd README: ActiveStrategyExtensions |
| `InvokeStrategy_NullResult_ReturnsDefault` | 策略 Invoke 返回 null 时，泛型方法返回 default(TOutput) | Snd README: ActiveStrategyExtensions |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `StubActiveStrategyEntity` | ActiveStrategyExtensionsTests.cs | ISndEntity 的 stub 实现，通过 Func<object?, object?> 注入 InvokeStrategy 行为，其他成员抛 NotImplementedException |
| `TestNumericEntity` | TryGetNumericExtensionsTests.cs | ISndDataAccess 的测试实现，内部 Dictionary<string, TypedData> 存储，TryGetData 通过 TypedDataObjectConverter 转换 |
| `TestResult` | ActiveStrategyExtensionsTests.cs | 含 Result 属性的简单 POCO，用于泛型 InvokeStrategy 的返回类型反序列化 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| EnsureReplaceableStrategy 的实际策略挂载与 AddStrategy 集成 | 当前测试用 StubSndEntity（不存储实际策略），未验证 EnsureReplaceableStrategy 结果与实际策略 Add 的联动 | Snd README: EnsureReplaceableStrategy |
| TryGetNumeric 对 decimal/byte/short 等数值类型的兼容性 | 仅测试 int/long/float/double 四种类型，其他 CLR 数值类型未覆盖 | Snd README: TryGetNumericExtensions |
| InvokeStrategy 泛型对复杂嵌套类型的序列化往返 | 仅测试简单匿名类型 {Sx, Sz} → TestResult，未测试嵌套对象/数组/枚举 | Snd README: ActiveStrategyExtensions |
| EnsureStrategy/EnsureReplaceableStrategy 在真实运行时环境（含策略池注册）的集成测试 | 当前测试仅操作 DummySndEntity/StubSndEntity 的数据层，未验证策略实际被 Add 和执行 | Snd README: ActiveStrategyExtensions |

---

[↑ 回到 Origo.Core.Tests](README.md)
