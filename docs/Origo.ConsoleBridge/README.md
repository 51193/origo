# Origo.ConsoleBridge

> [↑ 回到 Origo.manual](../README.md) · [↔ Core: Runtime/Console](../Origo.Core/Runtime/Console/README.md)

## 概述

TCP 远程控制台桥接服务器。允许通过 telnet/nc 连接（默认端口 9876），远程执行 Origo 控制台命令并接收输出。单连接模式：同时只允许一个客户端连接。

## 包含文件

| 文件 | 职责 |
|------|------|
| `ConsoleBridgeOptions.cs` | 配置选项：端口号（默认 9876）|
| `ConsoleBridgeServer.cs` | TCP 控制台桥接器：Accept 线程 + Handle 线程 + 输出订阅 |

## 架构

```
telnet client ──TCP:9876──> ConsoleBridgeServer
                                ├── input:  ConsoleInputBuffer.Enqueue(line)
                                └── output: ConsoleOutputChannel.Subscribe(OnConsoleOutput)
                                              → StreamWriter.WriteLine
```

**线程模型**：
- **Accept 线程**：后台线程，循环接受连接。有活跃连接时拒绝新连接（单连接模式）
- **Handle 线程**：每个连接一个后台线程，`ReadLine` 阻塞读取 → Enqueue 到共享 InputQueue
- **输出线程安全**：所有 `StreamWriter` 访问——输出广播写入与连接建立时的积压冲刷——都在 `lock(_writerLock)` 内串行化，避免输出线程与 Handle 线程并发写入同一 writer

## 使用方式

```bash
# 启动服务器（在 Godot 项目代码中）
var server = new ConsoleBridgeServer(consoleInput, consoleOutput);
server.Start();

# 客户端连接
nc localhost 9876
> help
> spawn my_entity template_basic
> snd_count
```

## 设计决策

### 为什么单连接模式

Origo 的游戏帧循环是单线程的。多连接意味着多条命令流并发进入 `ConsoleInputBuffer`，但命令执行是帧内串行的——多客户端场景下无法确定哪条命令先执行，导致不可预期的结果。

### 为什么输出采用 publish-subscribe 而非直接写入 socket

`ConsoleBridgeServer` 订阅 `IConsoleOutputChannel`，所有日志和控制台输出（不只是 Bridge 连接的客户端的命令输出）都推到连接。这让远程连接者可以看到完整的游戏日志流，便于调试。

### 为什么 Accept 和 Handle 分离为两个线程

Accept 线程只负责建立连接，立即交给 Handle 线程处理 I/O。如果 Accept 线程在处理连接 I/O 时阻塞，下一个客户端将无法连接。线程分离确保 Accept 线程始终处于"等待下一个连接"状态。

### 异常传播策略（fail-fast）

`ConsoleBridgeServer` 不捕获 I/O 异常做静默降级。`Dispose`、`AcceptLoop`、`HandleConnection` 中的 I/O 操作若失败，异常直接传播。客户端断连产生的异常通过 `when (_cts.IsCancellationRequested)` 精确捕获，系统级 socket 错误和写入失败不再被静默吞掉。

---
[↑ 回到 Origo.manual](../README.md)
