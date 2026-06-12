# Agent 强制工作流

> **任何对本仓库的代码变更，必须完成以下四步闭环。缺少任何一步视为未完成。**

---

## 四步闭环

| 步骤 | 说明 |
|------|------|
| 1. 代码变更 | 在本仓库实现功能/修复/重构 |
| 2. Changelog 对齐 | 将面向用户的显著变更写入 `CHANGELOG.md` 的 `[Unreleased]` 区块 |
| 3. 文档严格同步 | 在 `origo.manual` 仓库镜像更新目录结构、接口列表、设计决策 |
| 4. 测试文件补齐 | 新增 public API 必须有行为测试，修复的 bug 必须有回归测试 |

**禁止只完成部分步骤。**

---

## 仓库布局约定

两个仓库作为兄弟目录 checkout：

```
<workspace>/
├── origo/          # 本仓库（源代码）
└── origo.manual/   # 文档仓库
```

文档仓库相对路径：`../origo.manual`

详细的文档同步规则、Changelog 编写规范、测试要求见：

- `../origo.manual/AGENTS.md` — 完整四步闭环规范
- `../origo.manual/META.md` — 文档编写和同步规则

---

## Changelog 编写规则（摘要）

格式：[Keep a Changelog 1.1.0](https://keepachangelog.com/zh-CN/1.1.0/)

### 关键约束

1. **基线是上一个正式发布版本的 tag。** 对比上一个正式版本到当前状态的差异，提炼面向用户的显著变更。
2. **禁止记录版本内的来回变动。** 功能引入又删除、引入后又修复自身引入的 bug——这些噪声不应出现在 changelog 中。
3. **面向用户撰写。** 描述行为变化对使用者的影响，而非内部实现细节。
4. **日常变更写入 `[Unreleased]`。** Nightly 每日构建，变更累积在 `[Unreleased]`；发正式版本时移入带版本号的区块。

### 分类

| 类别 | 含义 |
|------|------|
| `Added` | 新添加的功能 |
| `Changed` | 对现有功能的变更 |
| `Deprecated` | 已不建议使用、即将移除的功能 |
| `Removed` | 已经移除的功能 |
| `Fixed` | 对 bug 的修复 |
| `Security` | 对安全性的改进 |

---

## 测试

- 运行命令：`bash scripts/ci.sh`
- 测试项目：`Origo.Core.Tests`、`Origo.GodotAdapter.Tests`、`Origo.ConsoleBridge.Tests`

---

## 发版流程

1. 将 `[Unreleased]` 内容移入 `## [x.y.z] - YYYY-MM-DD` 区块
2. 清空 `[Unreleased]`
3. 更新 `Directory.Build.props` 中的 `<Version>`
4. 提交并打 tag
