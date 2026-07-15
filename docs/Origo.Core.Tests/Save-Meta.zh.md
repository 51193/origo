<!-- docsync-pair: Origo.Core.Tests/Save-Meta -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 持久化：元数据 测试

> [↑ 回到 Origo.Core.Tests](README.zh.md)
> [↔ 被测模块: Origo.Core/Save/Meta](../Origo.Core/Save/Meta/README.zh.md)
> [↔ 被测行为: usage/persistence-flow](../usage/persistence-flow.zh.md)

## 被测行为概览

验证 `meta.map` 展示元数据的构建、合并与持久化。
覆盖 `ISaveMetaContributor` 贡献者接口、`DelegateSaveMetaContributor` 委托包装、
`SaveMetaBuildContext` 上下文数据传递、`SaveMetaMerger` 多来源合并、
贡献者注册与 SaveGame 完整链路。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `DelegateSaveMetaContributorTests.cs` | DelegateSaveMetaContributor 委托调用与 null 构造守卫 |
| `SaveMetaBuildContextTests.cs` | SaveMetaBuildContext 属性存储与 null 参数守卫 |
| `SaveMetaIntegrationTests.cs` | 完整链路：注册→RequestSaveGame→CustomMeta 写入 meta.map，也包含 SaveMetaNullAndSessionContextTests |
| `SaveMetaMergerTests.cs` | SaveMetaMerger 多贡献者合并、覆盖优先级、null 处理 |

## DelegateSaveMetaContributorTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `DelegateSaveMetaContributor_Contribute_InvokesDelegate` | 包装的委托被正确调用并返回字典，key/value 透传 | ISaveMetaContributor |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `DelegateSaveMetaContributor_Constructor_ThrowsOnNull` | null 委托参数 | ArgumentNullException |

## SaveMetaBuildContextTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `SaveMetaBuildContext_StoresAllProperties` | SaveId/CurrentLevelId/Progress/Session/SceneAccess 全部正确存储 | ISaveMetaContributor |

### 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `SaveMetaBuildContext_ThrowsOnNullArgs` | 任意构造参数为 null（SaveId/CurrentLevelId/Progress/Session/SceneAccess） | ArgumentNullException |

## SaveMetaIntegrationTests 测试详情

### SaveMetaContributorRegistrationTests 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `RegisterSaveMetaContributor_WithISaveMetaContributor_ContributesToSavePayload` | ISaveMetaContributor 注册后 RequestSaveGame 的 Payload.CustomMeta 包含贡献键值 | persistence-flow: meta.map |
| `RegisterSaveMetaContributor_WithDelegate_ContributesToSavePayload` | 委托注册后行为与接口注册一致 | persistence-flow: meta.map |
| `MultipleContributors_LaterOverwritesEarlier` | 多个贡献者提供相同 key 时后者覆盖前者 | persistence-flow |
| `MultipleContributors_EachAddsDifferentKey` | 多个贡献者各提供不同 key，最终 CustomMeta 包含全部 | persistence-flow |
| `SaveWithoutContributors_CustomMetaIsNull` | 无贡献者注册时 CustomMeta 为 null | persistence-flow |
| `ContributorReceivesCorrectSaveMetaBuildContext` | 贡献者的回调收到正确的 SaveMetaBuildContext（SaveId/LevelId/Progress/Session） | ISaveMetaContributor |
| `SaveMultipleTimes_EachSaveHasCorrectMeta` | 多次存档各自携带对应周期的 CustomMeta | persistence-flow |

### SaveMetaContributorRegistrationTests 错误路径

| 测试方法 | 触发的错误 | 预期行为 |
|---------|-----------|---------|
| `RegisterSaveMetaContributor_ThrowsOnNullContributor` | null ISaveMetaContributor | ArgumentNullException |
| `RegisterSaveMetaContributor_ThrowsOnNullDelegate` | null 委托 | ArgumentNullException |

## SaveMetaNullAndSessionContextTests 正确路径

## SaveMetaMergerTests 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `Merge_ContributorsThenOverrides_OverridesWin` | 贡献者键值被 overrides 覆盖，非冲突键各自保留 | SaveMetaMerger |
| `Merge_LaterContributorOverwritesEarlierSameKey` | 多个贡献者相同 key 时靠后者覆盖靠前者 | SaveMetaMerger |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| `Merge_NoContributorsNoOverrides_ReturnsNull` | 无贡献者且无 overrides | 返回 null |
| `Merge_SkipsNullOverrideValues` | overrides 中某个 key 的值为 null | 保留贡献者原值，不覆盖为 null |

## 测试辅助策略

| 策略类 | 定义位置 | 用途 |
|--------|---------|------|
| `KeyValueContributor` | SaveMetaIntegrationTests.cs | 固定 key/value 的 ISaveMetaContributor 桩 |
| `SndContextTestHelper` | SaveMetaIntegrationTests.cs | SndContext 快速构造与 ProgressRun 初始化辅助 |
| `FuncContributor` | SaveMetaMergerTests.cs | 委托驱动的 ISaveMetaContributor 桩 |
| `NullSceneHost` | SaveMetaMergerTests.cs | ISndSceneHost 空实现，供 SaveMetaBuildContext 构造 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| ISaveMetaContributor 在贡献时访问已 Dispose 的 Session | Dispose 后及时释放 Contributor 引用 | session-model: Dispose 语义 |
| SaveMetaMerger 处理贡献者抛出异常的回滚行为 | 单个贡献者异常时合并结果的一致性 | — |

---

[↑ 回到 Origo.Core.Tests](README.zh.md)
