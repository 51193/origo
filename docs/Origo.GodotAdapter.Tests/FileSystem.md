# 文件系统 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)
> [↔ 被测模块: Origo.GodotAdapter/FileSystem](../Origo.GodotAdapter/FileSystem/README.md)

## 被测行为概览

验证 GodotFileSystem 对 `res://`（只读）和 `user://`（可写）虚拟路径前缀的处理，
路径遍历保护（禁止 `..` 逃逸）。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `GodotFileSystemPathTests.cs` | res:// / user:// 路径解析和遍历保护 |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.md)
