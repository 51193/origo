<!-- docsync-pair: tools/DocSyncTool.Tests/README -->
<!-- docsync-revision: 5 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# DocSyncTool 测试

> [↑ 回到 Origo 手册](../../README.zh.md)

`DocSyncTool` 工具（`tools/DocSyncTool/`）的测试项目，位于 `tools/DocSyncTool.Tests/`，用于在隔离的临时仓库脚手架中验证工具的四个核心命令：`generate`、`validate`、`init` 及配置加载。

## 测试能力

| 被测单元 | 覆盖能力 |
|----------|----------|
| `Validator` | 双语 pair 的 revision 一致性、缺语言文件、跨语言/裸 `.md`/断链、缺失元数据头与 revision 提醒注释、pair 声明与路径不符、非法 revision 值；代码块/行内代码与外部 URL 链接豁免 |
| `Generator` | 每目录 `README.md` 导航中枢生成、幂等性（无变化不重写）、`.sync-status.json` 状态判定（`synced` / `zh-ahead` / `missing-en`）、子目录递归、无文档目录跳过、无元数据文件的默认派生，以及基于 git 历史的 `docsync-revision` 自动规划（多 commit push、翻译追赶、仅元数据 commit、未提交本地修改） |
| `Migrator` | `.md` → `.zh.md` 重命名与元数据注入、裸 `.md` 链接重写为 `.zh.md`、外链 URL 不被误改写、跳过已带语言后缀/已迁移/目标已存在的文件、嵌套目录 pair 派生 |
| `Config` | 配置解析（键大小写不敏感）、语言代码校验（空白/斜杠/反斜杠拒绝）、缺配置/非法 JSON 的失败行为 |
| `DocFile` | 语言后缀提取与 pair id 派生 |
| `Program` | 命令分发与退出码、未知命令 usage、仓库根查找失败 FATAL（进程工作目录敏感的测试在串行集合中运行） |

## 约定

- 与仓库其他测试项目一致：扁平命名空间 `DocSyncTool.Tests`（`.editorconfig` 对该路径豁免 IDE0130/CA1062）。
- 通过 `InternalsVisibleTo` 访问工具的 `internal` 类型。
- 每个测试在独立的临时目录中构建仓库脚手架（含 `AGENTS.md`、`docs/` 与 `tools/DocSyncTool/docsync-config.json`），不触碰真实仓库文件。git revision 相关测试会额外 `git init` 脚手架，覆盖与本地 `generate` 和 CI 相同的提交/重放逻辑。
- 覆盖率门槛：行覆盖率 ≥ 90%（`ThresholdStat=total`），与其他测试项目一致。
- **测试辅助 `ConsoleOutputCapture`**：把 `Console.Out`/`Console.Error` 重定向到静默写入器，使工具的预期输出（负面测试的 "Validation FAILED" 诊断、generate 进度行、迁移横幅）不污染测试运行器日志——"Validation FAILED" 在 CI 日志中看起来像构建失败。因重定向进程全局控制台流，涉及捕获的涉及捕获的测试类（`ProgramTests`/`ValidatorTests`/`GeneratorTests`/`MigratorTests`/`GitRevisionTests`/`GitRevisionAdvancedTests`）在串行集合 `DocSyncToolConsoleCapture` 中运行。

[↑ 回到 Origo 手册](../../README.zh.md)
