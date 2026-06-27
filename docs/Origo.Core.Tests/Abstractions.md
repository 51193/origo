# 测试替身 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Abstractions/FileSystem](../Origo.Core/Abstractions/FileSystem/README.md)

## 被测行为概览

验证测试辅助设施本身的正确性——这些设施是其他所有测试的基础。
覆盖 `TestFileSystem`（内存中的 IFileSystem 实现）的全部 12 种文件/目录操作、
`NullLogger` 的静默行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `MemoryFileSystemTests.cs` | TestFileSystem: 读写/枚举/复制/重命名/删除/父目录/路径拼接 |
| `NullLoggerTests.cs` | NullLogger.Instance 不抛异常、TestLogger 级别过滤行为 |
| `TestFileSystemAdditionalTests.cs` | TestFileSystem 额外边缘路径 |

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

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| IFileSystem.Delete(path) 对目录路径的处理 | 语义不明确 | IFileSystem |

---

[↑ 回到 Origo.Core.Tests](README.md)
