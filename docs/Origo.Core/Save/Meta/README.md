# Meta

> [↑ 回到 Save](../README.md)

## 概述

存档展示元数据（meta.map）的构建与合并系统。展示元数据与业务数据（progress.json、session.json）分离，仅用于存档选择界面展示（如关卡名、游玩时间、截图路径）。通过可插拔的贡献者模式收集元数据键值对。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ISaveMetaContributor.cs` | 元数据贡献者接口 |
| `DelegateSaveMetaContributor.cs` | 委托适配的贡献者实现 |
| `SaveMetaBuildContext.cs` | 单次存档时的只读构建上下文 |
| `SaveMetaDataEntry.cs` | 存档槽条目模型（SaveId + MetaData 字典） |
| `SaveMetaMerger.cs` | 合并贡献者 + 入参覆写的合并逻辑 |

## 模块详解

### 元数据贡献流程

1. **注册贡献者**：实现 `ISaveMetaContributor`，通过 `ISndSaveOperations.RegisterSaveMetaContributor()` 注册（支持接口实例和委托两种重载）
2. **构建上下文**：存档时创建 `SaveMetaBuildContext`（含 saveId、levelId、黑板、场景访问）
3. **收集**：`SaveMetaMerger.Merge()` 按注册顺序调用每个贡献者的 `Contribute()`，同名键后者覆盖前者
4. **覆写**：调用方提供的 `customMeta` 参数再次键级覆盖
5. **持久化**：最终字典通过 `BuildStringMapNode()` 转为 JSON DataSourceNode 树，由 `SavePayloadWriter` 写入 `meta.map`

### ISaveMetaContributor

单方法接口：
```csharp
IReadOnlyDictionary<string, string> Contribute(in SaveMetaBuildContext context);
```

贡献者返回自己产出的键值对字典，而非修改外部传入的可变字典。多个贡献者的结果由 `SaveMetaMerger` 按注册顺序合并——同名键后者覆盖前者。此设计防止贡献者调用 `target.Clear()` 或 `target.Remove()` 破坏其他贡献者的输出。

### SaveMetaMerger

静态工具类。合并逻辑：非空 contributors → 按序贡献 → 覆写键（跳过空值键）→ null 或无键时返回 null。

## 设计决策

### 为什么展示元数据与业务数据分离

业务数据（progress.json / session.json）包含完整的时序状态和类型信息，体积大；展示元数据仅包含存档选择界面需要的少量摘要。分离后读取存档列表时只需读取 meta.map（KB 级），无需解析完整的 progress.json（可能 MB 级）。

### 为什么贡献者是按序同名覆盖而非去重

不同贡献者可能对同一键有不同视角（如"play_time"，一个贡献者从流程黑板读取，另一个可能从会话黑板）。后注册者的值可能更准确。按序覆盖提供可预测的优先级模型。

### 为什么贡献者返回独立字典而非修改可变 target

原 `void Contribute(context, IDictionary<string, string> target)` 设计中，贡献者持有可变 `target` 引用，可以调用 `Clear()`、`Remove()` 等方法破坏此前其他贡献者的结果。改为返回 `IReadOnlyDictionary<string, string>` 后，每个贡献者只产出自己的键值对，合并（覆盖顺序）由框架的 `SaveMetaMerger` 统一处理——贡献者无法影响彼此的产出。

### 为什么 SaveMetaBuildContext 是 readonly struct

构建上下文在存档调用树中频繁传递。struct 避免堆分配，readonly 确保贡献者无法修改上下文（防止副作用扩散）。`in` 参数按引用传递避免拷贝。

---
[↑ 回到 Save](../README.md)
