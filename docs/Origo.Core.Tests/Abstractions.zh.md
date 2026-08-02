<!-- docsync-pair: Origo.Core.Tests/Abstractions -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 测试替身 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Abstractions/FileSystem](../Origo.Core/Abstractions/FileSystem/README.zh.md)
> [↔ 被测模块: Origo.Core/Abstractions/Logging](../Origo.Core/Abstractions/Logging/README.zh.md)

## 被测行为概览

验证测试辅助设施本身的正确性——这些设施是其他所有测试的基础。
覆盖 `TestFileSystem`（内存中的 IFileSystem 实现）的全部 12 种文件/目录操作、
`NullLogger` 的静默行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `MemoryFileSystemTests.cs` | TestFileSystem: 读写/枚举/复制/重命名/删除/父目录/路径拼接 |
| `NullLoggerTests.cs` | NullLogger.Instance 不抛异常 |
| `TestLoggerFilterTests.cs` (in `TestSupport/`) | TestLogger 日志级别过滤行为 |
| `TestMemoryFileSystemAdditionalTests.cs` | TestMemoryFileSystem 额外边缘路径 |

## MemoryFileSystemTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `MemoryFileSystem_BasicOperations` | Write→Exists→Read→Delete 链路 | IFileSystem |
| `MemoryFileSystem_EnumerateFiles` | 递归/非递归枚举、通配符过滤 | IFileSystem |
| `MemoryFileSystem_CombinePath` | 路径拼接、尾斜杠处理 | IFileSystem |
| `MemoryFileSystem_Rename` | 文件/目录重命名 | IFileSystem |
| `MemoryFileSystem_DeleteDirectory` | 递归删除目录 | IFileSystem |
| `MemoryFileSystem_EnumerateFiles_CustomPatternAndBackslashNormalize` | 反斜杠路径标准化、自定义通配符 | IFileSystem |
| `MemoryFileSystem_Rename_FileAtRoot` | 根级文件重命名 | IFileSystem |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `MemoryFileSystem_ReadAllText_Missing_ThrowsFileNotFound` | 读取不存在的文件 | FileNotFoundException |
| `MemoryFileSystem_WriteAllText_NoOverwrite_ThrowsWhenExists` | 不覆盖写入已存在文件 | IOException |
| `MemoryFileSystem_Copy_SourceMissing_Throws` | 复制不存在的文件 | FileNotFoundException |
| `MemoryFileSystem_Copy_NoOverwrite_ThrowsWhenDestExists` | 不覆盖复制到已存在路径 | IOException |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `MemoryFileSystem_CreateDirectory_EmptyPath_NoOp` | 空路径创建目录 | 不抛异常 |
| `MemoryFileSystem_GetParentDirectory_EdgeCases` | 无路径分隔符的文件/绝对路径/普通路径 | 正确返回父目录 |
| `MemoryFileSystem_EnumerateDirectories_FromExplicitDirectories` | 从显式目录枚举子目录 | 包含子目录 |

## NullLoggerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `NullLogger_ImplementsILogger` | NullLogger.Instance 可通过 ILogger 接口引用，不抛异常 | ILogger |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `NullLogger_Instance_IsSingleton` | 两次获取 Instance | 返回同一实例 |

## TestMemoryFileSystemAdditionalTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `TestFileSystem_WriteAllText_And_ReadAllText` | WriteAllText 后 ReadAllText 读回一致 | IFileSystem |
| `TestFileSystem_WriteAllText_Overwrite` | overwrite=true 覆盖已存在文件 | IFileSystem |
| `TestFileSystem_Delete_RemovesFile` | Delete 移除文件后 Exists 返回 false | IFileSystem |
| `TestFileSystem_CombinePath_CombinesCorrectly` | 路径拼接正确 | IFileSystem |
| `TestFileSystem_GetParentDirectory` | 从文件路径提取父目录 | IFileSystem |
| `TestFileSystem_EnumerateDirectories` | 从显式目录枚举子目录 | IFileSystem |
| `TestFileSystem_Rename_MovesAllFilesAndDirectories` | 重命名目录后所有文件/子目录迁移，数据不变 | IFileSystem |
| `TestFileSystem_DeleteDirectory_RemovesAllContents` | 递归删除目录及全部内容 | IFileSystem |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `TestFileSystem_WriteAllText_NoOverwrite_Throws` | overwrite=false 写入已存在文件 | IOException |

## TestLoggerFilterTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `MinimumLevel_SetToInfo_SuppressesDebug` | MinimumLevel=Info 时 Debug 级别消息被抑制 | TestLogger |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `MinimumLevel_DefaultDebug_RecordsAllLevels` | 默认 MinimumLevel=Debug | 所有级别消息均记录 |
| `MinimumLevel_SetToError_OnlyRecordsError` | MinimumLevel=Error | 仅 Error 级别被记录 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| IFileSystem.Delete(path) 对目录路径的处理 | 语义不明确 | IFileSystem |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
