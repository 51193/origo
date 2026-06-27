# Save

> [↑ 回到 Origo.Core](../README.md)
> [↔ 相关测试: Save-Storage](../../Origo.Core.Tests/Save-Storage.md) · [Save-Serialization](../../Origo.Core.Tests/Save-Serialization.md) · [Save-Meta](../../Origo.Core.Tests/Save-Meta.md)

## 模块能力

Origo 的持久化系统。负责存档的完整生命周期：Payload 构建、文件读写（两阶段写入）、快照管理、路径布局策略、展示元数据收集。遵循"严格读取、显式失败、两阶段写入"的持久化契约。

## 子模块

| 子模块 | 能力 | 详情 |
|--------|------|------|
| [Meta](Meta/README.md) | 展示元数据构建与合并 | ISaveMetaContributor + SaveMetaMerger + meta.map 编解码 |
| [Serialization](Serialization/README.md) | 存档序列化编排 | BlackboardSerializer + SndSceneSerializer + SaveContext |
| [Storage](Storage/README.md) | 存储层完整实现 | 两阶段写入、严格读取、路径布局、快照管理 |

## 本层核心文件

| 文件 | 职责 |
|------|------|
| `PersistentBlackboard.cs` | 持久化黑板：自动从磁盘加载/保存，修改写入 `current/` 目录 |
| `SavePayloads.cs` | 存档载荷模型：`SaveGamePayload` / `LevelPayload` / 序列化容器 |
| `WellKnownKeys.cs` | `internal` — 黑板键常量：`SessionTopology` / `ActiveSaveSlot` 等 |
| `SaveCoordinator.cs` | 存档协调器：从 ProgressRun 提取的独立类，负责构建存档 payload、持久化 progress 状态、管理元数据 |
| `SaveFileHandle.cs` | 统一 I/O 上下文（位于 Storage 子模块）：封装 FileSystem + IoGateway + SaveRootPath + PathPolicy |

## 持久化流程

```
ProgressRun.RequestSaveGame(saveId)
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
    ├── 写入 current/progress.json
    ├── 写入 current/level_*/snd_scene.json
    ├── 写入 current/level_*/session.json
    ├── 写入 current/level_*/session_state_machines.json
    ├── 写入 current/meta.map
    ├── 写入 current/.payload.sha
    └── 删除 .write_in_progress
    │
    ▼
DefaultSaveStorageService.WriteSavePayloadToCurrentThenSnapshot(...)
    ├── 检查 save_{id}/.payload.sha 是否存在且 hash 相同 → 跳过（幂等去重）
    ├── 重建 .write_in_progress marker
    ├── 复制 current/ → save_{id}.tmp/
    ├── 删除旧 save_{id}/（若存在）
    ├── 重命名 save_{id}.tmp/ → save_{id}/
    └── 删除 .write_in_progress marker
```

## 严格读取规则

- **current/ 有 `.write_in_progress`** → 抛异常（上次写入中断，需处理）
- **关卡三件套不全**（部分存在）→ 抛异常（数据损坏）
- **progress.json 缺失** → 抛异常
- **全部缺失** → 视为"尚无存档"（合法状态）

---
[↑ 回到 Origo.Core](../README.md)
