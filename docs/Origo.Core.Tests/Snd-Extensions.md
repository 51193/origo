# SND 扩展 测试

> [↑ 回到 Origo.Core.Tests](README.md)

## 验证能力

SND 实体上的扩展方法：惰性策略挂载（`EnsureStrategy`）、跨类型数值读取（`TryGetNumeric`）、泛型 ActiveStrategy 调用（`InvokeStrategy<TInput, TOutput>`）。

## 测试覆盖

### 正确路径（EnsureStrategy）

| 测试 | 验证内容 |
|------|---------|
| 首次调用时挂载策略并写入数据键 | `EnsureStrategy` 检查守护键不存在后执行 `AddStrategy` 和 `SetData` |
| 已有守护键时跳过挂载 | 幂等性：第二次调用不重复添加策略 |

### 正确路径（TryGetNumeric）

| 测试 | 验证内容 |
|------|---------|
| int → float 读取 | 跨数值类型的强制转换：int 值可通过 `TryGetNumeric<float>` 读取 |
| float → int 读取 | 反向转换同样支持 |
| long/double 兼容 | 支持 long ↔ double 的跨类型读取 |

### 正确路径（InvokeStrategy 泛型）

| 测试 | 验证内容 |
|------|---------|
| `InvokeStrategy<TInput, TOutput>` 序列化往返 | 输入对象被 JSON 序列化后传递给 ActiveStrategy，返回结果被反序列化为强类型 |
| 无输入参数的泛型重载 | `InvokeStrategy<TOutput>(string index)` 不传递 input 参数 |
| 返回 null 时返回 default | ActiveStrategy 返回 null 时，泛型方法返回 `default(TOutput)` |
