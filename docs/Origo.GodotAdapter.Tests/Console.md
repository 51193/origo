# 控制台 测试（适配层）

> [↑ 回到 Origo.GodotAdapter.Tests](README.md)
> [↔ 被测模块: Origo.GodotAdapter/Console](../Origo.GodotAdapter/Console/README.md)

## 被测行为概览

验证 Godot 适配层扩展的控制台命令：`press_button`（模拟按下 Godot Button 的 Pressed 信号）、
适配层 `CommandHandlerBase` 的基类行为。

## 测试文件清单

| 文件 | 验证侧重点 |
|------|-----------|
| `PressButtonCommandHandlerTests.cs` | press_button 命令：按实体名和路径查找 Button 并发射信号 |
| `CommandHandlerBaseTests.cs` | 适配层 CommandHandlerBase 基类行为 |

---

[↑ 回到 Origo.GodotAdapter.Tests](README.md)
