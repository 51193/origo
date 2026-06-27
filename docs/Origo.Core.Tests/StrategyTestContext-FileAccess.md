# 策略测试上下文文件访问 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Testing](../Origo.Core/Testing/README.md)
> [↔ 被测行为: usage/strategy-testing](../usage/strategy-testing.md)

## 被测行为概览

验证 `ISndFileAccess` 在 `StrategyTestContext` 上的行为——策略单元测试中的内存文件系统支持。`StrategyTestContext` 在构造时自动创建完整的文件 I/O 链路（`MemoryFileSystem` → `DataSourceIoGateway` → `DataSourceConverterRegistry`），允许策略单元测试在完全无磁盘的环境下验证文件读写逻辑。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `StrategyTestContextFileAccessTests.cs` | ISndFileAccess 在 StrategyTestContext 上的行为 |

## StrategyTestContextFileAccessTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `FileExists_ReturnsFalseForNonexistentFile` | 初始状态下文件不存在 | ISndFileAccess.FileExists |
| `FileExists_ReturnsTrueAfterWrite` | 写入后文件存在 | ISndFileAccess.FileExists |
| `WriteThenReadFile_RoundTrip_PreservesData` | DataSourceNode 写入后读回，键值与数值一致 | ISndFileAccess.ReadFile/WriteFile |
| `WriteThenReadFile_RoundTrip_ForArrayNode` | 数组节点写入后读回，长度和元素一致 | ISndFileAccess.ReadFile/WriteFile |
| `WriteFile_OverwriteTrue_ReplacesExisting` | overwrite=true 时覆盖已存在文件 | ISndFileAccess.WriteFile |
| `WriteFile_OverwriteFalse_ThrowsWhenFileExists` | overwrite=false 且文件已存在时抛出 | ISndFileAccess.WriteFile |
| `WriteThenReadObject_RoundTrip_Int` | int 值写入后读回一致 | ISndFileAccess.ReadObject/WriteObject |
| `WriteThenReadObject_RoundTrip_String` | string 值写入后读回一致 | ISndFileAccess.ReadObject/WriteObject |
| `WriteThenReadObject_RoundTrip_Bool` | bool 值写入后读回一致 | ISndFileAccess.ReadObject/WriteObject |
| `WriteThenReadObject_RoundTrip_Double` | double 值写入后读回一致 | ISndFileAccess.ReadObject/WriteObject |
| `WriteObject_OverwriteTrue_ReplacesExisting` | 强类型写入支持 overwrite 语义 | ISndFileAccess.WriteObject |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ReadFile_ThrowsForNonexistentFile` | 文件路径不存在 | 抛出异常（MemoryFileSystem 抛出 FileNotFoundException） |
| `ReadObject_ThrowsForNonexistentFile` | 文件不存在 | 抛出异常 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本测试文件不定义辅助策略，纯接口行为测试 |

## 设计决策

### 为什么 StrategyTestContext 内置 MemoryFileSystem

策略单元测试通常需要轻量级文件 I/O 支持（读取配置、写入状态）。`StrategyTestContext` 在构造函数中自动创建完整的 `MemoryFileSystem` → `DataSourceIoGateway` → `DataSourceConverterRegistry` 链路，使策略测试可以零配置使用 `ISndFileAccess` 的所有方法。所有文件操作在内存中完成，无需创建临时目录或模拟真实文件系统。

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 策略在 StrategyTestScenario 中通过 ISndContext 使用 ISndFileAccess 的集成测试 | 当前仅测试直接通过 StrategyTestContext 的接口行为，未测试完整策略流程中的文件读写 | ISndFileAccess |
| 自定义 Converter 注册后的 ReadObject/WriteObject 行为 | 未验证用户在测试中注册自定义类型转换器后文件读写的正确性 | DataSourceConverterRegistry |

---

[↑ 回到 Origo.Core.Tests](README.md)
