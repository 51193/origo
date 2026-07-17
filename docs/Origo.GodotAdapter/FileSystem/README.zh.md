<!-- docsync-pair: Origo.GodotAdapter/FileSystem/README -->
<!-- docsync-revision: 3 -->
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
- **WriteAllText**：`FileAccess.Open(path, Write)` → `StoreString(content)`
- **Copy**：ReadAllText + WriteAllText（简单复制，适合小文件场景；大文件复制可由上层优化）
- **Delete**：`DirAccess.RemoveAbsolute(path)`

### GodotDirectoryOperations

- **Create**：`DirAccess.MakeDirRecursiveAbsolute`
- **EnumerateFiles**：支持 `*pattern` 后缀过滤和递归模式
- **DeleteRecursive**：清除目录内容（递归删除文件和子目录），保留目录容器本身
- **Rename**：打开父目录后调用 `DirAccess.Rename`

## 设计决策

### 为什么文件操作按 File/Directory 二重拆分

单一 `GodotFileSystem` 类如果包含所有实现细节会过长（预期 200+ 行）。按文件操作与目录操作拆分为两个静态类，减少导航成本。路径处理逻辑位于 `Origo.Core.Utility.PathUtility`（已从 GodotAdapter 层提取），`GodotFileSystem` 和 `GodotDirectoryOperations` 直接调用，无需中间包装层。

### 为什么 Rename 不是用 FileAccess 实现

Godot 目录的 rename/move 操作需要打开目标所在父目录，然后对完整路径执行 `DirAccess.Rename`。文件级别的 rename 底层也需要目录操作，因此放在 `GodotDirectoryOperations` 中。

### 为什么 Copy 使用 read-then-write 而非流式传输

当前存档文件（JSON、map）体积小（KB 级），read-then-write 简单可靠。若未来有大型资源文件复制需求，可在上层（如 `SaveStorageFacade`）引入流式传输，不修改底层接口。

### 为什么 DeleteRecursive 不删除目录容器本身

`DirAccess.Remove`/`RemoveAbsolute` 在 Godot 编辑器进程内对 `user://` 路径不可靠：引擎在运行时持有已创建目录的文件描述符，即使目录内容已清空，`RemoveAbsolute` 仍返回 `Error.Failed`。目录内容清空后容器为空无害，后续存档写入操作会自然覆盖。同时避开此问题也无需引入 `System.IO` 等非适配层 API，保持适配层仅通过 Godot API 操作文件系统的约束。此问题不影响导出游戏——因为独立进程中不存在编辑器持有的 fd 引用。

---
[↑ 回到 Origo.GodotAdapter](../README.zh.md)
