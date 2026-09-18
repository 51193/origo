<!-- docsync-pair: architecture/agent-friendly/api-inventory -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 机器 API 清单：从可编译事实导航到设计合同

> [↑ 回到 Agent Friendly 调查](README.zh.md)

调查日期：2026-09-18；仓库观察基线：`cdba5e4`。本文提出机器可读 API 库存与查询工具；示例 schema、命令和生成门禁尚未实现，不是当前 API 承诺。

## 1. Origo 具体缺什么

**观察**：[Agent Reference](../../usage/agent-reference.zh.md) 手写了 `ISndEntity`、各窄接口、`ISndContext`、会话和状态机的完整 C# 签名，方便游戏开发 Agent 快速查找；[Abstractions/Snd](../../Origo.Core/Abstractions/Snd/README.zh.md) 又维护成员数量、属性类型和职责。当前 DocSync 检查双语、revision、链接和镜像文件清单，没有把这些代码块交给编译器验证，也不校验描述与有效公开成员集合是否一致。见 [DocSync 测试能力](../../tools/DocSyncTool.Tests/README.zh.md) 与 [Validator 实现](../../../tools/DocSyncTool/Validator.cs)。

具体例子不能草率认定为缺陷：能力清单写“9 个窄角色”，架构总览写“10 个 companion”。窄接口文档已经明确 **9 个 Snd 角色 + `IStateMachineContext` = 10 个 companion 属性**；另有路径属性与 `Bootstrap`，因此也不能把“所有属性数”当 companion 数。风险是 Agent 只读取一句摘要后混淆统计口径，不是接口设计错了。一个机器清单可以准确列出属性，再由人工能力分类注明哪些属于 companion，避免重复手抄数字。见 [能力清单](../../usage/capabilities.zh.md)、[架构总览](../overview.zh.md) 和 [ISndContext 源码](../../../Origo.Core/Snd/ISndContext.cs)。

另一个例子是 [TypedData](../../Origo.Core/Snd/Metadata/README.zh.md)：`TryGetInt32` 等公开访问器与转换 operator 由生成器产出。仅扫描仓库手写 `.cs` 的 public 关键字会遗漏这些方法；反之 [生成器文档](../../Origo.SourceGeneration/README.zh.md) 明确 Adapter 的 `TypedDataLayeredExtensions` 整个类为 internal，即便类内方法标为 public，也不能将其当游戏业务 API。必须判断**外部有效可访问性**。

Agent Reference 的 `[Test]` 模板与仓库 xUnit 的 `[Fact]` 不同，但该文档面向游戏使用者，策略测试框架可配合不同断言框架；不能据此断言本仓库测试框架写错。真正该做的是给示例标注 consumer/maintainer 和 runner，并为对应组合建立可编译示例。机器库存解决签名事实，模板测试解决使用方式。

## 2. 生成什么，保留什么

建议产物分三层：编译生成 `api.json`（类型和签名事实）；人工维护小型 `capabilities.json`（业务能力、推荐入口、作用域、编排理由与文档链接）；查询器将两者 join，返回任务所需片段。人工能力绑定的 symbol 必须存在且有效公开，否则立即失败。

库存不会替代 `<summary>`、`<inheritdoc />`、异常与生命周期说明，也不能证明某条方法是正确使用路径。例如实体销毁应走 `entity.OwningSession.RequestKillEntity(name)`；把内部 scene-host 删除方法罗列得更详细反而诱导 Agent 绕过钩子和资源生命周期。跨模块白名单、接口隔离、延迟队列时机与存档完整性理由继续留在人工 docs 中。事实层可以由机器供给，语义和设计理由必须可读且可检索。

[AGENTS.md](../../../AGENTS.md) 禁止 DocFX/Sandcastle 式 API 站点。本提案是 JSON 工具事实源与手册校验，不发布另一套人类 API 网站；实施前仍应把它的范围和治理方式写入 AGENTS/META，避免工具产物与 `docs/` 形成双重权威。

## 3. 为什么需要编译后的有效表面

使用与真实 MSBuild 相同的 evaluated inputs：TFM、Configuration、条件常量、引用、AdditionalFiles、analyzer 配置及 Source Generator。Roslyn 可以获得生成器更新后的 Compilation，再用 symbol 模型输出成员；官方 `GeneratorDriver` 的 `RunGeneratorsAndUpdateCompilation` 将生成树加入输出 compilation。不能拿未运行生成器的 design-time 视图冒充最终结果。[Roslyn GeneratorDriver](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/SourceGeneration/GeneratorDriver.cs)

推荐先正常编译，再从**与该构建一致的最终 Compilation**提取语义与源码位置，并以实际 reference assembly 做签名交叉校验。引用程序集保留供编译引用所需的 API 元数据，不包含方法实现；不要加载生产 DLL 执行 `ModuleInitializer` 只为了发现 API，尤其 Godot 类型注册或 native 依赖不应进入库存工具进程。[Reference assemblies](https://learn.microsoft.com/en-us/dotnet/standard/assembly/reference-assemblies)

有效公开集合包含对外可访问类型中的 public 成员，以及对外派生类型可使用的 protected/protected internal 成员；检查所有 containing types 的可访问性、sealed 类型及不可构造/派生边界。private protected 只向同程序集派生者开放，不应伪装成跨程序集消费者接口。partial 合并为一个 symbol；接口继承与基类继承关系保留，查询时区分 declared 与 inherited；operator、扩展方法、访问器的独立可见性、nullable、generic constraints、ref/out/in 与 optional defaults 都不能丢。Roslyn `ISymbol` 提供 `DeclaredAccessibility`、`ContainingSymbol`、源码位置等基础字段，单个 public 标志不足以决定完整外部可用性。[Roslyn ISymbol](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Symbols/ISymbol.cs)

Core、ConsoleBridge、GodotAdapter 应分程序集输出，并声明配置矩阵。当前源生成器项目使用 netstandard2.0，其编译器 API 版本与 SDK 协调升级；不能为库存工具单独随意升级 Roslyn。Adapter 单独在匹配 Godot SDK 的构建环境提取；Core 清单不得带 Godot 类型。失败构建、生成器诊断或缺配置必须报错，不产出半份“成功”库存。

## 4. schema 与查询示例

下面 JSON 是拟议节选；字段与签名用于说明合同，并非真实工具输出：

```json
{
  "schemaVersion": 1,
  "assembly": "Origo.Core",
  "build": {"tfm": "net10.0", "configuration": "Release"},
  "symbols": [
    {
      "id": "P:Origo.Core.Snd.ISndContext.Save",
      "kind": "property",
      "containingType": "Origo.Core.Snd.ISndContext",
      "accessibility": "public",
      "effectiveExternalAccessibility": "public",
      "type": "Origo.Core.Abstractions.Snd.ISndSaveOperations",
      "accessors": {"get": "public"},
      "origin": "source",
      "source": "Origo.Core/Snd/ISndContext.cs",
      "documentation": "docs/Origo.Core/Abstractions/Snd/README.zh.md"
    }
  ]
}
```

完整必需字段还包括稳定 overload 身份、namespace、类型基类/接口、泛型与约束、参数/返回类型及 nullable、static/virtual/abstract/override、extension receiver、默认值、属性/事件/索引器、operator、适用配置、生成器身份与 hint name、summary 原文或其引用、源码/文档关联。symbol ID 与 canonical signature 都应保留，不能靠方法名称匹配 overload。各语言文档绑定应分别输出，不把中文链接塞进英文查询结果。动态生成源码位置用 generator+hint 标识，不输出机器特有 `obj` 绝对路径。

拟议 consumer 查询：

```bash
# 尚未实现的命令
origo api query --capability save.load --audience game --language zh
origo api query --type Origo.Core.Snd.ISndContext --declared-only --json
origo api diff --base <inventory> --current <inventory> --json
```

`save.load` 应返回真实存档入口签名、建议调用片段、请求延迟生效的合同、读档失败语义、运行时观察方式、真实示例与测试入口。后几项来源于人工 capability 绑定与手册，不能从 Roslyn“猜出来”。库存只证明符号存在，不自动证明线程安全、持久化正确或游戏规则正确。

## 5. 确定性与门禁

固定 SDK/Roslyn/Godot、包解析与配置；排序 symbol、统一路径分隔与编码、排除时间戳和本机路径。记录输入哈希、版本与 baseline，在执行元数据中保存 commit/dirty 状态，在 canonical API payload 中避免把每次提交变化混进签名 diff。同一输入生成两次应字节相同。编译器的 deterministic 属性只是基础，引用、analyzer、目录等也属于输入，JSON 规范化仍需工具自己实现。[C# deterministic compilation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/code-generation)

拟议 CI 流程是 generate → validate → 检查生成物是否已提交；与 DocSync 的统一状态交接需明确属主，不手改 hub/revision。公开表面改变必须同时更新 capability 绑定、人工设计合同、使用示例与相应行为测试；破坏性变化按现行 release 流程进入 `BREAKING:` 分类。API diff 是提示与强制对齐入口，不是恢复兼容层的理由。示例采用当前声明的使用者 runner 编译，维护者模板使用本仓库 xUnit，Godot 示例在相应 SDK/headless 环境验证。

## 6. 分阶段验收与产品价值

先只输出 Core/ConsoleBridge JSON、查询一个真实能力；再处理生成器、Adapter 与配置矩阵；最后连接 DocSync 与示例编译。验收样本必须含 generated `TryGetXxx`、partial、internal containing type、protected、overload、nullable、generic constraints、条件编译和失效人工绑定；保证 Core 无 Godot 泄露、同输入幂等、错误显式失败、签名变动可定位。成本包括 MSBuild/Roslyn耦合、配置矩阵、生成产物审查和 capability 维护，不能只为减少几十行表格造一套过重平台。

对维护 Agent，价值是避免错误签名与人工清单漂移；对开发游戏的 Agent，价值是按“生成实体”“保存并恢复”“观察数据变化”得到**可编译入口加业务合同**。后者更符合 Origo 的产品目标。应测首次编译成功率、错误 API 调用次数、真实游戏任务完成率与查询上下文量，再决定是否扩大清单。它与 [受影响检查](affected-checks.zh.md) 组合提供“找对能力 → 编译 → 验证”的回路，但运行时可观测、游戏验收和场景操作仍需独立建设。
