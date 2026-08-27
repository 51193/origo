<!-- docsync-pair: Origo.TestSupport/FileSystem/README -->
<!-- docsync-revision: 2 -->
<!-- docsync-revision — bump me on every content change. See AGENTS.md §1.6 for rules. -->

# FileSystem

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

`IFileSystem` 的纯内存测试替身，无任何物理文件系统依赖。

## 包含文件

| 文件 | 职责 |
|------|------|
| `MemoryFileSystem.cs` | 纯内存 `IFileSystem` 参考实现：基于 `Dictionary<string, string>` 存储文件内容，`HashSet<string>` 记录目录；实现文件/目录读写、枚举、删除、复制、重命名、路径拼接/父目录。 |
| `TestMemoryFileSystem.cs` | `MemoryFileSystem` 的测试装饰器，增加 `SeedFile` 便利方法与 `ReadAllTextCallCount` 调用计数。 |

## 设计决策

### 为什么使用内存实现而非 mock 框架

`IFileSystem` 接口的方法组合（Write → Read → Exists → Delete → Enumerate）形成有状态协作，mock 框架的逐个方法 stub 无法正确模拟这种跨调用状态。内存实现保持与真实文件系统一致的语义，同时零 I/O 开销。

### 为什么 MemoryFileSystem 位于 TestSupport 而非 Core

`MemoryFileSystem` 没有生产消费者，只有测试项目经 `TestMemoryFileSystem` 或直接引用。按 AGENTS §1.2，生产程序集不应为测试便利承载内部实现，因此将参考实现放在 `Origo.TestSupport`，Core 只保留 `IFileSystem` 抽象与生产实现所需的路径/数据源组件。

---

[↑ 回到 TestSupport](../README.zh.md)
