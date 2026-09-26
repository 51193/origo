<!-- docsync-pair: architecture/shell-api-baseline -->
<!-- docsync-revision: 4 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Shell API 基线与门禁

> [↑ 回到 architecture](README.zh.md) · [↔ shell/kernel 边界](shell-kernel-boundary.zh.md) · [↔ API 分类](shell-api-classification.zh.md)

本文定义 0.1.0 shell API 基线门禁：用 tracked Roslyn inventory 从真实
Release 构建产出确定性 JSON，任何未批准的 shell API 增删或签名变化都必须
在同一 reviewed change 中更新基线。

## 范围

- inventory 覆盖 `Origo.Core.Contracts`、`Origo.Core`、`Origo.GodotAdapter` 与
  `Origo.ConsoleBridge` 四个 shell 程序集的导出面。
- 导出类型（含 `static`/`sealed`/`abstract`/`readonly`/`ref` 等 C# 类型修饰符与 enum/struct kind）、
  public/protected 成员、签名、nullable 注解、默认值、泛型约束、`init`/`set` accessor、
  扩展方法 `this` 参数、Source Generator 生成的公开成员与 Godot 生成的嵌套 signal 类型都进入基线。
- `Origo.Core.Kernel` 是 kernel 实现包，不进入基线；工具发现导出签名引用
  kernel 程序集类型时显式失败。`Origo.ConsoleBridge` 也进入成员级基线；其
  shell-only 依赖与包消费另由 #42 的门禁覆盖。

## 门禁执行

`scripts/api-inventory.sh`：

1. 以 Release 配置构建四个 shell 项目；仓库全局的 warnings-as-errors 设置
   使编译器或 Source Generator 诊断错误在生成 inventory 前终止。
2. 运行 `tools/ApiInventoryTool` 的 Roslyn metadata inventory。
3. `verify` 模式逐 API 行与 `tools/ApiInventoryTool/shell-api-baseline.json`
   比对；任何新增、删除或签名变化都以非零退出码失败，并输出差异与更新命令。
4. `generate` 模式用于已批准变更：重写基线 JSON，必须与实现同 commit review。

该脚本在普通 CI（Ubuntu/macOS/Windows）、`scripts/ci.sh` 与 Release workflow
中执行，因此未批准变更无法通过任一门禁。

## 基线与所有权

| 项 | 说明 |
|----|------|
| 基线文件 | `tools/ApiInventoryTool/shell-api-baseline.json` |
| 生成者 | `bash scripts/api-inventory.sh generate`，禁止手写 |
| Owner | 框架维护者；review 时确认每个差异是预期的 shell 契约变化 |
| 更新条件 | 已批准的新 shell API、移除或签名变更，且测试与双语文档在同一变更中更新 |
| 内核影响 | Kernel 实现的增删不触发基线变化；引用泄漏会直接失败 |

首次 0.1.0 发布前没有上一稳定 shell 包，因此 previous-package validation
显式跳过并输出说明。之后 `scripts/api-inventory.sh` 在未设置
`ORIGO_PREVIOUS_API_BASELINE` 时会通过 git tag 自动提取 HEAD 可达的、排除当前
提交所在 tag 的最新正式 release 树的 `shell-api-baseline.json`；显式环境变量
优先。previous-package compare 要求移除或签名变化失败，允许向后兼容的新增
API：0.1.x 允许兼容新增，行为破坏与移除进入 0.2.0。0.1.x 只承诺
source/behavior 兼容，不承诺 binary 兼容。

## 失败语义

- 构建失败或 Source Generator 诊断错误：脚本在 inventory 生成前退出，不产生
  半份基线。
- 程序集缺失、引用目录缺失、引用无法解析、导出签名引用 kernel 类型：工具输出
  明确错误并非零退出，不写 `generate` 输出。kernel 引用检查递归覆盖泛型类型实参、
  数组/指针/函数指针元素、indexer 参数与方法泛型约束。
- 基线文件缺失：`verify` 明确失败并提示先在同一 reviewed change 中生成基线。
- 确定性：JSON 按程序集名与 API 行 ordinal 排序，规范化 LF，重复运行字节一致；
  `ApiInventoryTool.Tests` 覆盖确定性、增删检测、previous-package 规则、kernel
  引用拒绝与命令行失败路径。

## 设计决策

### 为什么先用 JSON baseline，而不是 analyzer

0.1.0 的 shell 面仍在稳定过程中。tracked JSON baseline 能在普通 CI 中给出可
review 的完整差异，并允许在稳定后评估 `PublicApiAnalyzers` 一类 analyzer
门禁；在基线稳定前直接上 analyzer 会把未定型的实现细节冻结成诊断噪声。

### 为什么允许新增 API 也要改基线

门禁的目标是“消费者看到的 shell 面只能被有意改变”，而不是禁止演进。新增
API 同样改变消费者契约，因此和删除、签名变化一样需要显式 review 与同 commit
基线更新。

### 为什么基线不包含 kernel 程序集

Kernel 包在 0.1.x 没有消费者兼容承诺；把 kernel 导出面纳入 baseline 会把实现
细节误冻结为契约。Shell 签名引用 kernel 类型属于编译面泄漏，直接失败而不是
记录为允许差异。

---
[↑ 回到 Origo 手册](../README.zh.md)
