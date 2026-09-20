<!-- docsync-pair: Origo.Core.Kernel/Ports/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Ports

> [↑ 回到 Origo.Core.Kernel](../README.zh.md)

kernel-shell internal port 命名空间。port 为 shell 调用方构造并绑定 runtime/context，对消费者编译不可见，仅通过 `InternalsVisibleTo` 提供给 shell 程序集。

## Port 契约

| Port | 存在原因 | 移除条件 |
|------|----------|----------|
| `HostKernelPort` | Core shell facade 需要在 kernel 编译资产不进入消费者依赖图的前提下构造具体 runtime/SND context。 | 当 shell 能通过稳定契约构造等价 host，或组合逻辑迁入共享 host 包后移除。 |

## 包含文件

| 文件 | 职责 |
|------|------|
| `HostKernelPort.cs` | 见源码文档与 API 注释。 |

---
[↑ 回到 Origo.Core.Kernel](../README.zh.md)
