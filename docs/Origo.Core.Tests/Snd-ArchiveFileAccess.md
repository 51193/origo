# 存档文件访问 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Abstractions/Snd](../Origo.Core/Abstractions/Snd/README.md)
> [↔ 被测行为: usage/agent-reference](../usage/agent-reference.md)

## 被测行为概览

验证 `ISndArchiveFileAccess` 在 `SndContext` 上的全部行为：DataSourceNode 文件读写往返（路径相对于存档活动目录的 `extra/` 子目录）、强类型对象读写往返、文件存在检查、删除文件、覆盖语义（overwrite）、Map 格式解析、嵌套 JSON 解析、错误路径（不存在文件、路径穿越、类型不匹配、null 节点）和边界路径（空对象、Null 节点、Boolean 值），以及存档 save/load 往返保持。

所有文件 I/O 使用共享的 `TestFileSystem`（内存实现），不涉及真实磁盘操作。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `SndContextArchiveFileAccessTests.cs` | ISndArchiveFileAccess 在 SndContext 上的 complete 行为，含 save/load 往返 |

## SndContextArchiveFileAccessTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ReadFile_ReadsJsonFromExtraDirectory` | 从 `current/extra/` 读取 JSON 文件返回解析后的 DataSourceNode 树，路径自动转换为 `extra/` 子目录下的相对路径 | ISndArchiveFileAccess.ReadFile |
| `ReadFile_ReadsMapFileFromExtraDirectory` | 从 `current/extra/` 读取 .map 文件返回解析后的 DataSourceNode 树（`key: value` 格式） | ISndArchiveFileAccess.ReadFile |
| `ReadFile_ReadsNestedJsonStructure` | 读取嵌套 JSON 结构（对象含数组），支持对象/数组多层访问 | ISndArchiveFileAccess.ReadFile |
| `WriteFile_WritesToExtraAndCanBeReadBack` | 写入 DataSourceNode 树到 `current/extra/` 后可通过 ReadFile 读回，数据一致 | ISndArchiveFileAccess.WriteFile |
| `WriteFile_WithOverwriteTrue_OverwritesExistingFile` | overwrite=true 时覆盖 `extra/` 中已存在文件，旧数据被替换 | ISndArchiveFileAccess.WriteFile |
| `WriteFile_WithOverwriteFalse_ThrowsWhenFileExists` | overwrite=false 且文件已存在时抛出 IOException | ISndArchiveFileAccess.WriteFile |
| `WriteFile_WritesArrayNode` | 写入 DataSourceNode 数组节点并完整读回 | ISndArchiveFileAccess.WriteFile |
| `WriteFile_CreatesParentDirectories` | 写入深层路径时自动创建 `extra/a/b/c/` 所有父目录 | ISndArchiveFileAccess.WriteFile |
| `FileExists_ReturnsTrueForExistingFile` | `extra/` 中文件存在时返回 true | ISndArchiveFileAccess.FileExists |
| `FileExists_ReturnsFalseForNonexistentFile` | 文件不存在时返回 false | ISndArchiveFileAccess.FileExists |
| `ReadObject_DeserializesTypedPrimitive` | 读取 JSON 数字并通过 Converter 反序列化为 int | ISndArchiveFileAccess.ReadObject |
| `ReadWriteObject_RoundTrip_PreservesBool` | bool 值往返保持正确 | ISndArchiveFileAccess.ReadObject/WriteObject |
| `ReadWriteObject_RoundTrip_PreservesString` | string 值往返保持正确 | ISndArchiveFileAccess.ReadObject/WriteObject |
| `ReadWriteObject_RoundTrip_PreservesDouble` | double 值往返保持精度 | ISndArchiveFileAccess.ReadObject/WriteObject |
| `DeleteFile_RemovesExistingFile` | 删除 `extra/` 中已存在文件，删除后文件不存在 | ISndArchiveFileAccess.DeleteFile |
| `FileExists_ReturnsFalseAfterDelete` | 删除文件后 FileExists 返回 false | ISndArchiveFileAccess.DeleteFile |
| `DeleteFile_ThenRead_Throws` | 删除文件后读取抛出异常 | ISndArchiveFileAccess.DeleteFile |
| `ArchiveFileAccess_IsAccessibleThroughRoleInterface` | ISndArchiveFileAccess 可通过 ISndContext cast 获取并使用 | ISndContext |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `ReadFile_ThrowsForNonexistentFile` | 文件路径不存在 | 抛出异常 |
| `ReadFile_ThrowsForPathTraversal` | `../escape.json` 路径穿越 | ArgumentException |
| `WriteFile_ThrowsForPathTraversal` | `../escape.json` 路径穿越 | ArgumentException |
| `DeleteFile_ThrowsForNonexistentFile` | 删除不存在的文件 | InvalidOperationException |
| `DeleteFile_ThrowsForPathTraversal` | `../escape.json` 路径穿越 | ArgumentException |
| `ReadObject_ThrowsForTypeMismatch` | JSON 字符串节点作为 int 反序列化 | 抛出异常（Converter 不匹配） |
| `WriteFile_ThrowsForNullNode` | null DataSourceNode | ArgumentNullException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `WriteFile_EmptyObject_RoundTrip` | 空 DataSourceNode 对象（零 key） | 写入并读回，不含任何键 |
| `WriteFile_NullValueNode_RoundTrip` | 对象包含 Null 值节点 | 读回后 IsNull 为 true |
| `WriteFile_BooleanValues_RoundTrip` | 对象包含 true/false 节点 | 读回后 AsBool() 正确 |

### Save/Load 往返

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `WriteFile_SurvivesSaveLoadRoundTrip` | `extra/` 中写入的文件在 save → load 后仍可读回，数据一致（验证 `current/extra/` 被快照到 `save_{id}/extra/` 并在 load 时恢复） | ISndArchiveFileAccess |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| 无 | — | 本测试文件不定义辅助策略，纯接口行为测试 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| ReadObject/WriteObject 对复杂自定义类型的往返 | 当前仅测试 BCL 原语（int/string/bool/double），未测试用户自定义类型 | ISndArchiveFileAccess |
| 大量文件并发读写的线程安全性 | 多线程场景未覆盖 | — |
| `extra/` 目录在 Dispose/进度销毁时的清理行为 | 生命周期边界清理未独立验证 | ISndArchiveFileAccess |
| ReadFile 对超大型文件的延迟展开内存行为 | 大 JSON 文件的性能特征未覆盖 | DataSourceNode |

---

[↑ 回到 Origo.Core.Tests](README.md)
