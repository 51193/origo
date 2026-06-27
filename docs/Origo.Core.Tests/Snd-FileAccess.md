# 文件访问 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.md)
> [↔ 被测行为: usage/agent-reference](../usage/agent-reference.md)

## 被测行为概览

验证 `ISndFileAccess` 在 `SndContext` 上的全部行为：DataSourceNode 文件读写往返、强类型对象读写往返、文件存在检查、覆盖语义（overwrite）、Map 格式解析、嵌套 JSON 解析、错误路径（不存在文件、null 路径、无效 JSON）和边界路径（空对象、Null 节点、Boolean 值）。

所有文件 I/O 使用共享的 `TestFileSystem`（内存实现），不涉及真实磁盘操作。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `SndContextFileAccessTests.cs` | ISndFileAccess 在 SndContext 上的完整行为 |

## SndContextFileAccessTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ReadFile_ReadsJsonAndReturnsParsedTree` | 读取 JSON 文件返回解析后的 DataSourceNode 树，支持 `["key"]` 访问 | ISndFileAccess.ReadFile |
| `ReadFile_ReadsMapFileAndReturnsParsedTree` | 读取 .map 文件返回解析后的 DataSourceNode 树（`key: value` 格式） | ISndFileAccess.ReadFile |
| `ReadFile_ReadsNestedJsonStructure` | 读取嵌套 JSON 结构，支持对象/数组多层访问 | ISndFileAccess.ReadFile |
| `WriteFile_WritesNodeAndCanBeReadBack` | 写入 DataSourceNode 树后可通过 ReadFile 读回，数据一致 | ISndFileAccess.WriteFile |
| `WriteFile_WithOverwriteTrue_OverwritesExistingFile` | overwrite=true 时覆盖已存在文件，旧数据被替换 | ISndFileAccess.WriteFile |
| `WriteFile_WithOverwriteFalse_ThrowsWhenFileExists` | overwrite=false 且文件已存在时抛出 IOException | ISndFileAccess.WriteFile |
| `WriteFile_WritesArrayNode` | 写入 DataSourceNode 数组节点并完整读回 | ISndFileAccess.WriteFile |
| `FileExists_ReturnsTrueForExistingFile` | 文件存在时返回 true | ISndFileAccess.FileExists |
| `FileExists_ReturnsFalseForNonexistentFile` | 文件不存在时返回 false | ISndFileAccess.FileExists |
| `ReadObject_DeserializesJsonToTypedPrimitive` | 读取 JSON 数字并通过 Converter 反序列化为 int | ISndFileAccess.ReadObject |
| `ReadObject_DeserializesJsonToString` | 读取 JSON 字符串并通过 Converter 反序列化为 string | ISndFileAccess.ReadObject |
| `WriteObject_SerializesTypedValueAndCanBeReadBack` | 写入 int 后读回，值一致 | ISndFileAccess.WriteObject |
| `WriteObject_WithOverwrite_ReplacesExisting` | 强类型写入支持 overwrite 语义 | ISndFileAccess.WriteObject |
| `ReadWriteObject_RoundTrip_PreservesBool` | bool 值往返保持正确 | ISndFileAccess.ReadObject/WriteObject |
| `ReadWriteObject_RoundTrip_PreservesDouble` | double 值往返保持精度 | ISndFileAccess.ReadObject/WriteObject |
| `FileAccess_IsAccessibleThroughRoleInterface` | ISndFileAccess 可通过 ISndContext cast 获取并使用 | ISndContext |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ReadFile_ThrowsForNonexistentFile` | 文件路径不存在 | 抛出异常（类型取决于 IFileSystem 实现） |
| `ReadFile_ThrowsForNullPath` | null 路径 | ArgumentException |
| `ReadFile_ThrowsForInvalidJson` | JSON 语法错误（如 `{broken`） | 延迟展开时抛出异常 |
| `WriteFile_ThrowsForNullNode` | null DataSourceNode | ArgumentNullException |
| `ReadObject_ThrowsForNonexistentFile` | 文件不存在 | 抛出异常 |
| `ReadObject_ThrowsForTypeMismatch` | JSON 字符串节点读取为 int | 抛出异常（Converter 不匹配） |
| `FileExists_ReturnsFalseForEmptyPath` | 空字符串路径 | ArgumentException（Gateway 拒绝空路径） |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `WriteFile_EmptyObject_RoundTrip` | 空 DataSourceNode 对象 | 写入并读回，不含任何键 |
| `WriteFile_NullValueNode_RoundTrip` | 对象包含 Null 值节点 | 读回后 IsNull 为 true |
| `WriteFile_BooleanValues_RoundTrip` | 对象包含 true/false 节点 | 读回后 AsBool() 正确 |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本测试文件不定义辅助策略，纯接口行为测试 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| ReadObject/WriteObject 对复杂自定义类型的往返 | 当前仅测试 BCL 原语（int/string/bool/double），未测试用户自定义类型 | ISndFileAccess |
| 大量文件并发读写的线程安全性 | 多线程场景未覆盖 | — |
| ReadFile 对大文件的延迟展开内存行为 | 大 JSON 文件的性能特征未覆盖 | DataSourceNode |

---

[↑ 回到 Origo.Core.Tests](README.md)
