# TestSupport

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)

## 测试辅助设施

GodotAdapter 测试项目通过 `TestSupport/` 目录提供轻量级测试替身：

| 设施 | 类型 | 用途 |
|------|------|------|
| `NullFileSystem` | `IFileSystem` 实现 | 最小文件系统替身：所有 I/O 操作抛出 `NotSupportedException`，路径操作返回基础拼接 |
| `TestSndSceneHost` | `ISndSceneHost` 实现 | 内存中的场景宿主，维护实体字典，支持按名称查找和 RequestKillEntity |
| `TestLogger` | `ILogger` 实现 | 收集日志到按级别分类的列表中（Debugs/Infos/Warnings/Errors） |
| `TestRuntimeHelper` | 静态工厂类 | 快速创建 `OrigoRuntime` + `TestSndSceneHost` 组合，内置 `NullFileSystem` |
