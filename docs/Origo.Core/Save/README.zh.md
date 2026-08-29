<!-- docsync-pair: Origo.Core/Save/README -->
<!-- docsync-revision: 8 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Save

> [↑ 回到 Origo.Core](../README.zh.md)
> [↔ 相关测试: Save-Storage](../../Origo.Core.Tests/Save-Storage.zh.md) · [Save-Serialization](../../Origo.Core.Tests/Save-Serialization.zh.md) · [Save-Meta](../../Origo.Core.Tests/Save-Meta.zh.md)

## 模块能力

Origo 的持久化系统。负责存档的完整生命周期：Payload 构建、文件读写（两阶段写入）、快照管理、路径布局策略、展示元数据收集。遵循"严格读取、显式失败、两阶段写入"的持久化契约。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Meta](Meta/README.zh.md) | 展示元数据构建与合并 | ISaveMetaContributor + SaveMetaMerger + meta.map 编解码 |
| [Serialization](Serialization/README.zh.md) | 存档序列化编排 | BlackboardSerializer + SndSceneSerializer + SaveContext |
| [Storage](Storage/README.zh.md) | 存储层完整实现 | 两阶段写入、严格读取、路径布局、快照管理 |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `PersistentBlackboard.cs` | 持久化黑板：每次修改自动保存到磁盘；通过原子写入（临时文件 + 重命名 + 备份交换）防止崩溃导致文件损坏。磁盘状态需显式调用 `LoadFromDisk()` 加载（构造时不自动加载）。中断写入的残留临时文件在加载时自动清理。 |
| `SavePayloads.cs` | 存档载荷模型：`SaveGamePayload` / `LevelPayload` / 序列化容器 |
| `WellKnownKeys.cs` | `internal` — 黑板键常量：`SessionTopology` / `ActiveSaveId` 等 |
| `SaveCoordinator.cs` | 存档协调器：负责构建存档 payload、持久化 progress 状态、管理元数据的独立类 |
| `SaveFileHandle.cs` | 统一 I/O 上下文（位于 Storage 子模块）：封装 FileSystem + IoGateway + SaveRootPath + PathPolicy |

## 持久化流程

```
ISndSaveOperations.RequestSaveGame(saveId)
    │
    ▼
SaveCoordinator.BuildSavePayload(...)
    ├── BuildSaveMetaContext()
    │       └── 收集 SaveMetaBuildContext (saveId, levelId, 黑板, 场景)
    ├── SerializeProgress()  →  progress.json
    ├── SerializeSession()   →  session.json
    └── BuildSndScene()  →  snd_scene.json
    │
    ▼
SaveGamePayload (完整存档对象)
    │
    ▼
SavePayloadWriter.WriteToCurrent(handle, payload)
    ├── 创建 .write_in_progress marker
    ├── 写入 current/progress.json + progress_state_machines.json
    ├── 写入 current/level_*/snd_scene.json
    ├── 写入 current/level_*/session.json
    ├── 写入 current/level_*/session_state_machines.json
    ├── 写入 current/meta.map
    └── 删除 .write_in_progress
    （current/.payload.sha 由 SaveAtomicWriter.WritePayloadSha 单独写入，记录 combined hash）
    │
    ▼
DefaultSaveStorageService.WriteSavePayloadToCurrentThenSnapshot(...)
    ├── 检查 save_{id}/.payload.sha 是否存在且 hash 相同 → 跳过（幂等去重）
    ├── 重建 .write_in_progress marker
    ├── 复制 current/ → save_{id}.tmp/
    ├── 备份-替换：旧 save_{id}/ 改名为 save_{id}.bak/ → save_{id}.tmp/ 重命名为 save_{id}/ → 删除 save_{id}.bak/
    └── 删除 .write_in_progress marker
```

> 注：`current/.payload.sha` 在 `WriteToCurrent` 完成后、快照 marker 重建前写入，携带 combined hash（payload + `extra/` 目录）；快照路径的 `.payload.sha` 用于幂等去重。

## 严格读取规则

- **current/ 有 `.write_in_progress`** → 抛异常（上次写入中断，需处理）
- **关卡三件套不全**（部分存在）→ 抛异常（数据损坏）
- **progress.json 或 progress_state_machines.json 缺失** → 抛异常（含 current/ 完全不存在的情形）
- **格式版本高于当前支持版本** → 抛异常（拒绝加载未来版本存档）
- **拓扑引用的关卡无对应载荷**（前台或后台）→ 抛异常（拓扑不一致的存档，前台与后台均拒绝静默降级为空会话）

---
[↑ 回到 Origo.Core](../README.zh.md)
