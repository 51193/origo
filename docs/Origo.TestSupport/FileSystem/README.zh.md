<!-- docsync-pair: Origo.TestSupport/FileSystem/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# FileSystem

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

`IFileSystem` 的纯内存测试替身，无任何物理文件系统依赖。

## 包含文件

| 文件 | 职责 |
|------|------|
| `TestMemoryFileSystem.cs` | 完整实现 `IFileSystem` 接口（文件/目录读写、枚举、删除、复制、重命名、路径拼接/父目录）。基于 `Dictionary<string, string>` 存储文件内容，`HashSet<string>` 记录目录。提供调用计数追踪属性用于行为验证。 |

## 设计决策

### 为什么使用内存实现而非 mock 框架

`IFileSystem` 接口的方法组合（Write → Read → Exists → Delete → Enumerate）形成有状态协作，mock 框架的逐个方法 stub 无法正确模拟这种跨调用状态。内存实现保持与真实文件系统一致的语义，同时零 I/O 开销。

---

[↑ 回到 TestSupport](../README.zh.md)
