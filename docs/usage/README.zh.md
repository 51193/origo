<!-- docsync-pair: usage/README -->
<!-- docsync-revision: 1 -->
<!-- docsync-revision — 每次内容变更后自增此版本号。参见 AGENTS.md §1.6。 -->
# 使用文档

> [↑ 回到 Origo.manual](../README.zh.md)

## 文档索引

Origo 框架的使用方（游戏开发者、AI Agent）文档。按照使用场景组织，从新手入门到深度参考。

| 文档 | 适用对象 | 说明 |
|------|---------|------|
| [quick-start](quick-start.zh.md) | 新用户 | 5 分钟在 Godot 4 项目中接入 Origo |
| [architecture-overview](architecture-overview.zh.md) | 开发者 | 四层运行时架构、SND 模型、持久化、并发模型 |
| [snd-entity-model](snd-entity-model.zh.md) | 策略开发者 | Strategy + Node + Data 模型详解、策略编写指南 |
| [session-model](session-model.zh.md) | 进阶开发者 | 前台/后台会话、会话生命周期、拓扑编解码 |
| [persistence-flow](persistence-flow.zh.md) | 进阶开发者 | 两阶段写入、严格读取、文件布局、存档恢复 |
| [state-machine](state-machine.zh.md) | 策略开发者 | 字符串栈状态机、Push/Pop 钩子、持久化 |
| [console-commands](console-commands.zh.md) | 调试者 | 内置控制台命令完整参考 |
| [strategy-lifecycle](strategy-lifecycle.zh.md) | 策略开发者 | 生命周期钩子闭环配对、RAII 资源管理、BeforeSave 延迟同步 |
| [design-patterns](design-patterns.zh.md) | 策略开发者 | 命名约定、Manager 服务模式、可替换实现、模板最佳实践 |
| [strategy-testing](strategy-testing.zh.md) | 测试编写者 | StrategyTestScenario 使用指南 |
| [capabilities](capabilities.zh.md) | 所有用户 | 框架完整能力清单，按功能域索引，快速了解 Origo 能做什么 |
| [agent-reference](agent-reference.zh.md) | AI Agent | 完整运行时参考：接口签名、生命周期时间线、策略编写模板 |

## 推荐阅读路径

```
新用户:
  quick-start → capabilities（浏览全部能力）→ architecture-overview → snd-entity-model

策略开发者:
  snd-entity-model → strategy-lifecycle → design-patterns → state-machine → strategy-testing

存档系统使用者:
  architecture-overview → persistence-flow → session-model

控制台调试者:
  quick-start → console-commands

AI Agent:
  agent-reference (完整参考)
```

## 相关模块文档

使用文档描述的是"如何使用 Origo"，模块文档描述的是"Origo 的内部实现"。当需要深入理解某个子系统内部的代码结构时，请参考对应的模块文档：

| 使用文档中的系统 | 对应的模块文档 |
|-----------------|---------------|
| SND 实体模型 | [Origo.Core/Snd/](../Origo.Core/Snd/README.zh.md) |
| 状态机系统 | [Origo.Core/StateMachine/](../Origo.Core/StateMachine/README.zh.md) |
| 持久化系统 | [Origo.Core/Save/](../Origo.Core/Save/README.zh.md) |
| 控制台命令 | [Origo.Core/Runtime/Console/](../Origo.Core/Runtime/Console/README.zh.md) |
| Godot 适配 | [Origo.GodotAdapter/](../Origo.GodotAdapter/README.zh.md) |

---
[↑ 回到 Origo.manual](../README.zh.md)
