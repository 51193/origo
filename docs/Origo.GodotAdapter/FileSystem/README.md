# FileSystem

> [↑ 回到 Origo.GodotAdapter](../README.md) · [↔ Core 抽象: Abstractions/FileSystem](../../Origo.Core/Abstractions/FileSystem/README.md)

## 概述

`IFileSystem` 接口的 Godot 实现。基于 Godot 的 `FileAccess` 和 `DirAccess` API，支持 `res://`（只读项目资源）和 `user://`（可写用户数据）两种虚拟路径方案。所有路径操作在此层实现，正确处理 Godot 虚拟路径语义。

## 包含文件

| 文件 | 职责 |
|------|------|
| `GodotFileSystem.cs` | `IFileSystem` 的 Godot 实现，委托给分段静态类 |
| `GodotFileOperations.cs` | 文件级操作：Exists/ReadAllText/WriteAllText/Copy/Delete |
| `GodotDirectoryOperations.cs` | 目录级操作：Exists/Create/EnumerateFiles/EnumerateDirectories/Rename/DeleteRecursive |
| `GodotPathResolver.cs` | 薄转发包装，委托给 `Origo.Core.Utility.PathUtility` 执行路径拼接与父目录提取（含路径遍历防护） |

## 模块详解

### GodotFileSystem

薄外观层，将所有 `IFileSystem` 方法委托给适当的静态工具类。例如 `Exists` → `GodotFileOperations.Exists`，`DirectoryExists` → `GodotDirectoryOperations.Exists`。

### GodotFileOperations

- **ReadAllText**：`FileAccess.Open(path, Read)` → `GetAsText()`
- **WriteAllText**：`FileAccess.Open(path, Write)` → `StoreString(content)`
- **Copy**：ReadAllText + WriteAllText（简单复制，适合小文件场景；大文件复制可由上层优化）
- **Delete**：`DirAccess.RemoveAbsolute(path)`

### GodotDirectoryOperations

- **Create**：`DirAccess.MakeDirRecursiveAbsolute`
- **EnumerateFiles**：支持 `*pattern` 后缀过滤和递归模式
- **DeleteRecursive**：先删文件，再递归删子目录，最后删当前目录
- **Rename**：打开父目录后调用 `DirAccess.Rename`

### GodotPathResolver

薄包装层，委托给 `Origo.Core.Utility.PathUtility`：
- **Combine** → `PathUtility.Combine`：路径拼接 + 遍历攻击（`..`）检测
- **GetParentDirectory** → `PathUtility.GetParentDirectory`：父目录提取 + 根路径边界处理
- **NormalizeDirectoryPath** → `PathUtility.NormalizeDirectoryPath`：去除尾部斜杠
- **ExtractGlobSuffix** → `PathUtility.ExtractGlobSuffix`：`"*.json"` → `".json"`

路径逻辑本身已移至 Core 层，`GodotPathResolver` 保持为同构转发以满足 GodotAdapter 内部文件系统模块的调用一致性（所有路径操作统一经此包装层，`GodotDirectoryOperations` 不再直接引用 `PathUtility`）。

## 设计决策

### 为什么文件操作按 File/Directory/Path 三重拆分

单一 `GodotFileSystem` 类如果包含所有实现细节会过长（预期 200+ 行）。按职责拆分后每个静态类聚焦一种操作类型，减少导航成本。`GodotFileSystem` 是唯一公开的 `public` 类型。

### 为什么 Rename 不是用 FileAccess 实现

Godot 目录的 rename/move 操作需要打开目标所在父目录，然后对完整路径执行 `DirAccess.Rename`。文件级别的 rename 底层也需要目录操作，因此放在 `GodotDirectoryOperations` 中。

### 为什么 Copy 使用 read-then-write 而非流式传输

当前存档文件（JSON、map）体积小（KB 级），read-then-write 简单可靠。若未来有大型资源文件复制需求，可在上层（如 `SaveStorageFacade`）引入流式传输，不修改底层接口。

---
[↑ 回到 Origo.GodotAdapter](../README.md)
