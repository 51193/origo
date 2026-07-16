<!-- docsync-pair: docs/Origo.ConsoleBridge.Tests/Architecture -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 程序集架构守卫 测试

> [↑ 回到 Origo.ConsoleBridge.Tests](README.zh.md)
> [↔ 被测模块: Origo.ConsoleBridge](../Origo.ConsoleBridge/README.zh.md)

## 被测行为概览

通过反射验证 ConsoleBridge 程序集不依赖 Godot 引擎或 Origo.GodotAdapter，
确保 TCP 远程控制台桥接在无 Godot 运行时环境中可独立使用。

## 测试文件

| 文件 | 验证侧重点 |
|------|-----------|
| `Architecture/ConsoleBridgeArchitectureGuardrailTests.cs` | 程序集依赖方向与封装完整性 |

## 正确路径

| 测试方法 | 验证的行为 | 文档出处 |
|---------|-----------|---------|
| `ConsoleBridge_ShouldNotReferenceGodot` | 不引用任何 `Godot*` 前缀的程序集 | Origo.ConsoleBridge |
| `ConsoleBridge_ShouldNotReferenceGodotAdapter` | 不引用 `Origo.GodotAdapter` 程序集 | Origo.ConsoleBridge |
| `ConsoleBridge_ShouldOnlyReferenceCore` | 仅依赖 `Origo.Core` + BCL（`System.*`/`Microsoft.*`/`netstandard`/`System.Runtime`），无其他非预期程序集引用 | Origo.ConsoleBridge |

## 已知覆盖缺口

| 缺口描述 | 影响 | 文档依据 |
|---------|------|---------|
| 未验证依赖的具体版本号范围 | 程序集版本兼容性 | Origo.ConsoleBridge |

---

[↑ 回到 Origo.ConsoleBridge.Tests](README.zh.md)
