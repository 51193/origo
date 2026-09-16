<!-- docsync-pair: Origo.TestSupport/Snd/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Snd

> [↑ 回到 TestSupport](../README.zh.md)

## 概述

测试支撑库中的 SND 离线构建工具。`LevelBuilder` 从 Core 生产程序集迁移而来，供测试与工具代码以流式 API 构造 `LevelPayload`，不进入框架运行时的生产路径。

## 包含文件

| 文件 | 职责 |
|------|------|
| `LevelBuilder.cs` | internal 流式关卡构建器：`AddEntity` / `AddEntityFromTemplate` / `AddEntities` / `SetSessionData` → `Build()` 产生 `LevelPayload`，或 `Commit()` 直接写入存储服务的 `current/`。使用 `StubSndSceneHost` 作为零依赖场景容器 |

## 使用

```csharp
var builder = new LevelBuilder("lvl_1", sndWorld, storageService)
    .AddEntityFromTemplate("npc", "guard")
    .SetSessionData("difficulty", 2);
var payload = builder.Build();
```

业务代码仍应通过模板与 `entry.json` 构建运行时关卡；`LevelBuilder` 面向测试和离线工具场景。

---

[↑ 回到 TestSupport](../README.zh.md)
