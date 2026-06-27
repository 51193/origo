# 持久化流程

> [↑ 回到 usage](README.md)

## 概述

Origo 的存档系统遵循 **严格读取、显式失败、两阶段写入** 契约。所有文件 I/O 通过 `IDataSourceIoGateway` 进行，禁止直接 `File.*` API。

## 文件布局

```
{saveRoot}/
├── current/                              # 活动存档（临时的，运行时状态）
│   ├── .write_in_progress                # 写入进行中标记
│   ├── progress.json                    # 流程黑板（全局进度 + 完整会话拓扑）
│   ├── progress_state_machines.json      # 流程级状态机快照
│   ├── meta.map                         # 展示元数据（存档列表）
│   ├── extra/                           # 策略存档内文件（随存档生命周期）
│   └── level_{id}/                      # 各关卡数据
│       ├── snd_scene.json               # 关卡三件套：
│       ├── session.json                 #   - 场景实体列表
│       └── session_state_machines.json   #   - 会话黑板
│                                         #   - 会话状态机
├── save_001/                            # 快照存档 1
│   ├── extra/                           # 策略存档内文件（随存档生命周期）
│   └── ... (同上结构)
├── save_002/                            # 快照存档 2
│   ├── extra/
│   └── ...
└── save_.../
```

## 两阶段写入

### 阶段 1：写入 current/

```
1. 创建 current/.write_in_progress marker
2. 写入 progress.json
3. 写入 progress_state_machines.json
4. 写入 meta.map（若有自定义元数据）
5. 遍历所有关卡：
   a. 写入 level_{id}/snd_scene.json
   b. 写入 level_{id}/session.json
   c. 写入 level_{id}/session_state_machines.json
6. 删除 .write_in_progress marker
```

### 阶段 2：快照到 save_{id}/

```
1. 重建 current/.write_in_progress marker
2. 创建 save_{id}.tmp/
3. 递归复制 current/ 全部文件到 save_{id}.tmp/
4. 若 save_{id}/ 已存在，删除之
5. 原子重命名 save_{id}.tmp/ → save_{id}/
6. 删除 .write_in_progress marker
```

如果阶段 2 失败，`current/` 数据完整但 marker 残留。下次读取 `current/` 时会因为 marker 而拒绝读取，日志记录需要人工或重试处理。

## 严格读取规则

| 条件 | 行为 |
|------|------|
| `current/` 有 `.write_in_progress` | 抛 `InvalidOperationException`（写入中断，需先处理） |
| 关卡三件套全部缺失 | 视为"该关卡无存档"（`null`） |
| 关卡三件套部分存在 | 抛异常（数据损坏） |
| `progress.json` 缺失 | 抛异常 |
| 所有关卡都不存在 | 视为"尚无存档" |

## 存档结构

### SaveGamePayload

```csharp
SaveGamePayload {
    SaveId,                     // 存档槽 ID
    ActiveLevelId,              // 当前活跃关卡
    ProgressNode,               // 流程黑板：全局进度 + 会话拓扑 (DataSourceNode)
    ProgressStateMachinesNode,  // 流程级状态机
    CustomMeta,                 // 展示元数据字典
    Levels: {                   // 所有关卡数据（key 为 levelId，同一 levelId 不会出现多份——框架在构建 payload 前会校验 levelId 唯一性）
        {levelId}: LevelPayload {
            LevelId,
            SndSceneNode,               // 场景实体列表
            SessionNode,                // 会话黑板
            SessionStateMachinesNode    // 会话状态机
        }
    }
}
```

## 存档 API

### 请求保存

```csharp
// 在策略中：
ctx.RequestSaveGame("my_save_id");
ctx.RequestSaveGameAuto();  // 自动生成时间戳 ID

// ProgressRun 会处理这些请求：
// - 收集 Progress 黑板
// - 收集 Session 黑板（通过 BeforeSave 钩子触发策略最终修改）
// - 收集 SND 场景（通过 BuildMetaList）
// - 构建 SaveGamePayload
// - 两阶段写入
```

### 请求加载

```csharp
ctx.RequestLoadGame("my_save_id");    // 加载指定槽位
ctx.RequestContinueGame();            // 尝试继续游戏
ctx.RequestLoadInitialSave();         // 加载初始存档
ctx.RequestLoadMainMenuEntrySave();   // 加载主菜单入口存档
```

### 枚举存档

```csharp
// 获取所有存档槽 ID
var ids = saveStorageService.EnumerateSaveIds();

// 获取存档槽 + 展示元数据（用于存档选择界面）
var entries = saveStorageService.EnumerateSavesWithMetaData();
// entries[i].SaveId → "001"
// entries[i].MetaData → { "play_time": "2h30m", "level": "town" }
```

## meta.map 展示元数据

展示元数据与业务数据分离。`meta.map` 仅存储存档选择界面需要的少量摘要信息：

```
save_id: my_save_001
level_name: Town
play_time: 2h30m
player_name: Alice
```

通过 `ISaveMetaContributor` 贡献者接口自定义元数据，然后通过 `ctx.RegisterSaveMetaContributor(...)` 注册：

```csharp
// 贡献者实现
class MySaveMetaContributor : ISaveMetaContributor
{
    public IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context)
    {
        return new Dictionary<string, string>
        {
            ["play_time"] = CalculatePlayTime(),
            ["player_name"] = context.Progress?.TryGet<string>("player_name").value ?? ""
        };
    }
}

// 注册（在 OrigoDefaultEntry.ConfigureSaveMetadataContributors 或策略中）
ctx.RegisterSaveMetaContributor(new MySaveMetaContributor());
// 也可使用委托重载：
ctx.RegisterSaveMetaContributor((context) => new Dictionary<string, string>
{
    ["custom"] = "value"
});
```

## 路径策略

`ISavePathPolicy` 控制目录和文件布局，可替换以适应不同平台：

```csharp
public interface ISavePathPolicy
{
    string GetCurrentDirectory();
    string GetSaveDirectory(string saveId);
    string GetProgressFile(string baseDir);
    string GetProgressStateMachinesFile(string baseDir);
    string GetCustomMetaFile(string baseDir);
    string GetLevelDirectory(string baseDir, string levelId);
    string GetLevelSndSceneFile(string baseDir, string levelId);
    string GetLevelSessionFile(string baseDir, string levelId);
    string GetLevelSessionStateMachinesFile(string baseDir, string levelId);
    string GetWriteInProgressMarker(string baseDir);
    string GetPayloadShaFile(string baseDir);
    string GetExtraDirectory(string baseDir);
}
```

默认实现 `DefaultSavePathPolicy` → `SavePathLayout` 提供标准布局。

## 相关文档

- [会话模型](session-model.md) — Session 与存档的关系
- [架构总览](architecture-overview.md) — 持久化在整体架构中的位置

---
[↑ 回到 usage](README.md)
