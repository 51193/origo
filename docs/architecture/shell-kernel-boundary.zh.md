<!-- docsync-pair: architecture/shell-kernel-boundary -->
<!-- docsync-revision: 10 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Shell/Kernel 稳定边界

> [↑ 回到 architecture](README.zh.md)

本文定义 Origo 0.1.0 的稳定 shell、kernel 与消费者兼容边界，并给出实施、
验证和发布的工作计划。对应 [issue #34](https://github.com/51193/origo/issues/34)。

## 目标与范围

Origo 的消费者契约与实现细节将不再共用同一个公开包面。稳定边界需要同时满足：

- 游戏开发者与 agent 只引用 shell 包即可编译和运行；
- kernel 实现可以独立演进，kernel 编译资产不进入消费者编译面；
- 生命周期顺序、观察者恢复、存档语义和 fail-fast 行为有可验证的兼容承诺；
- 单一访问路径、接口隔离、适配层隔离与策略无状态等架构约束保持不变；
- runnable consumer demo、机器 API inventory 和消费者文档拥有稳定的目标表面。

本边界不冻结当前全部导出类型，不暴露 kernel 实现类型，不允许兼容层绕过既有
编排入口、校验、钩子或资源生命周期。ConsoleBridge JSON Lines、第三方 TypedData
注册和新玩法能力属于后续独立工作。

## 包与依赖拓扑

稳定边界由下列包组成：

| 包 | 角色 | 内容 |
|----|------|------|
| `Origo.Core.Contracts` | 稳定契约包 | 公共接口、纯数据类型、元数据、策略基类、data source 契约和日志抽象 |
| `Origo.Core.Kernel` | 实现包 | 运行时构造、SND 内部实现、存档与存储、data source codec、控制台路由、kernel-shell port |
| `Origo.Core` | 消费者 shell 包 | 面向消费者的 host facade、包装类型与包入口；编译面由 Contracts 与 shell 类型共同组成 |
| `Origo.GodotAdapter` | Godot shell 包 | Godot `Node` 派生入口、适配层能力提供者和面向消费者的扩展点 |
| `Origo.ConsoleBridge` | 控制台桥接 shell 包 | TCP 控制台桥接服务器与配置选项 |

依赖方向固定为：

```text
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel ◄── runtime-only ── Origo.Core shell
        ▲
        │ private compile
Origo.GodotAdapter shell ──► GodotSharp

Origo.ConsoleBridge shell ──► Origo.Core.Contracts
```

`Origo.Core` shell 对 `Origo.Core.Kernel` 的依赖只提供运行期资产；消费者对
`Origo.Core` 的 restore 不会获得 kernel 的编译资产。`Origo.GodotAdapter` 可以参考
Core kernel 的私有编译面并调用其内部 port，但它的公开签名只使用 Contracts 与
shell 类型。`Origo.ConsoleBridge` 只引用 Core Contracts 与 shell 稳定接口。

### Adapter 不拆分 kernel

Adapter 是相对固定的能力提供方：Godot 文件系统、日志、序列化、节点工厂和
`Node` 派生入口。其实现通过稳定 shell 类型和 Core kernel port 组合，不承载可独立
演进的编排逻辑。因此 0.1.0 保留单一的 `Origo.GodotAdapter` shell 包，不建立
`Origo.GodotAdapter.Kernel`。

Adapter 内部实现仍然不得进入消费者编译面：非 shell 的 Godot 桥接类型、命令处理器
和辅助类型保持 internal。`Origo.GodotAdapter` 的公开类型是真实 Godot 类型，Godot
编辑器、`.tscn`、脚本类发现和序列化均直接看到它们，不依赖程序集转发。

如果 Adapter 后续出现独立兼容周期、可替换内核或多个适配器共享的编排层，再评估
引入 Adapter kernel；该评估由本手册的包拓扑章节负责。

## 稳定消费者表面

`Origo.Core.Contracts` 与 shell 包共同提供以下稳定能力组：

- host 与 runtime：`IOrigoRuntime`、`ISndWorldAccess`、`IOrigoFrameDriver`、
  `OrigoMeta`、`OrigoHost`、`OrigoHostOptions`；
- context 与 session：`ISndContext`、`ISessionManager`、`ISessionRun`、
  `ISndSceneReadAccess`；
- SND 实体与策略：`ISndEntity` 及各窄接口、生命周期/主动/观察者/状态机/计划策略
  基类、`StrategyIndexAttribute`、`ObserveDataAttribute` 和策略扩展方法；
- 数据与元数据：`TypedData`、`SndMetaData`、节点/策略/数据元数据、
  `SndMetaFluentBuilder`、`DataSourceNode` 与 data source 契约；
- 存档与文件：`ISndSaveOperations`、`ISndLifecycleOperations`、
  `ISaveMetaContributor`、`ISndFileAccess`、`ISndArchiveFileAccess`、
  `ISndTemplateAccess`；
- 黑板、状态机、控制台与日志抽象；
- Godot 入口：`OrigoAutoHost`、`OrigoDefaultEntry`、`SndEntityNodeExtensions`、
  `CommandHandlerBase`、`GodotFileSystem`、`GodotLogger`、
  `GodotJsonConverterRegistry`、`GodotPackedSceneNodeFactory`；
- ConsoleBridge：`ConsoleBridgeServer`、`ConsoleBridgeOptions`。

具体值类型与接口成员以 `Origo.Core.Contracts` 和 shell 程序集的公开面为准。每个
public 类型和成员在契约冻结时都有明确归类：shell 契约、tooling 扩展、kernel 实现
或测试专用。当前导出类型逐项见
[shell-api-classification](shell-api-classification.zh.md)；kernel 实现与测试专用
类型不进入 shell 编译面。

### TypedData 注册范围

0.1.0 的 `TypedData` 只支持 Origo 第一方类型注册。`TypedData` 的公开面提供类型化
读写与 JSON 往返；内部存储、Kind 注册和分层桥接保持在该实现边界内。第三方
adapter 注册契约由后续独立设计提供，不在 0.1.0 shell 中开放。

### 存档与存储范围

shell 只提供存档操作与展示元数据：`ISndSaveOperations`、
`ISndLifecycleOperations`、`ISaveMetaContributor`、`SaveMetaBuildContext` 和
`SaveMetaDataEntry`。`ISaveStorageService`、`ISavePathPolicy`、存档 payload、
`PersistentBlackboard` 与 `SndContextParameters` 属于 kernel 实现，消费者不直接
依赖具体存储布局或 payload 结构。自定义存储与路径策略在后续版本另行设计。

## 兼容性承诺

0.1.0 是第一个正式 shell 版本。0.1.x 提供：

- **源兼容**：消费者重新编译后可以继续使用稳定 shell API；
- **行为兼容**：生命周期顺序、观察者绑定恢复、存档/读档语义、持久化完成信号和
  fail-fast 错误行为保持不变；
- **持久化兼容**：存档格式通过 `origo.format_version` 识别；旧档读取、新档读取和
  损坏数据的失败语义显式测试；
- **生成代码兼容**：`TryGetXxx`、nullable 标注、Kind 号段和 ORIGOSG 诊断保持稳定；
- **SDK 配对**：0.1.x 支持 .NET 10 与 Godot.NET.Sdk 4.7.2；更宽范围在后续版本
  经过验证后开放。

0.1.x 不承诺二进制兼容：消费者升级 shell 包后重新编译。新增消费者 API、行为破坏
和旧 API 移除进入 0.2.0。kernel 包不承诺消费者兼容，但 kernel-shell port 的调用
契约由本手册约束。shell 与 kernel 在 0.1.x 内使用精确版本配对，避免 restore 静默
组合未测试版本。

Godot 生成的嵌套 signal 类型属于生成公开面，自动纳入 API baseline；它们由 Godot
源生成器产出，不在人工文档中逐项维护。

## Kernel-shell port 规则

Kernel 可以拥有仅供 shell 调用的内部 port，但必须满足：

- port 位于 `Origo.Core.Kernel.Ports` 命名空间，并保持 internal；
  首个 host 构造 port 是 `HostKernelPort`；
- port 只通过 `InternalsVisibleTo` 暴露给 `Origo.Core` shell 与
  `Origo.GodotAdapter` shell；
- port 调用既有编排入口，不得绕过校验、钩子、资源生命周期或状态转换；
- 每个 port 有契约测试、存在的理由和移除条件；
- port 类型不出现在 shell 公开签名中；
- shell 不复制 kernel 的内部实现来模拟行为。

Adapter 对 Core kernel 的初始化、scene host 绑定、观察者拓扑和生命周期调用通过
上述 port 完成；`Origo.GodotAdapter` 自身不新增第二套编排路径。

## 元指令例外

稳定边界需要一项有界例外：`Origo.Core`、`Origo.GodotAdapter` 与
`Origo.ConsoleBridge` 在 0.1.x 内承诺 shell 契约与行为兼容；kernel 包继续遵循
早期开发的无兼容负担规则。例外范围只覆盖 shell 契约、port 契约和兼容测试，不放宽
单一访问路径、fail-fast 或架构隔离要求。

源码与文档保持当前态描述，不使用 `legacy`、`since`、`old` 等演进标记。每个兼容
shell API 在契约基线中记录 owner 与下一个 `0.y.0` 的移除条件。

## 实施计划

### 阶段一：Contracts 提取与 API 基线

1. 建立 `Origo.Core.Contracts`，迁入稳定接口、纯数据类型、元数据、策略基类和
   data source 契约。
2. 建立 Roslyn API inventory 工具，输出可重复的 JSON shell API baseline。
3. 增加编译探针：只引用 shell 包的消费者可以编译；引用 kernel 类型必须失败。
4. 更新项目引用与测试程序集，使既有 Core 测试在新分层下通过。

### 阶段二：Core Kernel 与 Core Shell

1. 建立 `Origo.Core.Kernel`，迁入运行时构造、SND 内部实现、存档、存储与 codec。
2. 实现 `IOrigoRuntime`、`ISndWorldAccess` 与 `OrigoHost` shell facade；
   具体 `OrigoRuntime`、`SndWorld`、`SndContext`、`SndContextParameters` 保持在 kernel。
3. 建立 `Origo.Core.Kernel.Ports` 命名空间与契约测试；host 构造 port 位于其中。
4. `Origo.Core` shell 包以 Contracts 为编译面主体，kernel 只作为运行期依赖。
5. 处理 `InternalsVisibleTo`、Source Generator 宿主程序集和 ORIGOSG007 诊断。

### 阶段三：GodotAdapter 单 shell 包

1. 将 `OrigoAutoHost`、`OrigoDefaultEntry` 与其余公开 Godot 类型保留在
   `Origo.GodotAdapter` shell 程序集，保持真实 `Node` 类型身份。
2. 适配层通过 Core kernel port 完成 scene host、观察者拓扑和生命周期绑定。
3. 非 shell Godot 实现保持 internal；公开签名只使用 Contracts 与 shell 类型。
4. 验证 Godot headless 与编辑器中的入口发现、`.tscn` 引用、脚本选择和退出清理。
5. 在干净环境使用 `PackageReference` 运行 consumer smoke。

### 阶段四：ConsoleBridge 与包验证

1. `Origo.ConsoleBridge` 保持 shell-only，只引用 Contracts 与 Core shell 稳定接口。
2. 扩展能力继续由 `IConsoleInputSource`、`IConsoleOutputChannel` 与
   `IConsoleCommandHandler` 承载，桥接包不新增第二套命令路径。
3. 验证每个 shell 包的 `ref`/`lib`/analyzer 资产、运行期依赖和 kernel 隔离。
4. 将本地 feed、consumer demo 与 package smoke 纳入 CI。

### 阶段五：兼容门禁与发布

1. API diff 在新增、删除或修改 shell 公开成员时失败，直到 baseline 在同一变更中更新。
2. 运行旧 shell 契约与新 kernel 实现的 contract 测试矩阵。
3. 运行存档格式 golden tests、失败语义测试和生成代码/诊断快照。
4. 更新 `AGENTS.md`、`docs/META.*` 与发布流程，写入有界 shell 例外与打包要求。
5. 在 Changelog 记录 0.1.0 边界设计及破坏性变更，按完整 CI、发布验证与 commit
   lint 闭环后发布 0.1.0。

## 当前实现状态

Core 的 contracts/kernel/shell 拆分已在特性分支实现：
`Origo.Core.Contracts` 承载稳定消费者表面与共享纯工具，
`Origo.Core.Kernel` 承载 runtime/SND/存档/data-source/console 实现及 internal
`HostKernelPort`，`Origo.Core` 是带 `OrigoHost` 的 shell 包。
`IOrigoRuntime` 与 `ISndWorldAccess` 是 Contracts 稳定接口，分类守卫验证
kernel 实现类型不会泄漏进 Core shell 编译面。`Origo.ConsoleBridge` 已只引用
Core shell 包且不携带 kernel 编译资产。Adapter 保持单 shell 包：Godot `Node`
入口是真实公开类型，bridge/manager 实现为 internal，启动经
`AdapterHostKernelPort` 完成 runtime、observer topology 与 SND context 的构造和
绑定。兼容契约测试（#40）已落地：`OrigoHost` 公开入口驱动生命周期顺序、观察者
恢复、fail-fast 与后台会话状态转换；`AdapterHostKernelPort` 覆盖缺失 runtime
binder、context binder 与 file system 的显式失败；仓库内 `origo.format_version=1`
golden 快照覆盖当前格式、旧档缺版本键与未来版本的原子拒绝。Shell API 与生成
代码门禁（#41）已落地：`scripts/api-inventory.sh` 以 tracked Roslyn JSON baseline
校验 Contracts/Core/Adapter shell 导出面、nullable 与生成嵌套类型，未批准的增删
或签名变化在普通 CI、`scripts/ci.sh` 与 Release workflow 失败；previous-package
validation 在首个正式版本前显式跳过并记录 first-release 行为。Packaged consumption
（#42）已落地：`scripts/package-consumer-smoke.sh` 在仓库外临时目录只通过
NuGet 包恢复 Core + Adapter，执行 warnings-as-errors 构建、CS0246 kernel 泄漏
负向探测、analyzer 资产与 kernel runtime 校验，并以 `OrigoDefaultEntry` headless
启动；普通 CI Godot job 与本地 `scripts/ci.sh` 均执行。发布（#43）仍属后续工作。

## 验证与门禁

| 验证 | 通过条件 |
|------|----------|
| 编译边界 | fresh consumer 只引用 shell 包可编译；引用 kernel 类型失败 |
| 行为契约 | 生命周期顺序、观察者恢复、存档读写和 fail-fast 语义保持不变 |
| 兼容契约测试 | shell 入口的 lifecycle/observer/fail-fast/会话状态契约与 golden v1 存档格式测试在常规与发布测试中通过 |
| 包完整性 | kernel 无 compile 资产泄漏；shell 运行期依赖与 analyzer 资产完整；`scripts/package-consumer-smoke.sh` 的本地 feed restore、负向编译与 Godot headless 启动通过 |
| 兼容矩阵 | 旧 shell 契约在新 kernel 上通过 contract tests |
| Godot | headless 与编辑器验证 Node 入口发现、启动、存档恢复与退出清理 |
| API 基线 | 未批准的 shell API 变更使 CI 失败 |
| 存档格式 | `origo.format_version`、旧档/新档读取和损坏失败语义通过 golden tests |

## 风险与缓解

| 风险 | 缓解 |
|------|------|
| 兼容层绕过编排形成 backdoor | port 只调用既有编排入口，按单一访问路径审查 |
| Contracts 膨胀并冻结实现 | 只冻结消费者路径；实现细节留在 kernel |
| Adapter 公共面扩大 | 保持单 shell 包时同步维护 internal 边界和 API baseline |
| Source Generator 宿主身份漂移 | 在构建中固定宿主程序集并维护诊断/生成快照测试 |
| shell 与 kernel 版本组合失控 | 0.1.x 使用精确配对，CI 运行兼容矩阵 |
| Godot 入口发现回归 | 每次 Adapter 变更运行 headless 与编辑器入口测试 |

## 后续范围

第三方 TypedData adapter 注册、基于 analyzer 的 API 门禁、二进制兼容、自定义存储与
路径扩展、以及 Adapter kernel 的重新评估属于 0.1.0 之后的独立工作。每项工作的
详细上下文、验收条件和实现注意事项随各自设计工作进入 tracked 设计文档。

---
[↑ 回到 architecture](README.zh.md)
