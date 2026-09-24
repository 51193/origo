<!-- docsync-pair: Origo.Core/README -->
<!-- docsync-revision: 21 -->
<!-- docsync-revision — 由 DocSyncTool 根据 git 历史自动管理；请勿手改。 -->
# Origo.Core

> [↑ 回到 Origo Manual](../README.zh.md)

## 模块概述

**Origo.Core** 是消费者 shell 包：包含平台无关的 shell facade
（`OrigoHost`）、网格/随机工具与 SND 扩展辅助；runtime 构造、
SND 内部、持久化、codec 与 console 路由位于
[Origo.Core.Kernel](../Origo.Core.Kernel/README.zh.md)。稳定契约与共享纯工具位于
[Origo.Core.Contracts](../Origo.Core.Contracts/README.zh.md)。

## 子系统一览

| 子系统 | 能力 | 详情 |
|--------|------|------|
| [Contracts](../Origo.Core.Contracts/README.zh.md) | 稳定契约 | 接口、纯数据、策略基类、metadata 与共享 shell 工具 |
| [Kernel](../Origo.Core.Kernel/README.zh.md) | 内核实现 | runtime、SND、存档/存储、数据源、console 路由与 kernel port |
| [Grid](Grid/README.zh.md) | 网格工具 | 网格坐标转换、A* 寻路与坐标解析；`GridPos` 是 Contracts 类型 |
| [Random](Random/README.zh.md) | 随机工具 | XorShift128+ PRNG、持久化随机与噪声图生成 |
| [Snd](Snd/README.zh.md) | SND shell 辅助 | active strategy、实体身份、数值读取与 archetype 扩展 |

## 本层文件

| 文件 | 职责 |
|------|------|
| `OrigoHost.cs` | 消费者 shell host facade；通过 internal kernel port 创建 runtime/SND context 并暴露稳定接口 |

## Core Shell 工作流

仅引用 Core shell 包的消费者可以启动 host 并运行策略，而无需编译依赖
kernel 实现类型：

```csharp
using Origo.Core;
using Origo.Core.Abstractions.Entity;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Snd;
using Origo.Core.Snd.Strategy;

var host = OrigoHost.Create(new OrigoHostOptions
{
    Meta = new OrigoMeta("MyGame", "1.0.0", OrigoMeta.DefaultBanner),
    Logger = NullLogger.Instance,
    AutoDiscoverStrategies = false,
});

host.Runtime.SndWorld.RegisterStrategy(() => new CounterStrategy());
host.DriveFrame(1.0 / 60.0);
```

`host.Context` 暴露稳定的 `ISndContext` 能力面；`host.Runtime` 暴露
`IOrigoRuntime` 与 `ISndWorldAccess`。工作流需要 entry 配置或存档文件时，传入
`OrigoHostOptions.FileSystem` 并调用 `host.Bootstrap()`；策略注册与帧驱动无需
文件访问。

## 架构约束

- **无引擎依赖**：Core 不引用 Godot 或 adapter 程序集。
- **稳定编译面**：公共签名只使用 Contracts 或 Core shell 类型；kernel 编译资产不流向消费者。
- **runtime-only kernel 依赖**：`Origo.Core` 以 `PrivateAssets="compile"` 引用 `Origo.Core.Kernel`。
- **单一访问路径**：消费者通过 `OrigoHost` 启动并使用 Contracts 接口；kernel port 保持 internal。

## 依赖方向

```
Origo.Core.Contracts
        ▲
        │
Origo.Core.Kernel ◄── runtime-only ── Origo.Core shell
```

---
[↑ 回到 Origo Manual](../README.zh.md)
