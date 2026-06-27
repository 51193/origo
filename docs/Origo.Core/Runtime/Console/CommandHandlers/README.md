# CommandHandlers

> [↑ 回到 Console](../README.md) · [↔ Usage: console-commands](../../../../usage/console-commands.md)

## 概述

内置控制台命令处理器的具体实现。每个命令对应一个 `ConsoleCommandHandlerBase` 的子类，通过 `OrigoConsole` 注册到 `ConsoleCommandRouter`。所有命令处理器均为 `internal`。

## 包含文件

| 文件 | 命令 | 功能 |
|------|------|------|
| `HelpCommandHandler.cs` | `help` | 列出所有已注册命令及帮助信息 |
| `BlackboardGetCommandHandler.cs` | `bb_get` | 读取黑板中指定层的键值 |
| `BlackboardSetCommandHandler.cs` | `bb_set` | 向黑板写入值（自动推断类型：int/float/bool/string）|
| `BlackboardKeysCommandHandler.cs` | `bb_keys` | 列出指定黑板层的全部键名 |
| `SpawnTemplateCommandHandler.cs` | `spawn` | 按模板生成 SND 实体（支持位置/命名参数）|
| `FindEntityCommandHandler.cs` | `find_entity` | 按名查找实体并显示其节点信息 |
| `KillAllCommandHandler.cs` | `kill_all` | 立即标记所有实体为待销毁（帧末统一执行） |
| `SndCountCommandHandler.cs` | `snd_count` | 显示当前实体数量 |
| `GetEntityDataCommandHandler.cs` | `entity_get_data` | 按名称和键读取 SND 实体数据（值及类型） |
| `SetEntityDataCommandHandler.cs` | `entity_set_data` | 设置 SND 实体数据（自动推断类型，保留已有键的类型） |
| `InvokeStrategyCommandHandler.cs` | `invoke_strategy` | 按实体名调用主动策略，传入可选 JSON 输入并输出返回值 |

## 命令详细

### bb_get

```
bb_get <layer> <key>
```
从指定黑板层读取键值。当前仅支持 `layer=system`。

### bb_set

```
bb_set <layer> <key> <value>
```
向黑板写入值。值类型自动推断：整数→Int32、浮点→Single、"true"/"false"→Boolean、其余→String。

### spawn

```
spawn <name> <template>
spawn name=<name> template=<template>
```
从模板生成实体。不支持混合使用位置参数和命名参数。模板通过 `SndWorld.ResolveTemplate` 解析。

### invoke_strategy

```
invoke_strategy <entity> <strategy_index> [input]
```
按实体名查找实体，调用 `ISndActiveStrategyAccess.InvokeStrategy` 执行主动策略，输出返回值。input 可选，支持 JSON 字符串，由策略自行解析。

## 设计决策

### 为什么仅 bb_get/bb_set 支持 system 层

运行时早期阶段，控制台主要用于调试系统状态。progress/session 层黑板在未启动流程/会话时为 null，暴露它们会增加命令复杂度和错误处理。后续可扩展。

### 为什么 bb_set 自动推断类型

在调试控制台中手动输入值是主要场景，让用户指定类型（如 `bb_set system key 42 int`）会降低效率。自动推断覆盖 95% 的调试需求（整数、浮点、布尔、字符串）。

### 为什么 spawn 支持命名参数

位置参数 `spawn player template_basic` 清晰但受限。命名参数 `spawn name=player template=template_basic` 在模板名较长时更可读，且为未来扩展（如 `spawn name=x template=y pos=10,20`）预留空间。

---
[↑ 回到 Console](../README.md)
