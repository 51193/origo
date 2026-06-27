# 持久化：元数据 测试

> [↑ 回到 Origo.Core.Tests](README.md)
> [↔ 被测模块: Origo.Core/Save/Meta](../Origo.Core/Save/Meta/README.md)
> [↔ 被测行为: usage/persistence-flow](../usage/persistence-flow.md)

## 被测行为概览

验证 `meta.map` 展示元数据的构建、合并与持久化。
覆盖 `ISaveMetaContributor` 贡献者接口、`SaveMetaMerger` 多来源合并、
`SaveMetaBuildContext` 上下文传递。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `DelegateSaveMetaContributorTests.cs` | ISaveMetaContributor 委托包装正确性 |
| `SaveMetaBuildContextTests.cs` | SaveMetaBuildContext 上下文数据传递 |
| `SaveMetaIntegrationTests.cs` | 完整链路：RegisterSaveMetaContributor → RequestSaveGame → CustomMeta 写入 meta.map |
| `SaveMetaMergerTests.cs` | SaveMetaMerger 多贡献者合并 |

## 测试详情

### 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| MetaContributor 通过 Contribute 添加键值 | ISaveMetaContributor 正确注入元数据 | persistence-flow: meta.map |
| SaveMetaMerger 合并多个贡献者 | 多来源元数据不冲突地合并到同一字典 | persistence-flow |
| SaveMetaBuildContext 携带上下文数据 | Context 传递 Session/Progress 引用 | ISaveMetaContributor |
| RegisterSaveMetaContributor 注册并保存 | ISaveMetaContributor 或委托注册后，RequestSaveGame 携带 CustomMeta | persistence-flow: meta.map |
| NullSndContext / SessionSndContext 注册行为 | NullSndContext 抛异常，SessionSndContext 委托到全局 | ISndSaveOperations |

### 边界路径

| 测试方法 | 边界条件 | 预期行为 |
|---------|---------|---------|
| 空贡献者列表合并 | 无贡献者时 merger 不抛异常 | — |
| 重复 key 合并 | 多个贡献者提供相同 key | 后覆盖先或抛异常（取决于设计） |
| meta.map 空内容编解码 | 空元数据编解码 | 不抛异常 |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| ISaveMetaContributor 在贡献时访问已 Dispose 的 Session | Dispose 后及时释放 Contributor 引用 | session-model: Dispose 语义 |

---

[↑ 回到 Origo.Core.Tests](README.md)
