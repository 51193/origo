<!-- docsync-pair: Origo.Core.Kernel/Ports/README -->
<!-- docsync-revision: 3 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Ports

> [↑ 回到 Origo.Core.Kernel](../README.zh.md)

kernel-shell internal port 命名空间。port 为 shell 调用方构造并绑定 runtime/context，对消费者编译不可见，仅通过 `InternalsVisibleTo` 提供给 shell 程序集。

## Port 契约

| Port | 存在原因 | 移除条件 |
|------|----------|----------|
| `HostKernelPort` | Core shell facade 需要在 kernel 编译资产不进入消费者依赖图的前提下构造具体 runtime/SND context；有文件系统时按 `SaveRootPath/system.json` 持久化 system blackboard。 | 当 shell 能通过稳定契约构造等价 host，或组合逻辑迁入共享 host 包后移除。 |
| `AdapterHostKernelPort` | Godot adapter shell 需要为真实 Godot `Node` 场景宿主构造 runtime、observer topology 与 SND context，同时不得复制 Core 的启动编排。 | 当 scene host 可以完全通过稳定契约构造 runtime/context，而无需 adapter 专用 port 时移除。 |

## 包含文件

| 文件 | 职责 |
|------|------|
| `HostKernelPort.cs` | 见源码文档与 API 注释。 |
| `AdapterHostKernelPort.cs` | 见源码文档与 API 注释；同时定义 `ISndSceneHostRuntimeBinder`、`AdapterRuntimeBundle` 与 `AdapterContextOptions`。 |

---
[↑ 回到 Origo.Core.Kernel](../README.zh.md)
