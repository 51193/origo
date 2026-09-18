<!-- docsync-pair: release-process -->
<!-- docsync-revision: 15 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# 发布与 Changelog 流程

> [↑ 回到 Origo 手册](README.zh.md)

本文件是 Origo **版本快照、正式发布与 Changelog 规则**的详细权威说明。
[AGENTS.md](../AGENTS.md) 仍是强制门禁与路由文档；若硬门禁与本文件冲突，
以 AGENTS.md 为准并修正本文件。开发循环见 AGENTS.md 的 Development Loop 章节；
文档维护规则见 [META.zh.md](META.zh.md)。

## Changelog 约定

`CHANGELOG.md` 基于 [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/)
并遵循 [Semantic Versioning](https://semver.org/spec/v2.0.0.html)。

| 分类 | 含义 |
|------|------|
| `Added` | 新功能 |
| `Changed` | 既有行为的变更 |
| `Deprecated` | 即将移除的功能 |
| `Removed` | 已移除的功能 |
| `Fixed` | 缺陷修复 |
| `Security` | 安全改进 |

**破坏性变更不单独设分类**：归入 `Changed`（行为变更）或 `Removed`（API 移除），
条目以 `BREAKING:` 开头，并在正文说明迁移方式。

### 基线与版本

- 基线是**最后一个正式发布 tag**（如 `v0.0.9`），不是 nightly tag。
- 带 `-nightly`、`-alpha`、`-preview` 等后缀的是快照标识，不是语义版本；
  这些变更保留在 `[Unreleased]`，只有无后缀正式版本才生成 `## [x.y.z] - YYYY-MM-DD` 块。
- 不记录同一版本周期内的来回反复（先加后删、先引入后修复）；只记录最终状态。
- 从用户视角描述行为影响，不写内部实现细节；跨模块共同设计不得误记为 `Fixed`。

### 写入流程

1. 找到最后一个正式发布 tag。
2. 对比该 tag 到当前 HEAD 的差异。
3. 过滤掉版本内来回反复，归类用户可见的显著变更。
4. 写入 `[Unreleased]` 对应分类。

## 每周快照构建

- 定时任务：每周一 **02:30 UTC**；窗口为**刚结束的一周**
  `[上一个周一 00:00, 当前周一 00:00) UTC`。
- 窗口内没有新提交时不发布；`workflow_dispatch` 可手动覆盖当前部分周。
- 快照 tag 形如 `v<base>-nightly.YYYYMMDD`，`<base>` 从
  `Directory.Build.props` 的 `<Version>` 推导。
- 快照 tag 复用正式发布流水线产出包与文档快照；`verify-release.sh`
  对带 `-` 的版本跳过正式版元数据校验。

## 正式发布清单

在打 tag 前按顺序完成：

1. 确定新版本号 `x.y.z`（无 `-nightly` 等后缀）。
2. 把 `[Unreleased]` 内容移入 `## [x.y.z] - YYYY-MM-DD` 块并清空 `[Unreleased]`。
3. 更新 `Directory.Build.props` 的 `<Version>` 为 `x.y.z`，并把
   `AssemblyVersion` / `FileVersion` 改为 `x.y.z.0`；tag 名为 `vx.y.z`。
   若 tag 提交里的版本戳不是它，发布流水线会自行创建 release 提交修正，
   并在测试打包成功后回写 `main` 与 tag；但前提是 tag 打在 `main` 当前 tip 上，
   tag 提交仍应尽量保留正确值。
4. 更新 `docs/README.zh.md` 与 `docs/README.en.md` 的版本说明为 `x.y.z`。
5. 把 `Origo.SourceGeneration/AnalyzerReleases.Unshipped.md` 中已发布的规则移入
   `AnalyzerReleases.Shipped.md`，并新增 `## Release x.y.z` 块。
6. 运行 `TAG_VERSION=x.y.z bash scripts/verify-release.sh` 并确认通过。
   该校验要求：CHANGELOG 有对应版本块、`[Unreleased]` 为空、analyzer shipped
   块存在、`AnalyzerReleases.Unshipped.md` 中没有未发布规则、两个 `docs/README.*` 提到该版本。
7. 运行 `dotnet run --project tools/DocSyncTool -- generate`。
8. 一次性提交 `CHANGELOG.md`、`Directory.Build.props`、analyzer release 文件、
   两个 `docs/README` 版本戳、所有 docs 内容、生成 hub 与 `.sync-status.json`。
9. 在该提交上运行完整 `bash scripts/ci.sh`，确认 lint-scripts、format、doc-sync、
   test、benchmark、Godot integration 全部通过；失败则 amend 后重跑。
10. 在该提交上运行 `bash scripts/lint-commits.sh`；失败则修正并 amend。
11. 在已验证的提交上创建并推送 tag `vx.y.z`。

## 发布流水线产物

推送 `v*` tag 会触发 Release workflow：

- 构建前先按 tag 自动改写 `Directory.Build.props`：去掉开头的 `v` 后写入
  `<Version>`，`AssemblyVersion` / `FileVersion` 取该版本的数值四段形式
  （`-nightly` / `-alpha` 等后缀只保留在 `<Version>` 中）；随后仍会校验 tag 与
  `<Version>` 完全一致。若产生了改动，流水线会创建一次
  `chore(release): sync version stamps to <tag>` 提交，之后的 DocSync、测试、
  基准和打包全部在这个 release 提交上执行；
- 运行 `verify-release.sh`、DocSync 生成文件检查和完整测试。自动改写只覆盖版本戳，
  正式版本所需的 CHANGELOG 版本块、空的 `[Unreleased]`、analyzer shipped 块和
  两个 `docs/README.*` 版本戳仍须在 tag 提交中准备好；
- 测试与打包全部成功后，流水线用一次 `--atomic` 推送同时把 `main` 快进到该
  release 提交、并把 tag 指向它，再创建 GitHub Release；因此校验或打包失败不会
  改动任何 ref，成功时 `main`、tag、提交与产物版本一致。该回写要求 tag 创建时的
  提交就是当前 `main` tip；如果 main 已前进、tag 不在此处，或运行期间有人移动了
  main / tag，lease 检查会失败并中止发布。重跑会先解析 tag 当前指向，已由本流程
  回写过的 tag 不会重复生成 release 提交。若仓库启用了 branch/tag protection 或
  token 缺少权限，原子推送会失败并中止发布；
- 打包 `Origo.Core`、`Origo.GodotAdapter`、`Origo.ConsoleBridge` 为 NuGet 包；
- 生成包含 `docs/`、`AGENTS.md`、`CHANGELOG.md` 的文档快照压缩包；
- 把包和文档快照附加到 GitHub Release。

回写会快进 `main` 并移动既有 `v*` ref：已经获取过旧 tag 的本地仓库需要
`git fetch --tags --force` 才能看到新的 release 提交。回写使用的 `GITHUB_TOKEN`
不会再次触发 workflow，所有校验、测试、基准和打包都在本轮完成。若流程要求
release tag 永久不可移动，应改为只把 release 提交推到 `main` 或专门分支，
不执行移动 tag。

包不作为正式制品推送到 nuget.org；消费者按根 [README](../README.md) 的说明，
从 GitHub Release 下载后配置本地包源。

## 相关文件

- [CHANGELOG.md](../CHANGELOG.md)
- [scripts/verify-release.sh](../scripts/verify-release.sh)
- [.github/workflows/release.yml](../.github/workflows/release.yml)
- [.github/workflows/weekly-build.yml](../.github/workflows/weekly-build.yml)

---
[↑ 回到 Origo 手册](README.zh.md)
