<!-- docsync-pair: Origo.GodotAdapter/FileSystem/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# FileSystem

> [↑ 回到 Origo.GodotAdapter](../README.zh.md) · [↔ Core 抽象: Abstractions/FileSystem](../../Origo.Core/Abstractions/FileSystem/README.zh.md)

## 概述

`IFileSystem` 接口的 Godot 实现。基于 Godot 的 `FileAccess` 和 `DirAccess` API，支持 `res://`（只读项目资源）和 `user://`（可写用户数据）两种虚拟路径方案。所有路径操作在此层实现，正确处理 Godot 虚拟路径语义。

## 包含文件

| 文件 | 职责 |
|------|------|
| `GodotFileSystem.cs` | `IFileSystem` 的 Godot 实现，委托给分段静态类 |
| `GodotFileOperations.cs` | 文件级操作：Exists/ReadAllText/WriteAllText/Copy/Delete |
| `GodotDirectoryOperations.cs` | 目录级操作：Exists/Create/EnumerateFiles/EnumerateDirectories/Rename/DeleteRecursive（路径操作直接调用 `Origo.Core.Utility.PathUtility`） |

## 模块详解

### GodotFileSystem

薄外观层，将所有 `IFileSystem` 方法委托给适当的静态工具类。例如 `Exists` → `GodotFileOperations.Exists`，`DirectoryExists` → `GodotDirectoryOperations.Exists`。

### GodotFileOperations

- **ReadAllText**：`FileAccess.Open(path, Read)` → `GetAsText()`
- **WriteAllText**：先确保父目录存在（`DirAccess.MakeDirRecursiveAbsolute`），再 `FileAccess.Open(path, Write)` → `StoreString(content)`；嵌套路径写入与内存文件系统替身语义一致
- **Copy**：ReadAllText + WriteAllText（简单复制，适合小文件场景；大文件复制可由上层优化）
- **Delete**：`DirAccess.RemoveAbsolute(path)`

### GodotDirectoryOperations

- **Create**：`DirAccess.MakeDirRecursiveAbsolute`
- **EnumerateFiles**：支持 `*pattern` 后缀过滤和递归模式，包含隐藏文件（点前缀，如 `.write_in_progress` 写中标记）
- **DeleteRecursive**：清除目录内容（递归删除文件和子目录，含隐藏文件），随后经父句柄尽力移除目录容器本身（引擎持 fd 时回退为保留空容器）
- **Rename**：打开父目录后调用 `DirAccess.Rename`

## 设计决策

### 为什么文件操作按 File/Directory 二重拆分

单一 `GodotFileSystem` 类如果包含所有实现细节会过长（预期 200+ 行）。按文件操作与目录操作拆分为两个静态类，减少导航成本。路径处理逻辑位于 `Origo.Core.Utility.PathUtility`，`GodotFileSystem` 和 `GodotDirectoryOperations` 直接调用，无需中间包装层。

### 为什么 Rename 不是用 FileAccess 实现

Godot 目录的 rename/move 操作需要打开目标所在父目录，然后对完整路径执行 `DirAccess.Rename`。文件级别的 rename 底层也需要目录操作，因此放在 `GodotDirectoryOperations` 中。

### 为什么 Copy 使用 read-then-write 而非流式传输

当前存档文件（JSON、map）体积小（KB 级），read-then-write 简单可靠。若未来有大型资源文件复制需求，可在上层（如 `SaveStorageFacade`）引入流式传输，不修改底层接口。

### 为什么 DeleteRecursive 尽力移除目录容器本身而非永远保留

`DirAccess.Remove`/`RemoveAbsolute` 在 Godot 编辑器进程内对 `user://` 路径不可靠：引擎在运行时持有已创建目录的文件描述符，即使目录内容已清空，容器移除仍可能返回 `Error.Failed`。因此容器移除是**尽力而为**：先经父句柄移除容器，失败时回退为保留空容器——空容器无害，后续存档写入操作会自然覆盖。

在 headless 与导出游戏进程（不持有 fd）中容器移除会成功，这使适配层与 `IFileSystem.DeleteDirectory` 契约一致，并避免 `SaveAtomicWriter.SwapSnapshotDirectory` 在残留空 `.bak` 容器上执行 rename 时因目标已存在而失败。实现沿用集成测试运行器已验证的父句柄 + 相对名移除机制，仅使用 `System.IO` 的异常类型（`IOException` / `DirectoryNotFoundException`），不引入文件系统 API 依赖。

---
[↑ 回到 Origo.GodotAdapter](../README.zh.md)
